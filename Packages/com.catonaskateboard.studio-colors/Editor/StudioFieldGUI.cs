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
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.Toggle(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Draws a native IntField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static int IntField(GUIContent label, int value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.IntField(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Edits an index through a bounded slider without changing saved data on repaint.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current configured index.</param>
        /// <param name="minimum">First selectable index.</param>
        /// <param name="maximum">Last selectable index.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The index selected or pasted by the user.</returns>
        public static int IntField(GUIContent label, int value, int minimum, int maximum, params GUILayoutOption[] options)
        {
            // Keep the shared context menu and leave validation to the owning settings.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            value = StudioValueMenu.Edit(row, label, value);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.IntSlider(rect, layout.InlineLabel, value, minimum, maximum);
            return EditorGUI.EndChangeCheck() ? selected : value;
        }

        /// <summary>Draws a native FloatField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static float FloatField(GUIContent label, float value, params GUILayoutOption[] options)
        {
            return FloatField(label, value, StudioPropertySchema.Range(StudioFieldMenu.NextProperty), options);
        }

        /// <summary>Shares the settings declaration's range with a transient draft control.</summary>
        /// <typeparam name="T">Settings class declaring the field.</typeparam>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current draft value.</param>
        /// <param name="field">Declared field name or nested path.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The edited value without changing out-of-range data on repaint.</returns>
        public static float FloatField<T>(GUIContent label, float value, string field, params GUILayoutOption[] options)
        {
            return FloatField(label, value, StudioPropertySchema.Range(typeof(T), field), options);
        }

        /// <summary>Keeps context menus and field colours identical for bound and transient float controls.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="range">Cached declared interval, or null for unbounded fields.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The edited scalar.</returns>
        private static float FloatField(GUIContent label, float value, RangeAttribute range, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return StudioFloatGUI.Draw(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value), range);
        }

        /// <summary>Draws a transient scalar with visible recommended endpoints and precise numeric entry.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current configured value.</param>
        /// <param name="minimum">Suggested lower endpoint.</param>
        /// <param name="maximum">Suggested upper endpoint.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The edited scalar without clamping existing data on repaint.</returns>
        public static float FloatField(GUIContent label, float value, float minimum, float maximum, params GUILayoutOption[] options)
        {
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return StudioFloatGUI.Draw(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value), minimum, maximum);
        }

        /// <summary>Draws a native TextField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static string TextField(GUIContent label, string value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.TextField(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Draws a native Vector2Field with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Vector2 Vector2Field(GUIContent label, Vector2 value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.wideMode ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2f, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.Vector2Field(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Draws a native Vector3Field with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Vector3 Vector3Field(GUIContent label, Vector3 value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.wideMode ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2f, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.Vector3Field(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Draws a native ColorField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Color ColorField(GUIContent label, Color value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.ColorField(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value));
        }

        /// <summary>Draws a native EnumPopup with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Enum EnumPopup(GUIContent label, Enum value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.EnumPopup(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value, result => result != null && result.GetType() == value.GetType()));
        }

        /// <summary>Draws a native EnumFlagsField with typed Copy/Paste support.</summary>
        /// <param name="label">Field caption and tooltip.</param>
        /// <param name="value">Current value.</param>
        /// <param name="options">Optional layout constraints.</param>
        /// <returns>The value returned through the tool's usual edit transaction.</returns>
        public static Enum EnumFlagsField(GUIContent label, Enum value, params GUILayoutOption[] options)
        {
            // Keep Unity's built-in editing behavior and existing indentation.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.EnumFlagsField(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value, result => result != null && result.GetType() == value.GetType()));
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
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.ObjectField(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value,
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
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            EditorGUI.BeginChangeCheck();
            float edited = StudioFloatGUI.Draw(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, value,
                result => result >= minimum && result <= maximum), minimum, maximum);
            return EditorGUI.EndChangeCheck() ? Mathf.Clamp(edited, minimum, maximum) : value;
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
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight, options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(StudioFieldMenu.NextProperty, label.text));
            Rect rect = layout.Control(row);
            return EditorGUI.Popup(rect, layout.InlineLabel, StudioValueMenu.Edit(row, label, selected,
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

        /// <summary>Shows a read-only field using the same caption layout as editable settings.</summary>
        /// <param name="label">Visible field name.</param>
        /// <param name="value">Value or diagnostic displayed beside the field name.</param>
        /// <param name="style">Optional style for the read-only text.</param>
        public static void ReadOnly(string label, string value, GUIStyle style = null)
        {
            using StudioFieldLayout layout = new StudioFieldLayout(new GUIContent(label));
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(layout.Control(row), layout.InlineLabel, new GUIContent(value), style ?? EditorStyles.label);
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
            if (StudioFieldRenderScope.Draw(property, label))
                return property.isExpanded;
            // Unity remains responsible for custom drawers and validation of native field types.
            StudioFieldMenu.TakeNext();
            if (includeChildren && property.isArray && property.propertyType == SerializedPropertyType.Generic)
                return StudioArrayGUI.Draw(property, label);
            if (includeChildren && property.propertyType == SerializedPropertyType.Generic)
                return StudioStructureGUI.Draw(property, label);
            RangeAttribute range = property.propertyType == SerializedPropertyType.Float ? StudioPropertySchema.Range(property) : null;
            if (range != null)
                StudioFloatGUI.Decorations(property);
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(range != null ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(property, layout.InlineLabel, includeChildren), options);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(property));
            Rect rect = layout.Control(row);
            if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                using SerializedProperty clicked = Row(property, rect, includeChildren);
                StudioFieldMenu.Context(row, clicked);
            }
            if (range != null)
            {
                StudioFloatGUI.Property(rect, property, layout.InlineLabel, range);
                return false;
            }
            return EditorGUI.PropertyField(rect, property, layout.InlineLabel, includeChildren);
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

        /// <summary>Returns a cached numeric interval declared on an editor field.</summary>
        /// <param name="property">Serialized numeric field.</param>
        /// <returns>The declared interval, or null when none is specified.</returns>
        public static RangeAttribute RangeFor(SerializedProperty property)
        {
            return StudioPropertySchema.Range(property);
        }

        #endregion
        #endregion
    }
}
