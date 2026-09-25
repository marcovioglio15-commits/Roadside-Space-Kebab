using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws repeated contact and dialogue cards while preserving one stable component draft.</summary>
    internal sealed class ExtendedInteractionView
    {
        #region State

        private GameObject target;
        private ObjectExtendedInteraction[] features = Array.Empty<ObjectExtendedInteraction>();
        private long[] identities = Array.Empty<long>();
        private GUIContent[] labels = Array.Empty<GUIContent>();
        private ObjectExtendedInteraction validated;
        private string warning = string.Empty;

        #endregion

        #region Methods

        #region Cache

        /// <summary>Refreshes component identities only after hierarchy, target or applied settings change.</summary>
        /// <param name="selected">Open prefab branch currently selected in the workspace.</param>
        internal void Refresh(GameObject selected)
        {
            // Duplicate feature names are harmless because each card retains its native component identity.
            target = selected;
            features = selected != null ? selected.GetComponents<ObjectExtendedInteraction>() : Array.Empty<ObjectExtendedInteraction>();
            identities = new long[features.Length];
            labels = new GUIContent[features.Length];
            validated = null;
            UnlockInteractionControls.Refresh(selected);
            AssemblyControls.Refresh(selected);
            for (int index = 0; index < features.Length; index++)
            {
                identities[index] = ObjectWorkspaceTarget.FileId(features[index]);
                labels[index] = new GUIContent((index + 1) + ". " + features[index].InteractionName,
                    "Expand this component's independent settings. Apply or Discard before selecting another component.");
            }
        }

        #endregion

        #region Drawing

        /// <summary>Lists the selected category and edits its expanded component through the common transaction.</summary>
        /// <param name="state">Persistent workspace and detached draft.</param>
        /// <param name="data">Serialized workspace wrapper.</param>
        /// <param name="kind">Feature type visible in this category.</param>
        internal void Draw(ObjectWorkspace state, SerializedObject data, ExtendedInteractionKind kind)
        {
            // Structural actions save immediately; settings retain the existing Apply/Discard workflow.
            using (new EditorGUI.DisabledScope(state.HasChanges || target == null || EditorUtility.IsPersistent(target)))
                if (GUILayout.Button(new GUIContent("+ Add Interaction", "Choose an available interaction in this category."), EditorStyles.miniButton))
                    ShowAddMenu(state, kind);
            for (int index = 0; index < features.Length; index++)
            {
                if (!InCategory(features[index].Kind, kind))
                    continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool selected = state.Extended.Kind == features[index].Kind && state.Extended.ComponentId == identities[index];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(state.HasChanges && !selected))
                        {
                            bool expanded = EditorGUILayout.Foldout(selected && state.Extended.Expanded, labels[index], true, EditorStyles.foldoutHeader);
                            if (expanded != (selected && state.Extended.Expanded))
                            {
                                if (!selected)
                                    state.Extended.Select(features[index]);
                                state.Extended.Expanded = expanded;
                                state.Persist();
                                validated = null;
                            }
                        }
                        using (new EditorGUI.DisabledScope(state.HasChanges))
                            if (GUILayout.Button(new GUIContent("Remove", "Remove this component with Undo; keep shared item state and authored HUD."),
                                EditorStyles.miniButton, GUILayout.Width(60f)))
                            {
                                ExtendedInteractionAuthoring.Remove(features[index]);
                                state.Extended.Read(target);
                                state.Persist();
                                Refresh(target);
                                return;
                            }
                    }
                    if (selected && state.Extended.Expanded)
                        using (new EditorGUI.IndentLevelScope())
                            DrawSettings(state, data, features[index]);
                }
            }
        }

        /// <summary>Shares category membership between card filtering and the creation menu.</summary>
        /// <param name="feature">Candidate interaction kind.</param>
        /// <param name="category">First interaction identifying the selected category.</param>
        /// <returns>True when the feature belongs in this category.</returns>
        private static bool InCategory(ExtendedInteractionKind feature, ExtendedInteractionKind category)
        {
            // Single-kind categories use the same menu behavior without separate button layouts.
            return feature == category || (category switch
            {
                ExtendedInteractionKind.Dialogue => feature == ExtendedInteractionKind.Slice,
                ExtendedInteractionKind.ModifyByContact => feature == ExtendedInteractionKind.Outline,
                ExtendedInteractionKind.AssemblyStation => feature == ExtendedInteractionKind.AssemblyProduct,
                _ => false
            });
        }

        /// <summary>Builds the category's dependency-aware menu only when requested.</summary>
        /// <param name="state">Workspace retaining the selected prefab.</param>
        /// <param name="category">First feature in this category.</param>
        private void ShowAddMenu(ObjectWorkspace state, ExtendedInteractionKind category)
        {
            // Capture the actual target so an older menu cannot act on a newly selected object.
            GenericMenu menu = new GenericMenu();
            GameObject owner = target;
            foreach (ExtendedInteractionKind kind in Enum.GetValues(typeof(ExtendedInteractionKind)))
            {
                if (!InCategory(kind, category))
                    continue;
                GUIContent label = new GUIContent(kind == ExtendedInteractionKind.Unlock ? "Availability Rule" : ObjectNames.NicifyVariableName(kind.ToString()),
                    "Add this interaction to the selected prefab object.");
                if (!CanAdd(kind))
                    menu.AddDisabledItem(label);
                else
                    menu.AddItem(label, false, () => Add(state, owner, kind));
            }
            menu.ShowAsContext();
        }

        /// <summary>Checks uniqueness and existing-target dependencies for the selected object.</summary>
        /// <param name="kind">Feature offered by the menu.</param>
        /// <returns>True when its structural prerequisites are satisfied.</returns>
        private bool CanAdd(ExtendedInteractionKind kind)
        {
            // Only ordinary feature components can become unlock targets.
            if (target == null)
                return false;
            switch (kind)
            {
                case ExtendedInteractionKind.Outline:
                    return target.GetComponent<ObjectOutline>() == null;
                case ExtendedInteractionKind.AssemblyProduct:
                    return target.GetComponent<ObjectAssemblyProduct>() == null;
                case ExtendedInteractionKind.Unlock:
                    foreach (ObjectInteraction interaction in target.transform.root.GetComponentsInChildren<ObjectInteraction>(true))
                        if (interaction is not ObjectInteractionUnlock)
                            return true;
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Rechecks a menu selection before creating and saving its exact feature.</summary>
        /// <param name="state">Workspace receiving the new component draft.</param>
        /// <param name="owner">Object that opened the menu.</param>
        /// <param name="kind">Requested interaction kind.</param>
        private void Add(ObjectWorkspace state, GameObject owner, ExtendedInteractionKind kind)
        {
            // Native menus can outlive a selection change or prefab-stage closure.
            if (target != owner || state.HasChanges || !CanAdd(kind) || !ObjectAuthoringSave.TryValidate(owner, out _)
                || EditorUtility.IsPersistent(owner) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            state.Extended.Select(ExtendedInteractionAuthoring.Add(owner, kind));
            state.Extended.Expanded = true;
            state.Persist();
            Refresh(owner);
        }

        /// <summary>Draws reusable configuration, local bindings and one actionable validation message.</summary>
        /// <param name="state">Workspace owning the expanded card.</param>
        /// <param name="data">Serialized detached proposal.</param>
        /// <param name="feature">Saved component represented by this card.</param>
        private void DrawSettings(ObjectWorkspace state, SerializedObject data, ObjectExtendedInteraction feature)
        {
            // Preset import changes only configuration; local input and HUD references stay with the prefab.
            if (feature is not (ObjectInteractionUnlock or ObjectAssemblyProduct))
                ExtendedInteractionPresetView.Draw(state);
            if (ExtendedInteractionControls.Draw(data, state) || validated != feature || GUI.changed)
            {
                validated = feature;
                state.Extended.Draft.TryValidate(feature, out warning);
            }
            if (feature is ObjectAssemblyProduct product
                && GUILayout.Button(new GUIContent("Open Assembly Preview", "Orbit the product and place its magnets with transform handles and ingredient prefab guides.")))
                AssemblyPreviewWindow.Open(state, product);
            if (feature is ObjectAssemblyStation station)
                using (new EditorGUI.DisabledScope(state.HasChanges))
                    if (GUILayout.Button(new GUIContent("Rebuild Staging", "Restore the inactive prefab child used to prepare new products before activation.")))
                    {
                        AssemblyAuthoring.Prepare(station);
                        ObjectAuthoringSave.Save(target);
                        Refresh(target);
                    }
            if (feature is ObjectDialogue
                && GUILayout.Button(new GUIContent("Configure Shared Dialogue HUD", "Open Scene Observer to edit the overlay used by every dialogue.")))
            {
                state.Category = ObjectInteractionCategory.SceneObserver;
                state.Persist();
            }
            if (feature is ObjectOutline outline)
                using (new EditorGUI.DisabledScope(state.HasChanges))
                    if (GUILayout.Button(new GUIContent("Collect Outline Renderers", "Collect original renderers after changing this prefab hierarchy.")))
                    {
                        OutlineAuthoring.Rebuild(outline);
                        ObjectAuthoringSave.Save(target);
                        state.Extended.Read(target);
                        state.Persist();
                        validated = null;
                    }
            if (warning.Length > 0)
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
