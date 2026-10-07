using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws native Studio widgets with the same field menu on both labels and value areas.</summary>
    public static class StudioFieldGUI
    {
        #region Methods
        #region Scalars

        /// <summary>Draws a native Toggle with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static bool Toggle(GUIContent label, bool value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.Toggle(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native IntField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static int IntField(GUIContent label, int value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.IntField(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native FloatField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static float FloatField(GUIContent label, float value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.FloatField(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native TextField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static string TextField(GUIContent label, string value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.TextField(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native Vector2Field with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Vector2 Vector2Field(GUIContent label, Vector2 value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.wideMode ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2f, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.Vector2Field(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native Vector3Field with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Vector3 Vector3Field(GUIContent label, Vector3 value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.wideMode ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2f, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.Vector3Field(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native ColorField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Color ColorField(GUIContent label, Color value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.ColorField(rect, label, StudioValueMenu.Edit(rect, label, value));
        }

        /// <summary>Draws a native EnumPopup with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Enum EnumPopup(GUIContent label, Enum value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.EnumPopup(rect, label, StudioValueMenu.Edit(rect, label, value, result => result != null && result.GetType() == value.GetType()));
        }

        /// <summary>Draws a native EnumFlagsField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Enum EnumFlagsField(GUIContent label, Enum value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.EnumFlagsField(rect, label, StudioValueMenu.Edit(rect, label, value, result => result != null && result.GetType() == value.GetType()));
        }

        /// <summary>Draws a multiline text field with whole-field Copy/Paste.</summary>
        /// <param name="value">Current text.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The edited text.</returns>
        public static string TextArea(string value, params GUILayoutOption[] options)
        {
            // Reserve height with the same text-area style used to render the field.
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(value), EditorStyles.textArea, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, string.Empty));
            return EditorGUI.TextArea(rect, StudioValueMenu.Edit(rect, GUIContent.none, value));
        }

        /// <summary>Draws an asset or scene reference with type-checked Paste.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current reference.</param>
        /// <param name="type">Accepted Unity object type.</param>
        /// <param name="sceneObjects">Whether scene references are allowed.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The selected or pasted compatible reference.</returns>
        public static UnityEngine.Object ObjectField(GUIContent label, UnityEngine.Object value, Type type, bool sceneObjects, params GUILayoutOption[] options)
        {
            // Object pickers keep Unity's drag/drop and scene-reference restrictions.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.ObjectField(rect, label, StudioValueMenu.Edit(rect, label, value,
                result => result == null || type.IsInstanceOfType(result) && (sceneObjects || EditorUtility.IsPersistent(result))), type, sceneObjects);
        }

        /// <summary>Draws a bounded scalar with a native slider.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current scalar.</param>
        /// <param name="minimum">Lower control bound.</param>
        /// <param name="maximum">Upper control bound.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The edited scalar.</returns>
        public static float Slider(GUIContent label, float value, float minimum, float maximum, params GUILayoutOption[] options)
        {
            // Reject an out-of-range paste instead of silently changing its value.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.Slider(rect, label, StudioValueMenu.Edit(rect, label, value,
                result => result >= minimum && result <= maximum), minimum, maximum);
        }

        /// <summary>Draws named choices with range-checked Copy/Paste.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="selected">Current choice index.</param>
        /// <param name="choices">Native menu captions.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The selected choice index.</returns>
        public static int Popup(GUIContent label, int selected, GUIContent[] choices, params GUILayoutOption[] options)
        {
            // Serialized enums use their actual enum type through StudioFieldMenu.Next.
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            return EditorGUI.Popup(rect, label, StudioValueMenu.Edit(rect, label, selected,
                result => result >= 0 && result < choices.Length), choices);
        }

        /// <summary>Draws string-captioned choices through the same native popup.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="selected">Current choice index.</param>
        /// <param name="choices">Native menu captions.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The selected choice index.</returns>
        public static int Popup(GUIContent label, int selected, string[] choices, params GUILayoutOption[] options)
        {
            // GUIContent conversion preserves the existing slash-separated submenu labels.
            return Popup(label, selected, Array.ConvertAll(choices, caption => new GUIContent(caption)), options);
        }

        #endregion
        #region Serialized Fields

        /// <summary>Draws a serialized field while exposing the context menu over its complete row.</summary>
        /// <param name="property">Field to display.</param>
        /// <param name="includeChildren">Whether expanded child fields should be drawn.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>Unity's foldout result.</returns>
        public static bool PropertyField(SerializedProperty property, bool includeChildren = false, params GUILayoutOption[] options)
        {
            // Native labels retain their tooltips and registered property drawers.
            return PropertyField(property, new GUIContent(property.displayName, property.tooltip), includeChildren, options);
        }

        /// <summary>Draws a serialized field with a custom caption.</summary>
        /// <param name="property">Field to display.</param>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="includeChildren">Whether expanded child fields should be drawn.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>Unity's foldout result.</returns>
        public static bool PropertyField(SerializedProperty property, GUIContent label, bool includeChildren = false, params GUILayoutOption[] options)
        {
            // Unity remains responsible for custom drawers and validation of native field types.
            StudioFieldMenu.TakeNext();
            if (includeChildren && property.isArray && property.propertyType == SerializedPropertyType.Generic)
                return StudioArrayGUI.Draw(property, label);
            if (includeChildren && property.propertyType == SerializedPropertyType.Generic)
                return StudioStructureGUI.Draw(property, label);
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, label, includeChildren), options);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                using SerializedProperty clicked = Row(property, rect, includeChildren);
                StudioFieldMenu.Context(rect, clicked);
            }
            return EditorGUI.PropertyField(rect, property, label, includeChildren);
        }

        /// <summary>Finds the visible child row underneath a click on an expanded native property.</summary>
        /// <param name="property">Top-level field rendered by Unity.</param>
        /// <param name="rect">Its complete layout rectangle.</param>
        /// <param name="children">Whether children were requested by the caller.</param>
        /// <returns>A disposable copy of the clicked field.</returns>
        private static SerializedProperty Row(SerializedProperty property, Rect rect, bool children)
        {
            // Single-row custom drawers own their whole field, including custom flag arrays.
            float offset = Event.current.mousePosition.y - rect.y;
            if (!children || !property.isExpanded || !property.hasVisibleChildren || offset < EditorGUIUtility.singleLineHeight)
                return property.Copy();
            offset -= EditorGUI.GetPropertyHeight(property, false) + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty child = property.Copy();
            using SerializedProperty end = property.GetEndProperty();
            if (child.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end))
                        break;
                    float height = EditorGUI.GetPropertyHeight(child, false) + EditorGUIUtility.standardVerticalSpacing;
                    if (offset < height)
                        return child;
                    offset -= height;
                }
                while (child.NextVisible(child.isExpanded));
            child.Dispose();
            return property.Copy();
        }

        #endregion
        #endregion
    }
}
