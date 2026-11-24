using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares nested keyframe controls between travel paths and local character tracks.</summary>
    internal static class SpawnFlowKeyframeControls
    {
        #region Methods
        #region Sections

        /// <summary>Draws a themed foldout without changing its serialized values.</summary>
        /// <param name="property">Settings retaining the expansion state.</param>
        /// <param name="label">Section caption.</param>
        /// <returns>True when its controls should be displayed.</returns>
        internal static bool Section(SerializedProperty property, string label)
        {
            // Expansion changes visibility without modifying animation settings.
            property.isExpanded = StudioArrayGUI.Foldout(property, new GUIContent(label, property.tooltip));
            return property.isExpanded;
        }

        /// <summary>Shows an optional travel path with its own nested keyframes.</summary>
        /// <param name="animation">Walk-in or walk-out settings.</param>
        /// <param name="label">Section caption.</param>
        internal static void Path(SerializedProperty animation, string label)
        {
            // Disabled paths retain their saved destinations without showing unused controls.
            if (!Section(animation, label))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(animation, "Enabled");
            if (!animation.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(animation, "Space", "Key Space");
            Frames(animation, false);
        }

        #endregion
        #region Keyframes

        /// <summary>Edits destinations with axis sliders and shared delay and interpolation controls.</summary>
        /// <param name="animation">Path supplying interpolation and keyframes.</param>
        /// <param name="offsets">Whether poses represent additive local character offsets.</param>
        private static void Frames(SerializedProperty animation, bool offsets)
        {
            // Travel and character tracks use the same timing and destination controls.
            HoverControls.Field(animation, "Interpolation");
            SerializedProperty frames = animation.FindPropertyRelative("Keyframes");
            // Structural changes stop drawing before stale serialized handles are reused.
            for (int index = 0; index < frames.arraySize; index++)
            {
                if (StudioArrayGUI.Header(frames, index, new GUIContent("Keyframe " + (index + 1), frames.tooltip)))
                    break;
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                if (!frame.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                SerializedProperty pose = frame.FindPropertyRelative("Pose");
                StudioVectorControls.Draw(pose.FindPropertyRelative("Position"), offsets ? -2f : -20f, offsets ? 2f : 20f,
                    offsets ? "Position Offset" : "Position");
                StudioVectorControls.Draw(pose.FindPropertyRelative("Rotation"), -360f, 360f, offsets ? "Rotation Offset" : "Rotation");
                StudioVectorControls.Draw(pose.FindPropertyRelative("Scale"), 0.01f, 3f, offsets ? "Scale Multiplier" : "Scale");
                HoverControls.Field(frame, "Delay");
                HoverControls.Field(frame, "Duration");
                HoverControls.Field(frame, "Direction");
            }
            StudioArrayGUI.Add(frames, "Add Keyframe", () => new SpawnFlowKeyframe());
        }

        /// <summary>Draws independent root or child tracks for one character animation.</summary>
        /// <param name="role">Animation role using transform keyframes.</param>
        /// <param name="prefab">Visitor hierarchy supplying track targets.</param>
        /// <param name="looping">Whether independent loop timing is relevant for this role.</param>
        internal static void Tracks(SerializedProperty role, GameObject prefab, bool looping)
        {
            SerializedProperty tracks = role.FindPropertyRelative("Tracks");
            // Each track owns its own destinations while the role shares one playback cycle.
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                Transform selected = track.FindPropertyRelative("Target").objectReferenceValue as Transform;
                if (StudioArrayGUI.Header(tracks, index,
                    new GUIContent("Track " + (index + 1) + " · " + (selected != null ? selected.name : "Root"), tracks.tooltip)))
                    break;
                if (!track.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                Target(track.FindPropertyRelative("Target"), prefab);
                if (looping)
                    HoverControls.Field(track, "IndependentLoop");
                Frames(track.FindPropertyRelative("Animation"), true);
            }
            StudioArrayGUI.Add(tracks, "Add Transform Track", () => new SpawnFlowTransformTrack());
        }

        #endregion
        #region Targets

        /// <summary>Offers only this prefab's transforms and retains exact references for duplicate names.</summary>
        /// <param name="property">Optional prefab transform; null selects the root.</param>
        /// <param name="prefab">Hierarchy supplying the menu.</param>
        private static void Target(SerializedProperty property, GameObject prefab)
        {
            // Exact component references distinguish siblings sharing the same name.
            Transform current = property.objectReferenceValue as Transform;
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent("Target", property.tooltip));
            Rect rect = field.ValueRect(field.GetRect(EditorGUIUtility.singleLineHeight));
            if (!GUI.Button(rect, current != null ? current.name : "Root", EditorStyles.popup))
                return;
            SerializedObject data = property.serializedObject;
            string path = property.propertyPath;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Root"), current == null || prefab != null && current == prefab.transform,
                () => Select(data, path, null));
            // Hierarchy discovery happens only when the selector opens.
            if (prefab != null)
            {
                Transform[] candidates = prefab.GetComponentsInChildren<Transform>(true);
                for (int index = 1; index < candidates.Length; index++)
                {
                    Transform selected = candidates[index];
                    string label = AnimationUtility.CalculateTransformPath(selected, prefab.transform);
                    menu.AddItem(new GUIContent("Children/" + label + " [" + index + "]"), current == selected,
                        () => Select(data, path, selected));
                }
            }
            menu.DropDown(rect);
        }

        /// <summary>Commits an explicit hierarchy selection with serialized Undo support.</summary>
        /// <param name="data">Shared plan proposal.</param>
        /// <param name="path">Exact target property path.</param>
        /// <param name="target">Prefab child or null for the root.</param>
        private static void Select(SerializedObject data, string path, Transform target)
        {
            // Resolve the property again after the menu closes.
            data.Update();
            data.FindProperty(path).objectReferenceValue = target;
            data.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
