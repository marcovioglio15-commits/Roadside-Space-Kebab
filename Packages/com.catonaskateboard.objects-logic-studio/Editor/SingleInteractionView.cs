using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns cached feature cards and explicit editor-only Add/Remove actions for single interactions.</summary>
    internal sealed class SingleInteractionView
    {
        #region State

        private GameObject target;
        private ObjectSingleInteraction[] features = Array.Empty<ObjectSingleInteraction>();
        private GUIContent[] names = Array.Empty<GUIContent>();
        private ObjectSingleInteraction validated;
        private string validationWarning = string.Empty;
        private static readonly GUIContent addLabel = new GUIContent("+ Add Interaction", "Add Grab, or add Drop/Throw after an enabled Grab exists.");
        private static readonly GUIContent removeLabel = new GUIContent("Remove", "Remove this feature with Undo. Remove Drop and Throw before removing Grab.");

        #endregion

        #region Methods

        #region Cache

        /// <summary>Recaches cards only when hierarchy, selection or applied values change.</summary>
        /// <param name="selected">Current workspace object.</param>
        internal void Refresh(GameObject selected)
        {
            // Repaint does not search the hierarchy or rebuild feature names.
            target = selected;
            validated = null;
            features = target != null ? target.GetComponents<ObjectSingleInteraction>() : Array.Empty<ObjectSingleInteraction>();
            names = new GUIContent[features.Length];
            for (int index = 0; index < features.Length; index++)
                names[index] = new GUIContent(features[index].Kind.ToString() == features[index].InteractionName
                    ? features[index].InteractionName : features[index].Kind + " · " + features[index].InteractionName,
                    "Expand this feature to edit its action and settings. Apply or Discard before opening another feature.");
        }

        #endregion

        #region Drawing

        /// <summary>Draws one expanded feature card and a dependency-aware Add menu.</summary>
        /// <param name="state">Persistent workspace and draft selection.</param>
        /// <param name="data">Serialized workspace used by the settings controls.</param>
        internal void Draw(ObjectWorkspace state, SerializedObject data)
        {
            // Structural changes operate in the native prefab stage and immediately save its asset.
            using (new EditorGUI.DisabledScope(state.HasChanges || target == null || EditorUtility.IsPersistent(target)))
                if (GUILayout.Button(addLabel, EditorStyles.miniButton, GUILayout.Width(135f)))
                    ShowAddMenu(state);
            for (int index = 0; index < features.Length; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool selected = features[index].Kind == state.Single.Kind;
                        bool expanded = selected && state.Single.Expanded;
                        using (new EditorGUI.DisabledScope(!selected && state.HasChanges))
                        {
                            bool requested = EditorGUILayout.Foldout(expanded, names[index], true, EditorStyles.foldoutHeader);
                            if (requested != expanded)
                            {
                                if (!selected)
                                {
                                    state.Single.Kind = features[index].Kind;
                                    state.Single.Read(target);
                                }
                                state.Single.Expanded = requested;
                                state.Persist();
                            }
                        }
                        using (new EditorGUI.DisabledScope(state.HasChanges || EditorUtility.IsPersistent(target)
                            || features[index] is ObjectGrab && features.Length > 1))
                            if (GUILayout.Button(removeLabel, EditorStyles.miniButton, GUILayout.Width(60f)))
                            {
                                Remove(state, features[index]);
                                return;
                            }
                    }
                    if (features[index].Kind == state.Single.Kind && state.Single.Expanded)
                    {
                        using EditorGUI.IndentLevelScope cardIndent = new EditorGUI.IndentLevelScope();
                        SingleInteractionPresetView.Draw(state);
                        if (SingleInteractionControls.Draw(data, state) || validated != features[index])
                        {
                            validated = features[index];
                            state.Single.Draft.TryValidate(validated, out validationWarning);
                        }
                        if (validationWarning.Length > 0)
                            EditorGUILayout.LabelField(validationWarning, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            if (features.Length == 0)
                EditorGUILayout.LabelField("Add Grab to configure object carrying.", EditorStyles.centeredGreyMiniLabel);
        }

        /// <summary>Builds the small feature menu only when its button is clicked.</summary>
        /// <param name="state">Workspace receiving the new feature selection.</param>
        private void ShowAddMenu(ObjectWorkspace state)
        {
            // Drop and Throw are unavailable until their same-object Grab exists and is enabled.
            GenericMenu menu = new GenericMenu();
            ObjectGrab grab = target.GetComponent<ObjectGrab>();
            foreach (SingleInteractionKind kind in Enum.GetValues(typeof(SingleInteractionKind)))
            {
                GUIContent label = new GUIContent(kind.ToString(), "Add the " + kind + " feature to this object.");
                if (SingleInteractionSession.Resolve(target, kind) != null || kind != SingleInteractionKind.Grab && (grab == null || !grab.enabled))
                    menu.AddDisabledItem(label);
                else
                    menu.AddItem(label, false, () => Add(state, kind));
            }
            menu.ShowAsContext();
        }

        #endregion

        #region Authoring

        /// <summary>Adds an authorized feature and required physics components in one Undo group.</summary>
        /// <param name="state">Workspace receiving the new selection.</param>
        /// <param name="kind">Feature chosen from the dependency-aware menu.</param>
        internal void Add(ObjectWorkspace state, SingleInteractionKind kind)
        {
            // Recheck menu prerequisites because an open native menu can outlive a selection change.
            if (state.HasChanges || target == null || EditorUtility.IsPersistent(target)
                || !ObjectAuthoringSave.TryValidate(target, out _)
                || SingleInteractionSession.Resolve(target, kind) != null
                || kind != SingleInteractionKind.Grab && target.GetComponent<ObjectGrab>() is not { enabled: true })
                return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add " + kind);
            Undo.RecordObject(state, "Select single interaction");
            ObjectSingleInteraction feature = kind switch
            {
                SingleInteractionKind.Grab => Undo.AddComponent<ObjectGrab>(target),
                SingleInteractionKind.Drop => Undo.AddComponent<ObjectDrop>(target),
                SingleInteractionKind.Throw => Undo.AddComponent<ObjectThrow>(target),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            using (SerializedObject serialized = new SerializedObject(feature))
            {
                serialized.FindProperty("interactionName").stringValue = kind.ToString();
                serialized.ApplyModifiedProperties();
            }
            ObjectAuthoringSave.Save(target);
            state.Single.Kind = kind;
            state.Single.Expanded = true;
            state.Single.Read(target);
            state.Persist();
            Undo.CollapseUndoOperations(group);
            Refresh(target);
        }

        /// <summary>Removes one feature without deleting user-authored rigidbodies or colliders.</summary>
        /// <param name="state">Workspace whose selected feature may change.</param>
        /// <param name="feature">Applied component represented by the clicked card.</param>
        internal void Remove(ObjectWorkspace state, ObjectSingleInteraction feature)
        {
            // A Grab remains the required dependency until both releases are removed.
            if (state.HasChanges || feature == null || EditorUtility.IsPersistent(feature)
                || !ObjectAuthoringSave.TryValidate(target, out _)
                || feature is ObjectGrab && target.GetComponents<ObjectRelease>().Length > 0)
                return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(state, "Remove single interaction");
            Undo.DestroyObjectImmediate(feature);
            ObjectAuthoringSave.Save(target);
            Refresh(target);
            if (SingleInteractionSession.Resolve(target, state.Single.Kind) == null && features.Length > 0)
                state.Single.Kind = features[0].Kind;
            state.Single.Read(target);
            state.Persist();
            Undo.CollapseUndoOperations(group);
        }

        #endregion

        #endregion
    }
}
