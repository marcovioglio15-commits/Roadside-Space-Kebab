using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits visual asset data with Unity's normal Inspector Undo and a single validation message.</summary>
    [CustomEditor(typeof(PlayerVisualPreset))]
    internal sealed class PlayerVisualPresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Draws the source and offset without creating models or editing the Player Studio session.</summary>
        public override void OnInspectorGUI()
        {
            // Default drawing preserves field tooltips, serialized values and the Inspector's native Undo path.
            DrawDefaultInspector();

            // Show one actionable warning; a valid preset needs no permanent help box.
            if (!PlayerVisualPresetValidation.TryValidate(serializedObject, out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
