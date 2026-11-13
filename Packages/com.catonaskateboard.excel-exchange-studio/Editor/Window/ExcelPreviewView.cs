using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Shows changes and diagnostics in one compact scrollable table.</summary>
    internal static class ExcelPreviewView
    {
        #region Fields

        private static GUIStyle errorStyle;
        private static bool changesOnly = true;

        #endregion
        #region Methods

        /// <summary>Draws cell-level before/after values and collection replacement notices.</summary>
        /// <param name="rows">Current preview rows.</param>
        /// <param name="warnings">Existing preset validation warnings.</param>
        /// <param name="scroll">Retained scroll position.</param>
        /// <param name="compare">Whether current and incoming values form an import comparison.</param>
        internal static void Draw(IReadOnlyList<ExcelPreviewRow> rows, IReadOnlyList<string> warnings, ref Vector2 scroll, bool compare = true)
        {
            if (compare)
                changesOnly = EditorGUILayout.ToggleLeft(new GUIContent("Changes Only", "Hide values that already match. Errors and default reset notices remain visible."), changesOnly);
            foreach (string warning in warnings)
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Cell", GUILayout.Width(130f));
                GUILayout.Label("Field", GUILayout.Width(250f));
                GUILayout.Label("Current", GUILayout.MinWidth(100f));
                GUILayout.Label("Incoming", GUILayout.MinWidth(100f));
            }
            using EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            foreach (ExcelPreviewRow row in rows)
            {
                if (compare && changesOnly && !row.Error && row.Before == row.After && row.Message.Length == 0)
                    continue;
                if (row.Error)
                {
                    errorStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = new Color(0.95f, 0.42f, 0.35f) } };
                    EditorGUILayout.LabelField(new GUIContent(row.Cell + "  " + row.Message, row.Field), errorStyle);
                    continue;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(row.Cell, row.Message), GUILayout.Width(130f));
                    GUILayout.Label(new GUIContent(row.Field, row.Field + "\n" + row.Message), GUILayout.Width(250f));
                    GUILayout.Label(new GUIContent(Display(row.Before), row.Before), GUILayout.MinWidth(100f));
                    GUILayout.Label(new GUIContent(Display(row.After), row.After), GUILayout.MinWidth(100f));
                }
                if (row.Message.Length > 0)
                    EditorGUILayout.LabelField(row.Message, EditorStyles.wordWrappedMiniLabel);
            }
        }

        /// <summary>Keeps empty values visible in a comparison table.</summary>
        /// <param name="value">Exact original or incoming value.</param>
        /// <returns>A compact display label.</returns>
        private static string Display(string value)
        {
            return value.Length == 0 ? "(empty)" : value.Replace('\n', ' ');
        }

        #endregion
    }
}
