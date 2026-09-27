using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Exposes the same conditional tool layout controls outside the workspace.</summary>
    [CustomEditor(typeof(PlayerToolsPreset))]
    internal sealed class PlayerToolsPresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Edits asset settings with Undo and reports invalid values without normalization.</summary>
        public override void OnInspectorGUI()
        {
            // Shared controls keep the asset Inspector aligned with the Tools module.
            serializedObject.Update();
            PlayerToolsControls.DrawSettings(serializedObject);
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerToolsPreset)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
