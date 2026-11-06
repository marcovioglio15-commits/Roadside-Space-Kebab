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
            HoverControls.Field(settings, "Enabled", "Animate Scoop");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(settings, "Mode", "Animation Mode");
            bool relative = settings.FindPropertyRelative("Mode").enumValueIndex == (int)ScoopAnimationMode.RelativeChain;
            SerializedProperty frames = settings.FindPropertyRelative("Keyframes");
            SerializedProperty approach = settings.FindPropertyRelative("Approach");
            bool resize = relative && approach.FindPropertyRelative("Resize").boolValue;
            if (relative)
            {
                approach.isExpanded = EditorGUILayout.Foldout(approach.isExpanded, new GUIContent("Dispenser Approach", approach.tooltip), true);
                if (approach.isExpanded)
                    using (new StudioIndentScope())
                        Frame(approach, false, false);
            }
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
                Frame(frame, !relative, true);
            }
            StudioArrayGUI.Add(frames, "Add Keyframe", () => new ScoopKeyframe { Id = Guid.NewGuid().ToString("N"), Name = "Keyframe " + (frames.arraySize + 1) });
            if (resize)
                HierarchyPathMenu.Draw(settings.FindPropertyRelative("VisualPath"), HierarchyPathMenu.Source(settings), false, transforms: true);
            FillMarker(settings.FindPropertyRelative("FillAfter"), frames, relative ? approach : null);
            HoverControls.Field(settings, "ReturnToStart", "Animate Return to Grab");
            if (settings.FindPropertyRelative("ReturnToStart").boolValue)
                HoverControls.Field(settings, "ReturnDuration");
            HoverControls.Field(settings, "ObstacleMargin");
        }

        /// <summary>Groups each target's position, rotation, travel and scale without duplicating approach controls.</summary>
        /// <param name="frame">Target receiving pose and timing edits.</param>
        /// <param name="reference">Whether independent pose references are available.</param>
        /// <param name="named">Whether this is a named optional keyframe.</param>
        private static void Frame(SerializedProperty frame, bool reference, bool named)
        {
            // The automatic approach has a fixed role; optional targets retain their names.
            if (named)
                HoverControls.Field(frame, "Name");
            if (Group(frame.FindPropertyRelative("Position"), "Position Settings"))
                using (new StudioIndentScope())
                {
                    if (reference)
                        HoverControls.Field(frame, "Space", "Position Reference");
                    ObjectNumericControls.Vector(frame.FindPropertyRelative("Position"), caption: reference ? "Position Offset" : named ? "Previous Target Offset" : "Pivot Offset");
                }
            if (Group(frame.FindPropertyRelative("Rotation"), "Rotation Settings"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(frame, "Rotate");
                    if (frame.FindPropertyRelative("Rotate").boolValue)
                        ObjectNumericControls.Vector(frame.FindPropertyRelative("Rotation"), -180f, 180f, "Rotation Offset");
                }
            if (Group(frame.FindPropertyRelative("Duration"), "Travel Settings"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(frame, "Duration");
                    HoverControls.Field(frame, "Easing");
                }
            if (Group(frame.FindPropertyRelative("Scale"), "Scale Settings"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(frame, "Resize");
                    if (frame.FindPropertyRelative("Resize").boolValue)
                        ObjectNumericControls.Vector(frame.FindPropertyRelative("Scale"), 0.01f, 5f);
                }
        }

        /// <summary>Keeps each pose subsection's expansion state on its serialized property.</summary>
        /// <param name="property">Property retaining the foldout state.</param>
        /// <param name="label">Specific subsection caption.</param>
        /// <returns>True while this subsection is open.</returns>
        private static bool Group(SerializedProperty property, string label)
        {
            // Expansion belongs to the inspector and does not modify the saved animation.
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, new GUIContent(label, property.tooltip), true);
            return property.isExpanded;
        }

        /// <summary>Selects the fill boundary by keyframe name without exposing internal IDs.</summary>
        /// <param name="marker">Stable key receiving the selected boundary.</param>
        /// <param name="frames">Current keyframe sequence.</param>
        /// <param name="approach">Automatic first target, or null for independent poses.</param>
        private static void FillMarker(SerializedProperty marker, SerializedProperty frames, SerializedProperty approach)
        {
            int offset = approach != null ? 2 : 1;
            string[] labels = new string[frames.arraySize + offset];
            labels[0] = "End of sequence";
            if (approach != null)
                labels[1] = "Dispenser Approach";
            int selected = approach != null && marker.stringValue == approach.FindPropertyRelative("Id").stringValue ? 1 : 0;
            for (int index = 0; index < frames.arraySize; index++)
            {
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                labels[index + offset] = frame.FindPropertyRelative("Name").stringValue;
                if (frame.FindPropertyRelative("Id").stringValue == marker.stringValue)
                    selected = index + offset;
            }
            EditorGUI.BeginChangeCheck();
            selected = StudioFieldGUI.Popup(new GUIContent("Apply Fill After", marker.tooltip), selected, labels);
            if (EditorGUI.EndChangeCheck())
                marker.stringValue = selected == 0 ? string.Empty : selected < offset ? approach.FindPropertyRelative("Id").stringValue
                    : frames.GetArrayElementAtIndex(selected - offset).FindPropertyRelative("Id").stringValue;
        }

        #endregion

        #endregion
    }
}
