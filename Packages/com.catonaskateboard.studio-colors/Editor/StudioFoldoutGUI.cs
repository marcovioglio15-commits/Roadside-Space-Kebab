using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Separates foldout headings with compact shaded bands while retaining custom field colors.</summary>
    public static class StudioFoldoutGUI
    {
        #region Methods

        #region Drawing

        /// <summary>Paints a heading behind its controls, before any optional Studio Colors override.</summary>
        /// <param name="rect">Complete heading row, including native indentation.</param>
        /// <param name="major">Uses a stronger band for a main section.</param>
        public static void Background(Rect rect, bool major = false)
        {
            // Paint only during repaint; layout and input events keep their normal control order.
            if (Event.current.type != EventType.Repaint)
                return;
            rect = EditorGUI.IndentedRect(rect);
            Color background = EditorGUIUtility.isProSkin
                ? major ? new Color(0.17f, 0.20f, 0.23f) : new Color(0.27f, 0.28f, 0.29f)
                : major ? new Color(0.65f, 0.70f, 0.75f) : new Color(0.81f, 0.82f, 0.83f);
            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.3f) : new Color(0f, 0f, 0f, 0.15f));
        }

        #endregion

        #endregion
    }
}
