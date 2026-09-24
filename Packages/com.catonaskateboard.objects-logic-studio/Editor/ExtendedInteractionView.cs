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
            using (new EditorGUILayout.HorizontalScope())
            {
                AddButton(state, kind);
                if (kind == ExtendedInteractionKind.AssemblyStation)
                    AddButton(state, ExtendedInteractionKind.AssemblyProduct);
                if (kind == ExtendedInteractionKind.ModifyByContact)
                    AddButton(state, ExtendedInteractionKind.Outline);
            }
            for (int index = 0; index < features.Length; index++)
            {
                if (features[index].Kind != kind && !(kind == ExtendedInteractionKind.ModifyByContact && features[index].Kind == ExtendedInteractionKind.Outline)
                    && !(kind == ExtendedInteractionKind.AssemblyStation && features[index].Kind == ExtendedInteractionKind.AssemblyProduct))
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

        /// <summary>Adds a feature or unlock rule only where its required existing components allow it.</summary>
        /// <param name="state">Workspace guarding unfinished proposals.</param>
        /// <param name="kind">Requested feature or rule type.</param>
        private void AddButton(ObjectWorkspace state, ExtendedInteractionKind kind)
        {
            // Unlock only configures existing features; one outline owns an object's shell geometry.
            bool unavailable = kind switch
            {
                ExtendedInteractionKind.Outline => target.GetComponent<ObjectOutline>() != null,
                ExtendedInteractionKind.AssemblyProduct => target.GetComponent<ObjectAssemblyProduct>() != null,
                _ => false
            };
            if (kind == ExtendedInteractionKind.Unlock)
            {
                unavailable = true;
                foreach (ObjectInteraction interaction in target.transform.root.GetComponentsInChildren<ObjectInteraction>(true))
                    if (interaction is not ObjectInteractionUnlock)
                    {
                        unavailable = false;
                        break;
                    }
            }
            using (new EditorGUI.DisabledScope(state.HasChanges || unavailable))
                if (GUILayout.Button(new GUIContent("+ Add " + (kind == ExtendedInteractionKind.Unlock ? "Unlock Rule" : ObjectNames.NicifyVariableName(kind.ToString())),
                    "Add this configuration to the current prefab object."), EditorStyles.miniButton))
                {
                    state.Extended.Select(ExtendedInteractionAuthoring.Add(target, kind));
                    state.Extended.Expanded = true;
                    state.Persist();
                    Refresh(target);
                }
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
            if (feature is ObjectDialogue dialogue)
                DrawHud(state, dialogue);
            if (feature is ObjectOutline outline)
                using (new EditorGUI.DisabledScope(state.HasChanges))
                    if (GUILayout.Button(new GUIContent("Rebuild Outline Geometry", "Refresh authored shells after adding, removing or replacing source renderers.")))
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

        /// <summary>Exposes the prefab-local HUD and repairs missing presentation before Play.</summary>
        /// <param name="state">Workspace guarding structural changes while settings are pending.</param>
        /// <param name="dialogue">Selected dialogue component.</param>
        private void DrawHud(ObjectWorkspace state, ObjectDialogue dialogue)
        {
            // HUD creation and rebinding are explicit saved prefab operations, separate from content presets.
            if (!state.Sections.Draw("Dialogue HUD", "Use an existing local HUD; edit its text, font and panel in the prefab hierarchy."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            using (new EditorGUI.DisabledScope(state.HasChanges))
            {
                DialogueHud requested = (DialogueHud)EditorGUILayout.ObjectField(new GUIContent("HUD", "Existing complete HUD inside this object's prefab branch."),
                    dialogue.Hud, typeof(DialogueHud), true);
                if (requested != dialogue.Hud)
                {
                    if (requested != null && !requested.IsValid(dialogue.transform))
                        warning = "Choose a complete Dialogue HUD inside this object's hierarchy.";
                    else
                    {
                        using (SerializedObject binding = new SerializedObject(dialogue))
                        {
                            binding.FindProperty("hud").objectReferenceValue = requested;
                            binding.ApplyModifiedProperties();
                        }
                        ObjectAuthoringSave.Save(target);
                        state.Extended.Read(target);
                        state.Persist();
                        validated = null;
                    }
                }
                if (dialogue.Hud == null && GUILayout.Button(new GUIContent("Create Dialogue HUD", "Author or reuse local presentation before Play; no runtime UI construction.")))
                {
                    DialogueAuthoring.CreateHud(dialogue);
                    ObjectAuthoringSave.Save(target);
                    state.Extended.Read(target);
                    state.Persist();
                    validated = null;
                }
            }
        }

        #endregion

        #endregion
    }
}
