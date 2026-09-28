using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Exposes the same conditional tool layout controls outside the workspace.</summary>
    [CustomEditor(typeof(PlayerToolsPreset))]
    internal sealed class PlayerToolsPresetEditor : UnityEditor.Editor
    {
        #region Serialized Fields

        [Header("Preview Hierarchy")]
        [Tooltip("Player object used to browse hierarchy targets. This sample is not saved into the preset.")]
        [SerializeField]
        private GameObject hierarchySource;

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Edits asset settings with Undo and reports invalid values without normalization.</summary>
        public override void OnInspectorGUI()
        {
            // Shared controls keep the asset Inspector aligned with the Tools module.
            serializedObject.Update();
            hierarchySource = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Hierarchy Source",
                "Choose a player prefab or scene object to browse its children."), hierarchySource, typeof(GameObject), true);
            PlayerToolsControls.DrawSettings(serializedObject, hierarchySource != null ? hierarchySource.transform : null);
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerToolsPreset)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
