using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains foldout visibility independently of pending preset values.</summary>
    [Serializable]
    internal sealed class ObjectStudioSections
    {
        #region Fields

        [Header("Menus")]
        [Tooltip("Collapsed sections restored when the workspace reopens.")]
        [SerializeField]
        private List<string> closed = new List<string>();

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Draws a titled dropdown without treating its visibility as a configuration edit.</summary>
        /// <param name="title">Stable menu title.</param>
        /// <param name="tooltip">Purpose of this group.</param>
        /// <param name="parent">Serialized settings represented by the section, when applicable.</param>
        /// <param name="fields">Relative fields included in section transfers; empty selects the complete parent.</param>
        /// <returns>True when the group's controls should be drawn.</returns>
        internal bool Draw(string title, string tooltip, SerializedProperty parent = null, params string[] fields)
        {
            // Folding is workspace navigation, so it does not enable Apply.
            bool changed = GUI.changed;
            Rect rect = EditorGUILayout.GetControlRect();
            string key = "ObjectsLogicStudio.Section." + title;
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                StudioFieldGroup.Context(rect, parent == null ? Array.Empty<SerializedProperty>() : fields.Length == 0
                    ? new[] { parent } : Array.ConvertAll(fields, parent.FindPropertyRelative), key);
            using StudioFieldColors colors = new StudioFieldColors(rect, key);
            bool open = EditorGUI.Foldout(rect, !closed.Contains(title), new GUIContent(title, tooltip), true, EditorStyles.foldoutHeader);
            GUI.changed = changed;
            if (open)
                closed.Remove(title);
            else if (!closed.Contains(title))
                closed.Add(title);
            return open;
        }

        #endregion

        #endregion
    }
}
