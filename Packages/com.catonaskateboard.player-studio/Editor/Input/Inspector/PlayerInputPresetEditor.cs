using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Uses the same compatible action menus in the asset Inspector and Player Studio.</summary>
    [CustomEditor(typeof(PlayerInputPreset))]
    internal sealed class PlayerInputPresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Edits action identities with native Undo and displays only invalid configuration warnings.</summary>
        public override void OnInspectorGUI()
        {
            // Shared controls keep both authoring entry points consistent.
            serializedObject.Update();
            PlayerInputActionMenu.Draw(serializedObject, "movementAction");
            PlayerInputActionMenu.Draw(serializedObject, "jumpAction");
            PlayerInputActionMenu.Draw(serializedObject, "lookDeltaAction");
            PlayerInputActionMenu.Draw(serializedObject, "lookRateAction");
            PlayerInputActionMenu.Draw(serializedObject, "cursorToggleAction");
            serializedObject.ApplyModifiedProperties();
            if (!PlayerModuleValidation.TryValidate((PlayerInputPreset)target, out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
