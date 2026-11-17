using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Shares cell and header positions between the grid, its buttons and context menus.</summary>
    internal readonly struct ExcelGridGeometry
    {
        #region Fields

        internal const float Gutter = 60f;
        internal const float Header = 44f;
        internal const float Gap = 18f;
        internal const float CellHeight = 68f;
        internal readonly float Width;
        internal float ColumnStride => Width + Gap;
        internal float RowStride => CellHeight + Gap;

        #endregion
        #region Methods
        #region Coordinates

        /// <summary>Stores the selected cell width while reserving separate space for insertion buttons.</summary>
        /// <param name="width">Cell width in GUI points.</param>
        internal ExcelGridGeometry(float width)
        {
            // Every grid control uses the same cell width.
            Width = width;
        }

        /// <summary>Locates a cell without including its surrounding button gaps.</summary>
        /// <param name="row">One-based row.</param>
        /// <param name="column">One-based column.</param>
        /// <returns>The exact paint and selection area.</returns>
        internal Rect Cell(int row, int column)
        {
            // Exclude insertion gaps from the cell's click area.
            return new Rect(Gutter + (column - 1) * ColumnStride, Header + (row - 1) * RowStride, Width, CellHeight);
        }

        /// <summary>Locates the insertion gap immediately before a row or column.</summary>
        /// <param name="row">Whether to measure a row gap.</param>
        /// <param name="before">One-based following row or column.</param>
        /// <returns>The gap's center coordinate.</returns>
        internal float Boundary(bool row, int before)
        {
            // The same formula also places the final insertion button after the last cell.
            return (row ? Header : Gutter) + (before - 1) * (row ? RowStride : ColumnStride) - Gap * 0.5f;
        }

        #endregion
        #endregion
    }
}
