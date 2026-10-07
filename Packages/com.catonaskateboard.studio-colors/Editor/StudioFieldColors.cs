using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Applies persistent field and section colors without modifying Unity's shared GUI styles.</summary>
    public readonly struct StudioFieldColors : IDisposable
    {
        #region State

        private readonly Color content;
        private readonly Color background;
        private static readonly Dictionary<string, string> keys = new Dictionary<string, string>();

        #endregion
        #region Methods
        #region Presentation

        /// <summary>Applies one configured field tint for the duration of its native drawing call.</summary>
        /// <param name="rect">Visible field or menu rectangle.</param>
        /// <param name="key">Stable field or section identity.</param>
        public StudioFieldColors(Rect rect, string key)
        {
            content = GUI.contentColor;
            background = GUI.backgroundColor;
            ElementRule rule = ColorSettings.instance.FindElement(key);
            if (rule == null)
                return;
            if (rule.OverrideText)
                GUI.contentColor = rule.Text;
            if (rule.OverrideBackground)
            {
                GUI.backgroundColor = rule.Background;
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, rule.Background);
            }
        }

        /// <summary>Restores colors before another field is drawn.</summary>
        public void Dispose()
        {
            GUI.contentColor = content;
            GUI.backgroundColor = background;
        }

        /// <summary>Identifies a field consistently across objects and array element positions.</summary>
        /// <param name="property">Optional serialized field.</param>
        /// <param name="label">Fallback caption for a transient control.</param>
        /// <returns>A persistent Studio Colors element key.</returns>
        public static string Key(SerializedProperty property, string label = "")
        {
            // Array reordering must not move a field's chosen color to an unrelated setting.
            if (property == null)
                return "Studio.Field." + (EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().Name : "Inspector") + "." + label;
            string path = property.serializedObject.targetObject.GetType().FullName + "." + property.propertyPath;
            if (!keys.TryGetValue(path, out string key))
            {
                key = Regex.Replace(path, @"\.Array\.data\[\d+\]", ".Element");
                keys.Add(path, key);
            }
            return key;
        }

        /// <summary>Adds color editing to the same menu used for field transfers.</summary>
        /// <param name="menu">Context menu receiving the commands.</param>
        /// <param name="key">Stable identity of the clicked control.</param>
        public static void Menu(GenericMenu menu, string key)
        {
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Edit Color", "Edit text and background colors for this field or section."), false,
                () => StudioColorsWindow.EditElement(key));
            menu.AddItem(new GUIContent("Reset Color", "Restore the field's original colors."), false,
                () => ColorSettings.instance.RemoveElement(key));
        }

        #endregion
        #endregion
    }
}
