using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Edits arrays one element at a time without exposing destructive size fields.</summary>
    public static class StudioArrayGUI
    {
        #region Methods
        #region Controls

        /// <summary>Draws a serialized foldout whose complete value participates in context commands.</summary>
        /// <param name="property">Section, struct or array represented by the header.</param>
        /// <param name="label">Visible caption and tooltip.</param>
        /// <returns>The expanded state requested by the control.</returns>
        public static bool Foldout(SerializedProperty property, GUIContent label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            return EditorGUI.Foldout(rect, property.isExpanded, label, true);
        }

        /// <summary>Draws an element header with explicit movement and removal commands.</summary>
        /// <param name="array">Array containing the element.</param>
        /// <param name="index">Current element index.</param>
        /// <param name="label">Element caption and tooltip.</param>
        /// <returns>True when removal or movement invalidated the current drawing pass.</returns>
        public static bool Header(SerializedProperty array, int index, GUIContent label)
        {
            // End the caller's loop after a structural edit so stale properties are never reused.
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, element);
            using (new StudioFieldColors(rect, StudioFieldColors.Key(element)))
                element.isExpanded = EditorGUI.Foldout(rect, element.isExpanded, label, true);
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button(new GUIContent("^", "Move this element up."), GUILayout.Width(24f)))
                {
                    array.MoveArrayElement(index, index - 1);
                    return true;
                }
            using (new EditorGUI.DisabledScope(index + 1 == array.arraySize))
                if (GUILayout.Button(new GUIContent("v", "Move this element down."), GUILayout.Width(24f)))
                {
                    array.MoveArrayElement(index, index + 1);
                    return true;
                }
            if (!GUILayout.Button(new GUIContent("-", "Remove only this element. Undo restores it."), GUILayout.Width(24f)))
                return false;
            int count = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == count)
                array.DeleteArrayElementAtIndex(index);
            return true;
        }

        /// <summary>Appends one initialized element without duplicating the previous row.</summary>
        /// <param name="array">Array receiving the new element.</param>
        /// <param name="label">Add command caption.</param>
        /// <param name="create">Factory for the initial serialized value.</param>
        public static void Add(SerializedProperty array, string label, Func<object> create)
        {
            // New rows start from their declared defaults rather than a copy of existing data.
            if (!StudioButton.Draw(new GUIContent("+ " + label, "Append one element. Existing elements remain intact.")))
                return;
            array.arraySize++;
            SerializedProperty added = array.GetArrayElementAtIndex(array.arraySize - 1);
            if (added.propertyType == SerializedPropertyType.ObjectReference)
                added.objectReferenceValue = null;
            else
                added.boxedValue = create();
            added.isExpanded = true;
        }

        /// <summary>Draws a complete array with native element fields and explicit row operations.</summary>
        /// <param name="array">Serialized array to edit.</param>
        /// <param name="label">Array caption and tooltip.</param>
        /// <returns>Whether the array is expanded.</returns>
        public static bool Draw(SerializedProperty array, GUIContent label)
        {
            // The array header also represents the complete array for Copy, Paste and batch updates.
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, array);
            using (new StudioFieldColors(rect, StudioFieldColors.Key(array)))
                array.isExpanded = EditorGUI.Foldout(rect, array.isExpanded, label, true);
            if (!array.isExpanded)
                return false;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            for (int index = 0; index < array.arraySize; index++)
            {
                if (Header(array, index, new GUIContent("Element " + (index + 1), array.tooltip)))
                    break;
                SerializedProperty element = array.GetArrayElementAtIndex(index);
                if (element.isExpanded)
                    StudioFieldGUI.PropertyField(element, new GUIContent("Value", element.tooltip), true);
            }
            Add(array, "Add Element", () =>
            {
                Type type = StudioPropertySchema.Resolve(array);
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return element == typeof(string) ? string.Empty : Activator.CreateInstance(element);
            });
            return true;
        }

        #endregion
        #endregion
    }
}
