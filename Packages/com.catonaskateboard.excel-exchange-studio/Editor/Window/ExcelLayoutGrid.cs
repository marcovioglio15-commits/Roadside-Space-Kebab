using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Draws only mapping anchors and repeated-column guides on an empty worksheet.</summary>
    internal sealed class ExcelLayoutGrid
    {
        #region Fields

        private Vector2 scroll;
        private GUIStyle cellStyle;
        private readonly Dictionary<(int Row, int Column), int> anchors = new Dictionary<(int Row, int Column), int>();
        private readonly Dictionary<int, List<(int Row, int Index)>> repeats = new Dictionary<int, List<(int Row, int Index)>>();
        private static readonly Color anchorTint = new Color(0.65f, 0.85f, 1f);
        private static readonly Color repeatTint = new Color(0.94f, 0.97f, 1f);
        private static readonly Color selectionTint = new Color(0.3f, 0.7f, 1f);

        #endregion
        #region Methods
        #region Drawing

        /// <summary>Virtualizes the worksheet and returns one clicked anchor or empty address.</summary>
        /// <param name="preset">Mapping declarations only.</param>
        /// <param name="fields">Declared display names.</param>
        /// <param name="width">Cell width chosen in Grid View.</param>
        /// <param name="selected">Current anchor index.</param>
        /// <param name="edit">Handles row and column context-menu operations.</param>
        /// <param name="cell">Receives the clicked address.</param>
        /// <param name="index">Receives its mapping index, or -1 for an empty cell.</param>
        /// <param name="select">Whether the right button requested selection rather than painting.</param>
        /// <returns>True when a cell was clicked.</returns>
        internal bool Draw(ExcelExchangePreset preset, List<ExcelField> fields, int width, int selected, Action<bool, bool, int> edit, out string cell, out int index, out bool select)
        {
            cell = string.Empty;
            index = -1;
            select = false;
            cellStyle ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, wordWrap = true,
                fixedHeight = 0f, fixedWidth = 0f, fontSize = 12, padding = new RectOffset(9, 9, 5, 5) };
            BuildIndex(preset);
            // Reserve the scrollable extent, then draw only the visible coordinates.
            const float height = 76f;
            Rect viewport = GUILayoutUtility.GetRect(100f, 10000f, 240f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            bool hovered = viewport.Contains(Event.current.mousePosition);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0f, 0f, 42f + preset.Columns * width, 24f + preset.Rows * height));
            if (hovered)
                ExcelGridMenus.Draw(preset, width, height, edit);
            int firstRow = Math.Max(1, (int)(scroll.y / height));
            int lastRow = Math.Min(preset.Rows, firstRow + (int)(viewport.height / height) + 2);
            int firstColumn = Math.Max(1, (int)(scroll.x / width));
            int lastColumn = Math.Min(preset.Columns, firstColumn + (int)(viewport.width / width) + 2);
            Color background = GUI.backgroundColor;
            // Labels belong to anchors; repeated cells stay empty and select their anchor.
            for (int column = firstColumn; column <= lastColumn; column++)
                GUI.Label(new Rect(42f + (column - 1) * width, 0f, width, 24f), ExcelAddress.Format(1, column).TrimEnd('1'), EditorStyles.centeredGreyMiniLabel);
            for (int row = firstRow; row <= lastRow; row++)
            {
                GUI.Label(new Rect(0f, 24f + (row - 1) * height, 39f, height), row.ToString(), EditorStyles.centeredGreyMiniLabel);
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    Rect rect = new Rect(42f + (column - 1) * width, 24f + (row - 1) * height, width - 6f, height - 6f);
                    int anchor = Find(row, column, out bool repeated);
                    ExcelCellMapping mapping = anchor >= 0 ? preset.Mappings[anchor] : null;
                    GUI.backgroundColor = mapping != null ? repeated ? repeatTint : anchor == selected ? selectionTint : anchorTint : background;
                    string caption = mapping != null && !repeated ? ExcelLayoutSchema.Caption(mapping, fields) : string.Empty;
                    string tooltip = ExcelAddress.Format(row, column) + "\nLeft: brush. Right: select." + (mapping != null
                        ? "\n" + ExcelLayoutSchema.Caption(mapping, fields) + "\n" + mapping.Path + (repeated ? "\nSelect the column anchor at " + mapping.Cell : string.Empty) : "\nPlace the selected mapping here.");
                    GUI.Box(rect, new GUIContent(caption, tooltip), cellStyle);
                    if (hovered && Event.current.type == EventType.MouseDown && Event.current.button is 0 or 1 && rect.Contains(Event.current.mousePosition))
                    {
                        select = Event.current.button == 1;
                        cell = ExcelAddress.Format(row, column);
                        index = select || !repeated ? anchor : -1;
                        Event.current.Use();
                    }
                    GUI.backgroundColor = background;
                }
            }
            GUI.EndScrollView();
            return cell.Length > 0;
        }

        #endregion
        #region Coordinates

        /// <summary>Indexes anchor coordinates once per draw instead of parsing every mapping for every visible cell.</summary>
        /// <param name="preset">Current mapping proposal.</param>
        private void BuildIndex(ExcelExchangePreset preset)
        {
            anchors.Clear();
            foreach (List<(int Row, int Index)> column in repeats.Values)
                column.Clear();
            for (int index = 0; index < preset.Mappings.Length; index++)
            {
                ExcelCellMapping mapping = preset.Mappings[index];
                if (!ExcelAddress.TryParse(mapping.Cell, out (int Row, int Column) position))
                    continue;
                anchors.TryAdd(position, index);
                if (mapping.Group.Length == 0)
                    continue;
                if (!repeats.TryGetValue(position.Column, out List<(int Row, int Index)> column))
                    repeats.Add(position.Column, column = new List<(int Row, int Index)>());
                column.Add((position.Row, index));
            }
        }

        /// <summary>Finds an exact anchor before resolving a repeated column beneath it.</summary>
        /// <param name="row">Visible row.</param>
        /// <param name="column">Visible column.</param>
        /// <param name="repeated">Whether the address is below its real anchor.</param>
        /// <returns>The mapping index or -1 for an empty cell.</returns>
        private int Find(int row, int column, out bool repeated)
        {
            repeated = false;
            if (anchors.TryGetValue((row, column), out int exact))
                return exact;
            int result = -1;
            int nearest = 0;
            if (repeats.TryGetValue(column, out List<(int Row, int Index)> candidates))
                foreach ((int Row, int Index) candidate in candidates)
                    if (candidate.Row < row && candidate.Row > nearest)
                    {
                        nearest = candidate.Row;
                        result = candidate.Index;
                    }
            repeated = result >= 0;
            return result;
        }

        #endregion
        #endregion
    }
}
