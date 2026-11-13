using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Offers structural edits on the separators between mapping cells.</summary>
    internal static class ExcelGridMenus
    {
        #region Methods

        /// <summary>Opens row or column actions only when the right button hits a cell boundary.</summary>
        /// <param name="preset">Current grid dimensions.</param>
        /// <param name="width">Cell stride.</param>
        /// <param name="height">Row stride.</param>
        /// <param name="edit">Runs one reversible structural edit.</param>
        internal static void Draw(ExcelExchangePreset preset, float width, float height, Action<bool, bool, int> edit)
        {
            Event input = Event.current;
            if (input.type != EventType.ContextClick)
                return;
            Vector2 point = input.mousePosition;
            int row = Mathf.RoundToInt((point.y - 24f) / height);
            int column = Mathf.RoundToInt((point.x - 42f) / width);
            bool horizontal = row >= 0 && row <= preset.Rows && point.x >= 42f
                && point.x <= 42f + preset.Columns * width && Mathf.Abs(point.y - 24f - row * height) <= 5f;
            bool vertical = column >= 0 && column <= preset.Columns && point.y >= 24f
                && point.y <= 24f + preset.Rows * height && Mathf.Abs(point.x - 42f - column * width) <= 5f;
            if (!horizontal && !vertical)
                return;
            GenericMenu menu = new GenericMenu();
            if (horizontal)
                Add(menu, true, row + 1, preset.Rows, edit);
            if (horizontal && vertical)
                menu.AddSeparator(string.Empty);
            if (vertical)
                Add(menu, false, column + 1, preset.Columns, edit);
            menu.ShowAsContext();
            input.Use();
        }

        /// <summary>Builds insertion and adjacent-removal actions for one boundary.</summary>
        /// <param name="menu">Native context menu.</param>
        /// <param name="row">Whether this boundary separates rows.</param>
        /// <param name="before">Coordinate following the boundary.</param>
        /// <param name="count">Current grid extent.</param>
        /// <param name="edit">Structural edit callback.</param>
        private static void Add(GenericMenu menu, bool row, int before, int count, Action<bool, bool, int> edit)
        {
            string axis = row ? "Row" : "Column";
            menu.AddItem(new GUIContent("Insert " + axis + " Here"), false, () => edit(row, true, before));
            if (count > 1 && before > 1)
                menu.AddItem(new GUIContent("Remove " + axis + (row ? " Above" : " Left")), false, () => edit(row, false, before - 1));
            if (count > 1 && before <= count)
                menu.AddItem(new GUIContent("Remove " + axis + (row ? " Below" : " Right")), false, () => edit(row, false, before));
        }

        #endregion
    }
}
