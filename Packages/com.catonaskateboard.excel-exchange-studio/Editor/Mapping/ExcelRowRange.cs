using System;
using System.Collections.Generic;
using System.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Resolves one shared row extent for all columns in a collection group.</summary>
    internal static class ExcelRowRange
    {
        #region Methods

        /// <summary>Reads contiguous row identities without treating empty text or lists as table endings.</summary>
        /// <param name="group">Boundary and guide-column settings.</param>
        /// <param name="columns">Active group columns.</param>
        /// <param name="sheet">Source worksheet.</param>
        /// <returns>Included worksheet rows.</returns>
        internal static List<int> Read(ExcelCollectionGroup group, ExcelCellMapping[] columns, ExcelSheet sheet)
        {
            ExcelCellMapping guide = columns.FirstOrDefault(mapping => mapping.Path == group.Path + "." + group.KeyPath && mapping.Kind == ExcelMappingKind.Data)
                ?? columns.First(mapping => mapping.Kind == ExcelMappingKind.Data);
            (int Row, int Column) anchor = ExcelAddress.Parse(group.GuideCell.Length > 0 ? group.GuideCell : guide.Cell);
            int last = sheet.LastRow;
            switch (group.Extent)
            {
                case ExcelRowExtent.LastRow:
                    last = group.LastRow;
                    break;
                case ExcelRowExtent.ExcelTable:
                    if (!sheet.Tables.TryGetValue(group.TableName, out string range))
                        throw new FormatException("Excel table not found: " + group.TableName);
                    last = ExcelAddress.Parse(range.Split(':').Last()).Row - sheet.TableTotals[group.TableName];
                    break;
            }
            if (last < anchor.Row && group.Policy == ExcelCollectionPolicy.ReplaceCollection)
                return new List<int>();
            if (last < anchor.Row || last > 1048576)
                throw new FormatException("The row boundary must include the anchor: " + guide.Cell);
            List<int> result = new List<int>();
            bool ended = false;
            for (int row = anchor.Row; row <= last; row++)
            {
                ExcelCell key = sheet.Get(row, anchor.Column);
                if (string.IsNullOrEmpty(key.Text))
                {
                    if (group.Extent != ExcelRowExtent.FirstBlankKey)
                        throw new FormatException("Missing row key at " + ExcelAddress.Format(row, anchor.Column));
                    ended = true;
                    if (columns.Any(mapping => !string.IsNullOrEmpty(sheet.Get(row, ExcelAddress.Parse(mapping.Cell).Column).Text)))
                        throw new FormatException("A partially filled row has no key at " + ExcelAddress.Format(row, anchor.Column));
                    continue;
                }
                if (ended)
                    throw new FormatException("Data follows an empty row key at " + ExcelAddress.Format(row, anchor.Column) + ". Choose a fixed boundary for separate tables.");
                result.Add(row);
            }
            if (result.Count == 0 && group.Policy != ExcelCollectionPolicy.ReplaceCollection)
                throw new FormatException("No rows found at " + guide.Cell + ". Choose Replace Collection explicitly to clear an unambiguous collection.");
            return result;
        }

        #endregion
    }
}
