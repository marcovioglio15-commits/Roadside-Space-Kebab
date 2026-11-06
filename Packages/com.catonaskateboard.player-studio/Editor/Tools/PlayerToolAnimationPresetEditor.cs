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
        private bool setupOpen = true;
        private bool tracksOpen = true;

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
            using EditorGUI.DisabledScope editing = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            serializedObject.Update();
            if (PlayerToolFields.Section(ref setupOpen, "Animation Setup", "Duration and hierarchy used by this animation."))
            {
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                hierarchySource = (GameObject)StudioGUI.ObjectField(new GUIContent("Hierarchy Source",
                    "Use the same hierarchy root selected by the Tools preset."), hierarchySource, typeof(GameObject), true);
                PlayerToolFields.Scalar(serializedObject.FindProperty("Duration"));
            }
            if (PlayerToolFields.Section(ref tracksOpen, "Tracks", "Transform targets and their timed keys."))
                DrawTracks(serializedObject.FindProperty("Tracks"));
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerToolAnimationPreset)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Lists each track by target and draws only the expanded tracks.</summary>
        /// <param name="tracks">Animation track array.</param>
        private void DrawTracks(SerializedProperty tracks)
        {
            using EditorGUI.IndentLevelScope listIndent = new EditorGUI.IndentLevelScope();
            Transform root = hierarchySource != null ? hierarchySource.transform : null;
            bool allowRoot = hierarchySource != null && hierarchySource.GetComponent<PlayerHost>() == null;
            // Each track resolves only once at runtime; no manual path field is exposed here.
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                SerializedProperty path = track.FindPropertyRelative("Path");
                string route = path.stringValue;
                string targetName = route.Length == 0 ? "Hierarchy Root" : route.Substring(route.LastIndexOf('/') + 1);
                if (StudioArrayGUI.Header(tracks, index, new GUIContent($"{index + 1}. {targetName}", route)))
                    break;
                if (!track.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope trackIndent = new EditorGUI.IndentLevelScope();
                PlayerHierarchyMenu.DrawPath(path, root, new GUIContent("Target", path.tooltip), allowRoot);
                StudioFields.Draw(track, "Space", "RelativeToStart");
                SerializedProperty keys = track.FindPropertyRelative("Keys");
                keys.isExpanded = StudioArrayGUI.Foldout(keys, new GUIContent($"Keys ({keys.arraySize})", keys.tooltip));
                if (keys.isExpanded)
                    DrawKeys(keys, track, PlayerHierarchy.Resolve(root, path.stringValue));
            }
            StudioArrayGUI.Add(tracks, "Add Track", () => new PlayerToolTrack());
        }

        /// <summary>Shows key times in the list and resolves capture against the current track target.</summary>
        /// <param name="keys">Transform key array.</param>
        /// <param name="track">Track supplying the coordinate space and offset mode.</param>
        /// <param name="animated">Target in the sample hierarchy, if resolved.</param>
        private static void DrawKeys(SerializedProperty keys, SerializedProperty track, Transform animated)
        {
            using EditorGUI.IndentLevelScope listIndent = new EditorGUI.IndentLevelScope();
            // Resolve the hierarchy once per expanded track, even when several keys are open.
            for (int index = 0; index < keys.arraySize; index++)
            {
                SerializedProperty frame = keys.GetArrayElementAtIndex(index);
                SerializedProperty time = frame.FindPropertyRelative("Time");
                if (StudioArrayGUI.Header(keys, index, new GUIContent($"Key {index + 1} · {time.floatValue:0.###} s", keys.tooltip)))
                    break;
                if (!frame.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope keyIndent = new EditorGUI.IndentLevelScope();
                PlayerToolFields.Scalar(time);
                StudioFields.Draw(frame, "RotationDirection");
                SerializedProperty pose = frame.FindPropertyRelative("Pose");
                PlayerToolFields.Pose(pose);
                using (new EditorGUI.DisabledScope(animated == null))
                    if (StudioButton.Draw(new GUIContent("Read Current Pose", "Capture this target in the track's coordinate space. Relative tracks capture an identity offset.")))
                        pose.boxedValue = track.FindPropertyRelative("RelativeToStart").boolValue
                            ? PlayerToolPose.Identity : PlayerToolPose.Read(animated, (PlayerToolSpace)track.FindPropertyRelative("Space").enumValueIndex);
            }
            StudioArrayGUI.Add(keys, "Add Key", () => new PlayerToolKey());
        }

        #endregion

        #endregion
    }
}
