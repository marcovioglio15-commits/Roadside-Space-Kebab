using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws transform coordinates with per-axis sliders shared by tool, animation and muzzle editors.</summary>
    internal static class PlayerToolFields
    {
        #region Methods

        #region Animations

        /// <summary>Draws an animation reference and opens its own independent Apply/Discard editor.</summary>
        /// <param name="property">Animation asset reference in a tool or arm preset.</param>
        /// <param name="root">Hierarchy against which this clip resolves its tracks.</param>
        /// <param name="label">Optional caption for a reload step.</param>
        internal static void Animation(SerializedProperty property, Transform root, GUIContent label = null)
        {
            // Editing the referenced preset never applies its parent tool or module draft.
            StudioFieldGUI.PropertyField(property, label ?? new GUIContent(property.displayName, property.tooltip));
            if (property.objectReferenceValue is PlayerToolAnimationPreset animation
                && StudioButton.Draw(new GUIContent("Edit Animation", "Edit this preset's tracks and keyframes in its own Apply/Discard window.")))
                PlayerToolAnimationWindow.Open(animation, root);
        }

        #endregion

        #region Coordinates

        /// <summary>Draws a compact pose with independent position, rotation and scale sections.</summary>
        /// <param name="pose">Serialized transform pose.</param>
        /// <param name="label">Optional caption replacing the serialized name.</param>
        internal static void Pose(SerializedProperty pose, string label = null)
        {
            if (!Foldout(pose, label))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            Vector(pose.FindPropertyRelative("Position"), -10f, 10f);
            Vector(pose.FindPropertyRelative("Rotation"), -720f, 720f);
            Vector(pose.FindPropertyRelative("Scale"), 0.01f, 5f);
        }

        /// <summary>Draws three slider-assisted coordinates without normalizing an untouched authored vector.</summary>
        /// <param name="property">Serialized Vector3 field.</param>
        /// <param name="minimum">Suggested per-axis minimum.</param>
        /// <param name="maximum">Suggested per-axis maximum.</param>
        internal static void Vector(SerializedProperty property, float minimum, float maximum)
        {
            if (!Foldout(property))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            // Each axis retains its serialized context menu and the parent field's explanation.
            for (int index = 0; index < 3; index++)
            {
                string axis = index switch { 0 => "x", 1 => "y", _ => "z" };
                SerializedProperty component = property.FindPropertyRelative(axis);
                EditorGUI.BeginChangeCheck();
                float value = StudioFieldGUI.FloatField(StudioFieldMenu.Value(component, new GUIContent(axis.ToUpperInvariant(), property.tooltip)),
                    component.floatValue, minimum, maximum);
                if (EditorGUI.EndChangeCheck())
                    component.floatValue = value;
            }
        }

        /// <summary>Shares foldout captions and whole-value context menus across transform controls.</summary>
        /// <param name="property">Value whose expanded state is retained by Unity.</param>
        /// <param name="label">Optional replacement caption.</param>
        /// <returns>True when child controls should be visible.</returns>
        private static bool Foldout(SerializedProperty property, string label = null)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded,
                new GUIContent(label ?? property.displayName, property.tooltip), true);
            return property.isExpanded;
        }

        #endregion

        #endregion
    }
}
