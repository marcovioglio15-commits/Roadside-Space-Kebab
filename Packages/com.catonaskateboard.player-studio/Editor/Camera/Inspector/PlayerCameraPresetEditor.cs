using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Uses the same conditional camera fields as the Player Studio draft.</summary>
    [CustomEditor(typeof(PlayerCameraPreset))]
    internal sealed class PlayerCameraPresetEditor : UnityEditor.Editor
    {
        #region State

        private readonly PlayerStudioSections sections = new PlayerStudioSections();

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Applies deliberate Inspector changes using native asset Undo.</summary>
        public override void OnInspectorGUI()
        {
            // Direct Inspector edits follow Unity's normal asset behavior, outside the Studio session.
            serializedObject.Update();
            PlayerCameraControls.Draw(serializedObject, sections);
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerCameraPreset)target).TryGetSettings(out _, out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
