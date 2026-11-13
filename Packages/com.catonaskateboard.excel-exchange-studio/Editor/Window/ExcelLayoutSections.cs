using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Draws consistent dividers between mapping panels and related settings.</summary>
    internal static class ExcelLayoutSections
    {
        #region Methods

        /// <summary>Separates neighboring panels with a vertical rule.</summary>
        internal static void Vertical()
        {
            Rect rect = GUILayoutUtility.GetRect(7f, 7f, 1f, 10000f, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y, 1f, rect.height), new Color(0.4f, 0.4f, 0.4f, 0.65f));
        }

        /// <summary>Separates stacked settings without adding a help box.</summary>
        internal static void Horizontal()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 9f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4f, rect.width, 1f), new Color(0.4f, 0.4f, 0.4f, 0.65f));
        }

        #endregion
    }
}
