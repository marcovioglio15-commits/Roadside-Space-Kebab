using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Keeps module visibility independent of the preset proposals and their common Apply footer.</summary>
    [Serializable]
    internal sealed class PlayerStudioModuleTabs
    {
        #region Serialized State

        [Header("Tabs")]
        [Tooltip("Open module tabs; closing one does not discard its values.")]
        [SerializeField]
        private int openMask = 31;

        [Tooltip("Currently displayed module: Body, Locomotion, Visual, Input or Camera.")]
        [SerializeField]
        private int active;

        [Tooltip("Visual section and handle preferences retained independently of its draft.")]
        [SerializeField]
        private PlayerVisualDraftView visualView = new PlayerVisualDraftView();

        [Tooltip("Collapsed subsections retained when tabs or the window are closed.")]
        [SerializeField]
        private PlayerStudioSections sections = new PlayerStudioSections();

        #endregion

        #region Labels

        private static readonly GUIContent menuLabel = new GUIContent("Modules", "Open or close module tabs without discarding their drafts.");
        private static readonly GUIContent closeLabel = new GUIContent("×", "Close this tab. Its pending edits remain in the session.");
        private static readonly GUIContent[] labels =
        {
            new GUIContent("Body", "Edit collision dimensions."),
            new GUIContent("Locomotion", "Edit movement, gravity and jump values."),
            new GUIContent("Visual", "Edit model source, offsets and the scene binding."),
            new GUIContent("Input", "Select compatible actions by map and action name."),
            new GUIContent("Camera", "Configure the view, follow and movement orientation.")
        };

        #endregion

        #region Methods

        #region Visibility

        /// <summary>Reports whether a tab remains open without inspecting any preset values.</summary>
        /// <param name="index">Module index in the fixed tab order.</param>
        /// <returns>True when the corresponding visibility bit is set.</returns>
        public bool IsOpen(int index)
        {
            // Visibility is not part of dirty tracking or an asset write.
            return (openMask & (1 << index)) != 0;
        }

        /// <summary>Opens or closes a module and selects another visible module when necessary.</summary>
        /// <param name="index">Module whose visibility is changing.</param>
        /// <param name="open">Whether the tab should be available.</param>
        public void SetOpen(int index, bool open)
        {
            // No draft object is passed here, so a close cannot discard data.
            openMask = open ? openMask | (1 << index) : openMask & ~(1 << index);
            if (open)
                active = index;
            else if (active == index)
                for (int candidate = 0; candidate < labels.Length; candidate++)
                    if (IsOpen(candidate))
                    {
                        active = candidate;
                        break;
                    }
        }

        /// <summary>Builds the visibility menu only after an explicit click.</summary>
        private void ShowMenu()
        {
            // Menu allocations do not occur during the ordinary repaint path.
            GenericMenu menu = new GenericMenu();
            for (int index = 0; index < labels.Length; index++)
            {
                int selected = index;
                menu.AddItem(labels[index], IsOpen(index), () => SetOpen(selected, !IsOpen(selected)));
            }
            menu.ShowAsContext();
        }

        #endregion

        #region Drawing

        /// <summary>Draws one module while retaining independent drafts across all tabs.</summary>
        /// <param name="state">Workspace proposals and scene bindings.</param>
        /// <param name="owner">Window recorded by native Undo.</param>
        /// <param name="locked">Whether Play suspends module editing.</param>
        /// <returns>True when the visible module changed a draft.</returns>
        public bool Draw(PlayerStudioState state, UnityEngine.Object owner, bool locked)
        {
            // Compact tab navigation remains available even when a module's fields are locked.
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(menuLabel, EditorStyles.toolbarDropDown, GUILayout.Width(75f)))
                    ShowMenu();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(openMask == 0))
                    if (GUILayout.Button(closeLabel, EditorStyles.toolbarButton, GUILayout.Width(22f)))
                        SetOpen(active, false);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                for (int index = 0; index < labels.Length; index++)
                    if (IsOpen(index) && GUILayout.Toggle(active == index, labels[index], EditorStyles.toolbarButton))
                        active = index;

            if (!IsOpen(active))
                return false;

            // A pending edit keeps its original asset even if the master proposes a different slot.
            ScriptableObject source = active switch
            {
                0 => state.Body.Source,
                1 => state.Locomotion.Source,
                2 => state.Visual.Source,
                3 => state.Input.Source,
                4 => state.Camera.Source,
                _ => null
            };
            if (source != null)
                EditorGUILayout.LabelField(new GUIContent("Preset", "Asset receiving the edits in this tab."),
                    new GUIContent(source.name, AssetDatabase.GetAssetPath(source)));

            // Every module retains its own draft while the other tabs remain editable.
            using (new EditorGUI.DisabledScope(locked))
                switch (active)
                {
                    case 0 when state.Body.Source != null:
                        return sections.Draw("Body.Shape", "Collision Shape") && PlayerBodyDraftView.Draw(state.Body, owner);
                    case 1 when state.Locomotion.Source != null || state.Locomotion.HasChanges:
                        return PlayerLocomotionDraftView.Draw(state.Locomotion, owner, sections);
                    case 2:
                        return visualView.Draw(state.Visual, state.VisualScene, owner);
                    case 3 when state.Input.Source != null || state.Input.HasChanges:
                        return DrawModule(state.Input, owner, false);
                    case 4 when state.Camera.Source != null || state.Camera.HasChanges:
                        bool changed = DrawModule(state.Camera, owner, true);
                        return (sections.Draw("Camera.Binding", "Scene Binding") && state.CameraScene.Draw(owner)) || changed;
                    default:
                        EditorGUILayout.LabelField("Assign and apply this module's preset to edit it.", EditorStyles.wordWrappedLabel);
                        return false;
                }
        }

        /// <summary>Edits a temporary module copy and records only the window proposal in Undo.</summary>
        /// <param name="session">Retained module draft.</param>
        /// <param name="owner">Window owning the serialized draft.</param>
        /// <param name="camera">Whether to use conditional Camera controls.</param>
        /// <returns>True when serialized draft values changed.</returns>
        private bool DrawModule(PlayerModuleEditSession session, UnityEngine.Object owner, bool camera)
        {
            // Property fields target the temporary copy, never the saved asset.
            SerializedObject serialized = session.GetEditor();
            if (serialized == null)
                return false;
            EditorGUI.BeginChangeCheck();
            if (camera)
                PlayerCameraControls.Draw(serialized, sections);
            else
            {
                if (sections.Draw("Input.Movement", "Movement"))
                    PlayerPresetField.DrawAction(serialized, "movementAction");
                if (sections.Draw("Input.Jump", "Jump"))
                    PlayerPresetField.DrawAction(serialized, "jumpAction");
                if (sections.Draw("Input.Camera", "Camera"))
                {
                    PlayerPresetField.DrawAction(serialized, "lookDeltaAction");
                    PlayerPresetField.DrawAction(serialized, "lookRateAction");
                    PlayerPresetField.DrawAction(serialized, "cursorToggleAction");
                }
            }
            if (!EditorGUI.EndChangeCheck())
                return false;
            Undo.RecordObject(owner, "Edit Player Module");
            session.Capture();
            return true;
        }

        #endregion

        #endregion
    }
}
