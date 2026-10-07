using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits optional switch tracks using hierarchy choices without a preview recorder.</summary>
    [CustomEditor(typeof(PlayerToolAnimationPreset))]
    internal sealed class PlayerToolAnimationPresetEditor : UnityEditor.Editor
    {
        #region Serialized Fields

        [Header("Hierarchy")]
        [Tooltip("Sample hierarchy matching the root selected in Tools. It is not stored in the animation asset.")]
        [SerializeField]
        private GameObject hierarchySource;

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Draws target menus and local keys while reporting invalid timing without changing it.</summary>
        public override void OnInspectorGUI()
        {
            // The hierarchy sample supplies choices; animation data keeps reusable relative targets.
            hierarchySource = (GameObject)StudioGUI.ObjectField(new GUIContent("Hierarchy Source",
                "Use the same hierarchy root selected by the Tools preset."), hierarchySource, typeof(GameObject), true);
            serializedObject.Update();
            StudioGUI.PropertyField(serializedObject.FindProperty("Duration"));
            SerializedProperty tracks = serializedObject.FindProperty("Tracks");
            StudioArrayGUI.Add(tracks, "Add Track", () => new PlayerToolTrack());
            // Each track resolves only once at runtime; no manual path field is exposed here.
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(tracks, index, new GUIContent("Track " + (index + 1), "Local transform keys for one hierarchy target.")))
                    break;
                if (!track.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                PlayerHierarchyMenu.DrawPath(track.FindPropertyRelative("Path"), hierarchySource != null ? hierarchySource.transform : null,
                    new GUIContent("Target", track.FindPropertyRelative("Path").tooltip), hierarchySource != null && hierarchySource.GetComponent<PlayerHost>() == null);
                SerializedProperty keys = track.FindPropertyRelative("Keys");
                StudioArrayGUI.Add(keys, "Add Key", () => new PlayerToolKey());
                for (int key = 0; key < keys.arraySize; key++)
                {
                    if (StudioArrayGUI.Header(keys, key, new GUIContent("Key " + (key + 1), keys.tooltip)))
                        break;
                    if (keys.GetArrayElementAtIndex(key).isExpanded)
                        StudioGUI.PropertyField(keys.GetArrayElementAtIndex(key), true);
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerToolAnimationPreset)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
