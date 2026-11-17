using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Places one removal button per header and insertion buttons in the gaps between cells.</summary>
    internal static class ExcelGridButtons
    {
        #region Fields

        private const float size = 16f;
        private static GUIStyle style;

        #endregion
        #region Methods
        #region Grid Controls

        /// <summary>Draws column and row controls without running edits during cell iteration.</summary>
        /// <param name="preset">Current grid dimensions.</param>
        /// <param name="geometry">Shared cell measurements.</param>
        /// <param name="firstRow">First visible row.</param>
        /// <param name="lastRow">Last visible row.</param>
        /// <param name="firstColumn">First visible column.</param>
        /// <param name="lastColumn">Last visible column.</param>
        /// <param name="row">Receives the edited axis.</param>
        /// <param name="insert">Receives insertion or removal intent.</param>
        /// <param name="position">Receives the selected coordinate, or zero without a click.</param>
        internal static void Draw(ExcelExchangePreset preset, ExcelGridGeometry geometry, int firstRow, int lastRow, int firstColumn, int lastColumn,
            out bool row, out bool insert, out int position)
        {
            // Return one pending edit to apply after the grid finishes drawing.
            row = false;
            insert = false;
            position = 0;
            style ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 0f, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
            // Reserve column headers for one removal button and each insertion boundary.
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                float center = geometry.Cell(1, column).center.x;
                string name = ExcelAddress.Format(1, column).TrimEnd('1');
                GUI.Label(new Rect(center - 24f, 0f, 48f, 18f), name, EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(preset.Columns <= 1))
                    if (Button(center, 28f, "−", "Remove column " + name + ". Undo restores it."))
                        position = column;
                if (Button(geometry.Boundary(false, column), 28f, "+", "Insert a column here."))
                {
                    position = column;
                    insert = true;
                }
            }
            if (Button(geometry.Boundary(false, lastColumn + 1), 28f, "+", "Insert a column here."))
            {
                position = lastColumn + 1;
                insert = true;
            }
            // Keep row controls in their own gutter, away from cell contents.
            for (int current = firstRow; current <= lastRow; current++)
            {
                float center = geometry.Cell(current, 1).center.y;
                GUI.Label(new Rect(27f, center - 9f, 25f, 18f), current.ToString(), EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(preset.Rows <= 1))
                    if (Button(12f, center, "−", "Remove row " + current + ". Undo restores it."))
                    {
                        row = true;
                        insert = false;
                        position = current;
                    }
                if (Button(12f, geometry.Boundary(true, current), "+", "Insert a row here."))
                {
                    row = true;
                    insert = true;
                    position = current;
                }
            }
            if (Button(12f, geometry.Boundary(true, lastRow + 1), "+", "Insert a row here."))
            {
                row = true;
                insert = true;
                position = lastRow + 1;
            }
        }

        #endregion
        #region Buttons

        /// <summary>Centers a compact button inside its reserved area.</summary>
        /// <param name="x">Horizontal center.</param>
        /// <param name="y">Vertical center.</param>
        /// <param name="text">Visible symbol.</param>
        /// <param name="tooltip">Short action description.</param>
        /// <returns>True when the button is clicked.</returns>
        private static bool Button(float x, float y, string text, string tooltip)
        {
            // A shared size keeps all controls inside the dedicated gaps.
            return GUI.Button(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), new GUIContent(text, tooltip), style);
        }

        #endregion
        #endregion
    }
}
