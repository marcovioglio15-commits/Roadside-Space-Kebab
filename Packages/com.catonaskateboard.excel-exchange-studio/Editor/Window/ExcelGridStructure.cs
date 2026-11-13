using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Inserts and removes mapping coordinates as a single reversible operation.</summary>
    internal static class ExcelGridStructure
    {
        #region Methods
        #region Editing

        /// <summary>Moves anchors, row guides and fixed boundaries without editing the workbook.</summary>
        /// <param name="preset">Detached mapping configuration.</param>
        /// <param name="row">Whether to edit rows rather than columns.</param>
        /// <param name="insert">Whether to insert rather than remove.</param>
        /// <param name="position">One-based inserted or removed coordinate.</param>
        internal static void Change(ExcelExchangePreset preset, bool row, bool insert, int position)
        {
            int count = row ? preset.Rows : preset.Columns;
            int maximum = row ? 1048576 : 16384;
            if (position < 1 || position > count + (insert ? 1 : 0) || !insert && count <= 1 || insert && count >= maximum)
                throw new InvalidOperationException("Choose an existing grid coordinate. Keep at least one row and column.");
            // Validate every affected address before recording or changing any value.
            foreach (ExcelCellMapping mapping in preset.Mappings)
                Validate(mapping.Cell, row, insert, maximum);
            foreach (ExcelCollectionGroup group in preset.Groups)
            {
                if (group.GuideCell.Length > 0)
                    Validate(group.GuideCell, row, insert, maximum);
                if (row && insert && group.Extent == ExcelRowExtent.LastRow && group.LastRow >= maximum)
                    throw new InvalidOperationException("This edit would move the final import row outside Excel worksheet limits.");
            }
            Undo.RegisterCompleteObjectUndo(preset, (insert ? "Insert " : "Remove ") + (row ? "Mapping Row" : "Mapping Column"));
            List<ExcelCellMapping> retained = new List<ExcelCellMapping>();
            foreach (ExcelCellMapping mapping in preset.Mappings)
            {
                string moved = Move(mapping.Cell, row, insert, position);
                if (moved.Length == 0)
                    continue;
                mapping.Cell = moved;
                retained.Add(mapping);
            }
            preset.Mappings = retained.ToArray();
            preset.Groups = preset.Groups.Where(group => retained.Any(mapping => mapping.Group == group.Name)).ToArray();
            foreach (ExcelCollectionGroup group in preset.Groups)
            {
                if (group.GuideCell.Length > 0)
                    group.GuideCell = Move(group.GuideCell, row, insert, position);
                if (row && group.Extent == ExcelRowExtent.LastRow && group.LastRow >= position)
                    group.LastRow += insert ? 1 : -1;
            }
            if (row)
                preset.Rows += insert ? 1 : -1;
            else
                preset.Columns += insert ? 1 : -1;
        }

        #endregion
        #region Coordinates

        /// <summary>Rejects invalid or overflowing addresses before a structural edit starts.</summary>
        /// <param name="address">Mapped anchor or guide.</param>
        /// <param name="row">Edited axis.</param>
        /// <param name="insert">Whether a new coordinate is inserted.</param>
        /// <param name="maximum">Excel limit for this axis.</param>
        private static void Validate(string address, bool row, bool insert, int maximum)
        {
            (int Row, int Column) cell = ExcelAddress.Parse(address);
            if (insert && (row ? cell.Row : cell.Column) >= maximum)
                throw new InvalidOperationException("This edit would move a mapping outside Excel worksheet limits.");
        }

        /// <summary>Translates one address or removes it when its row or column is deleted.</summary>
        /// <param name="address">Existing anchor or row guide.</param>
        /// <param name="row">Axis receiving the edit.</param>
        /// <param name="insert">Whether to insert a coordinate.</param>
        /// <param name="position">Inserted or removed index.</param>
        /// <returns>The translated address, or empty for a removed cell.</returns>
        private static string Move(string address, bool row, bool insert, int position)
        {
            (int Row, int Column) cell = ExcelAddress.Parse(address);
            int value = row ? cell.Row : cell.Column;
            if (!insert && value == position)
                return string.Empty;
            if (value >= position)
                value += insert ? 1 : -1;
            return ExcelAddress.Format(row ? value : cell.Row, row ? cell.Column : value);
        }

        #endregion
        #endregion
    }
}
