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
            Reference(property, typeof(PlayerToolAnimationPreset), label);
            if (property.objectReferenceValue is PlayerToolAnimationPreset animation
                && StudioButton.Draw(new GUIContent("Edit Animation", "Edit this preset's tracks and keyframes in its own Apply/Discard window.")))
                PlayerToolAnimationWindow.Open(animation, root);
        }

        #endregion

        #region Sections and Fields

        /// <summary>Draws a clickable heading with the same treatment as the track and key headers.</summary>
        /// <param name="expanded">Expansion state retained by the owning Inspector.</param>
        /// <param name="label">Section caption.</param>
        /// <param name="tooltip">Brief description of the grouped fields.</param>
        /// <returns>True when the section body should be drawn.</returns>
        internal static bool Section(ref bool expanded, string label, string tooltip)
        {
            // The complete heading toggles the section, including its caption.
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            StudioFoldoutGUI.Background(rect, EditorGUI.indentLevel == 0);
            return expanded = EditorGUI.Foldout(rect, expanded, new GUIContent(label, tooltip), true);
        }

        /// <summary>Draws a toggle inside a foldout without repeating the field's Inspector header.</summary>
        /// <param name="property">Serialized boolean field.</param>
        /// <returns>Whether the option is enabled.</returns>
        internal static bool Toggle(SerializedProperty property)
        {
            // Preserve the serialized context menu on the custom row.
            bool enabled = StudioFieldGUI.Toggle(Label(property), property.boolValue);
            if (enabled != property.boolValue)
                property.boolValue = enabled;
            return enabled;
        }

        /// <summary>Draws an asset reference as one row inside its existing section.</summary>
        /// <param name="property">Serialized reference.</param>
        /// <param name="type">Accepted Unity asset type.</param>
        /// <param name="label">Optional row caption.</param>
        internal static void Reference(SerializedProperty property, System.Type type, GUIContent label = null)
        {
            // Delayed Copy and Paste still address the same serialized reference.
            Object asset = StudioFieldGUI.ObjectField(StudioFieldMenu.Value(property,
                label ?? new GUIContent(property.displayName, property.tooltip)), property.objectReferenceValue, type, false);
            if (asset != property.objectReferenceValue)
                property.objectReferenceValue = asset;
        }

        /// <summary>Draws a bounded float without repeating its section header.</summary>
        /// <param name="property">Serialized float with a declared slider range.</param>
        internal static void Scalar(SerializedProperty property)
        {
            // The bound widget reads its range directly from the field declaration.
            float value = StudioFieldGUI.FloatField(Label(property), property.floatValue);
            if (!value.Equals(property.floatValue))
                property.floatValue = value;
        }

        /// <summary>Associates a custom row with its serialized field menu and tooltip.</summary>
        /// <param name="property">Field shown in the next Studio widget.</param>
        /// <returns>The display label for the next widget.</returns>
        internal static GUIContent Label(SerializedProperty property)
        {
            return StudioFieldMenu.Value(property, new GUIContent(property.displayName, property.tooltip));
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
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
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
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
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
