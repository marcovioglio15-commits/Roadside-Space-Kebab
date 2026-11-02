using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits scoop movement with conditional segment fields and a stable fill marker.</summary>
    internal static class ScoopAnimationControls
    {
        #region Methods

        #region Drawing

        /// <summary>Draws independent poses or a dispenser-first relative sequence.</summary>
        /// <param name="settings">Serialized scoop animation proposal.</param>
        internal static void Draw(SerializedProperty settings)
        {
            HoverControls.Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(settings, "Mode");
            SerializedProperty frames = settings.FindPropertyRelative("Keyframes");
            bool resize = false;
            // Each frame owns one duration measured from its preceding target.
            for (int index = 0; index < frames.arraySize; index++)
            {
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                resize |= frame.FindPropertyRelative("Resize").boolValue;
                string name = frame.FindPropertyRelative("Name").stringValue;
                if (StudioArrayGUI.Header(frames, index, new GUIContent(name, frames.tooltip)))
                    break;
                if (!frame.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                HoverControls.Field(frame, "Name");
                if (settings.FindPropertyRelative("Mode").enumValueIndex == (int)ScoopAnimationMode.ReferencePoses)
                    HoverControls.Field(frame, "Space");
                else
                    EditorGUILayout.LabelField(index == 0 ? "Offset from dispenser pivot" : "Offset from previous keyframe", EditorStyles.miniLabel);
                ObjectNumericControls.Vector(frame.FindPropertyRelative("Position"));
                HoverControls.Field(frame, "Rotate");
                if (frame.FindPropertyRelative("Rotate").boolValue)
                    ObjectNumericControls.Vector(frame.FindPropertyRelative("Rotation"), -180f, 180f);
                HoverControls.Field(frame, "Duration");
                HoverControls.Field(frame, "Easing");
                HoverControls.Field(frame, "Resize");
                if (frame.FindPropertyRelative("Resize").boolValue)
                    ObjectNumericControls.Vector(frame.FindPropertyRelative("Scale"), 0.01f, 5f);
            }
            StudioArrayGUI.Add(frames, "Add Keyframe", () => new ScoopKeyframe { Id = Guid.NewGuid().ToString("N"), Name = "Keyframe " + (frames.arraySize + 1) });
            if (resize)
                HierarchyPathMenu.Draw(settings.FindPropertyRelative("VisualPath"), HierarchyPathMenu.Source(settings), false, transforms: true);
            FillMarker(settings.FindPropertyRelative("FillAfter"), frames);
            HoverControls.Field(settings, "ReturnToStart");
            if (settings.FindPropertyRelative("ReturnToStart").boolValue)
                HoverControls.Field(settings, "ReturnDuration");
            HoverControls.Field(settings, "ObstacleMargin");
        }

        /// <summary>Selects the fill boundary by keyframe name without exposing internal IDs.</summary>
        /// <param name="marker">Stable key receiving the selected boundary.</param>
        /// <param name="frames">Current keyframe sequence.</param>
        private static void FillMarker(SerializedProperty marker, SerializedProperty frames)
        {
            string[] labels = new string[frames.arraySize + 1];
            labels[0] = "End of sequence";
            int selected = 0;
            for (int index = 0; index < frames.arraySize; index++)
            {
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                labels[index + 1] = frame.FindPropertyRelative("Name").stringValue;
                if (frame.FindPropertyRelative("Id").stringValue == marker.stringValue)
                    selected = index + 1;
            }
            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Popup(new GUIContent("Apply Fill After", marker.tooltip), selected, labels);
            if (EditorGUI.EndChangeCheck())
                marker.stringValue = selected == 0 ? string.Empty : frames.GetArrayElementAtIndex(selected - 1).FindPropertyRelative("Id").stringValue;
        }

        #endregion

        #endregion
    }
}
