using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits reusable transform tracks with local/world coordinates and slider-assisted keyframe poses.</summary>
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

        /// <summary>Uses the caller's hierarchy to resolve track targets without storing scene objects in the preset.</summary>
        /// <param name="root">Tools root, or arm root for idle clips.</param>
        internal void Bind(Transform root)
        {
            // Scene references stay in the editor context and never enter the animation asset.
            hierarchySource = root != null ? root.gameObject : null;
        }

        /// <summary>Draws target menus and local keys while reporting invalid timing without changing it.</summary>
        public override void OnInspectorGUI()
        {
            // The hierarchy sample supplies choices; animation data keeps reusable relative targets.
            if (EditorUtility.IsPersistent(target)
                && StudioButton.Draw(new GUIContent("Open Animation Editor", "Edit this preset in its own Apply/Discard window.")))
                PlayerToolAnimationWindow.Open((PlayerToolAnimationPreset)target, hierarchySource != null ? hierarchySource.transform : null);
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
                StudioFields.Draw(track, "Space", "RelativeToStart");
                SerializedProperty keys = track.FindPropertyRelative("Keys");
                StudioArrayGUI.Add(keys, "Add Key", () => new PlayerToolKey());
                for (int key = 0; key < keys.arraySize; key++)
                {
                    if (StudioArrayGUI.Header(keys, key, new GUIContent("Key " + (key + 1), keys.tooltip)))
                        break;
                    if (!keys.GetArrayElementAtIndex(key).isExpanded)
                        continue;
                    SerializedProperty frame = keys.GetArrayElementAtIndex(key);
                    StudioFields.Draw(frame, "Time", "RotationDirection");
                    PlayerToolFields.Pose(frame.FindPropertyRelative("Pose"));
                    Transform animated = PlayerHierarchy.Resolve(hierarchySource != null ? hierarchySource.transform : null,
                        track.FindPropertyRelative("Path").stringValue);
                    using (new EditorGUI.DisabledScope(animated == null))
                        if (StudioButton.Draw(new GUIContent("Read Current Pose", "Capture the selected target in the track's coordinate space. Start-relative tracks capture an identity offset.")))
                            frame.FindPropertyRelative("Pose").boxedValue = track.FindPropertyRelative("RelativeToStart").boolValue
                                ? PlayerToolPose.Identity : PlayerToolPose.Read(animated, (PlayerToolSpace)track.FindPropertyRelative("Space").enumValueIndex);
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
