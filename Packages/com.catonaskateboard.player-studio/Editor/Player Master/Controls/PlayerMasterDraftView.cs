using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws pending preset assignments without editing the contents of the candidate assets.</summary>
    internal static class PlayerMasterDraftView
    {
        #region Labels

        private static readonly GUIContent bodyLabel = new GUIContent("Body Slot", "Propose a Body for this master. Apply Slot saves only the reference; the Body asset is unchanged.");
        private static readonly GUIContent inputLabel = new GUIContent("Input Slot", "Optional Input mapping. Apply confirms the reference; action bindings stay in the assigned Input Actions asset.");
        private static readonly GUIContent locomotionLabel = new GUIContent("Locomotion Slot", "Optional movement preset. Confirm its reference before editing its numbers.");

        private static readonly GUIContent visualLabel = new GUIContent("Visual Slot", "Optional visual preset. Apply confirms this reference without changing the preset's source or offsets.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Records the pending slot change on the window before updating its master draft.</summary>
        /// <param name="session">Assignment draft being displayed.</param>
        /// <param name="undoTarget">Window that serializes the draft and owns its Undo records.</param>
        /// <returns>True when a proposed slot changed in this GUI event.</returns>
        public static bool Draw(PlayerMasterEditSession session, Object undoTarget)
        {
            // The object picker accepts only assets; Apply also checks persistence and dimensions.
            EditorGUI.BeginChangeCheck();
            PlayerBodyPreset body = PlayerPresetPicker.Draw(bodyLabel, session.BodyPreset);
            PlayerInputPreset input = PlayerPresetPicker.Draw(inputLabel, session.InputPreset);
            PlayerLocomotionPreset locomotion = PlayerPresetPicker.Draw(locomotionLabel, session.LocomotionPreset);
            PlayerVisualPreset visual = PlayerPresetPicker.Draw(visualLabel, session.VisualPreset);
            PlayerCameraPreset camera = PlayerPresetPicker.Draw(new GUIContent("Camera Slot", "Optional view and follow configuration."), session.CameraPreset);
            if (!EditorGUI.EndChangeCheck())
                return false;

            // Choosing references edits the window state, never the master's serialized slots.
            Undo.RecordObject(undoTarget, "Edit Preset Assignments");
            session.SetDraft(body, input, locomotion, visual, camera);
            return true;
        }

        #endregion

        #endregion
    }
}
