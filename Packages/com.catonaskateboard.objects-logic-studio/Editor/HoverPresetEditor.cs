using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Provides the same conditional dropdown controls for direct preset inspection.</summary>
    [CustomEditor(typeof(HoverPreset))]
    internal sealed class HoverPresetEditor : UnityEditor.Editor
    {
        #region Fields

        [Header("Inspector")]
        [Tooltip("Collapsed configuration menus retained across inspector reloads.")]
        [SerializeField]
        private ObjectStudioSections sections = new ObjectStudioSections();

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Edits the selected asset directly; workspace drafts detect these outside changes before Apply.</summary>
        public override void OnInspectorGUI()
        {
            // Native inspector editing remains explicit and separate from detached workspace proposals.
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                HoverControls.Draw(serializedObject.FindProperty("configuration"), sections);
            serializedObject.ApplyModifiedProperties();
            if (!((HoverPreset)target).Configuration.TryValidate(out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
