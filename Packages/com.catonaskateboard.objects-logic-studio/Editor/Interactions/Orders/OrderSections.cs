using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps catalog sections independent for each order and variant without modifying their settings.</summary>
    internal static class OrderSections
    {
        #region Methods
        #region Drawing

        /// <summary>Uses serialized expansion state and transfers exactly the fields represented by the heading.</summary>
        /// <param name="owner">Order, variant or paragraph configuration.</param>
        /// <param name="title">Section caption.</param>
        /// <param name="tooltip">Purpose of the grouped controls.</param>
        /// <param name="fields">Relative names included in this section.</param>
        /// <returns>True when its controls should be drawn.</returns>
        internal static bool Draw(SerializedProperty owner, string title, string tooltip, params string[] fields)
        {
            SerializedProperty state = owner.FindPropertyRelative(fields[0]);
            if (ObjectFieldFocus.ExpandSection(owner, fields))
                state.isExpanded = true;
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            string key = StudioFieldColors.Key(state) + ".Section";
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                StudioFieldGroup.Context(rect, Array.ConvertAll(fields, owner.FindPropertyRelative), key);
            bool changed = GUI.changed;
            StudioFoldoutGUI.Background(rect);
            using StudioFieldColors colors = new StudioFieldColors(rect, key);
            state.isExpanded = EditorGUI.Foldout(rect, state.isExpanded, new GUIContent(title, tooltip), true);
            GUI.changed = changed;
            return state.isExpanded;
        }

        #endregion
        #endregion
    }
}
