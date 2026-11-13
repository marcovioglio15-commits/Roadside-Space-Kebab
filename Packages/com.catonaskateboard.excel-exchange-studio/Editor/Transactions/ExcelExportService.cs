using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Expands the same logical mappings for export without saving the source preset.</summary>
    public static class ExcelExportService
    {
        #region Methods
        #region Planning

        /// <summary>Builds managed output cells from the tool's current proposal.</summary>
        /// <param name="context">Selected editing session.</param>
        /// <param name="preset">Linked exchange configuration.</param>
        /// <param name="template">Optional already-read template for styles and stale row cleanup.</param>
        /// <returns>Sparse output cells without unrelated template contents.</returns>
        public static ExcelWorkbook Build(StudioPresetContext context, ExcelExchangePreset preset, ExcelWorkbook template = null)
        {
            using Snapshot snapshot = new Snapshot(context.Capture());
            List<ExcelField> fields = ExcelFieldCatalog.Build(snapshot.Value, context.Paths);
            ExcelMappingRules.Validate(context, preset, fields, true);
            ExcelWorkbook result = new ExcelWorkbook();
            // Literal headers and single values never depend on collection length.
            foreach (ExcelCellMapping mapping in preset.Mappings.Where(mapping => ExcelMappingRules.Enabled(mapping, true) && mapping.Group.Length == 0))
                Write(result, template, mapping, snapshot.Value, fields, ExcelAddress.Parse(mapping.Cell).Row, mapping.Indices, preset.Worksheet);
            foreach (ExcelCollectionGroup group in preset.Groups)
            {
                ExcelCellMapping[] columns = ExcelMappingRules.Columns(preset, group, true);
                if (columns.Length == 0)
                    continue;
                List<int[]> rows = Elements(snapshot.Value, group.Path);
                int first = ExcelAddress.Parse(columns[0].Cell).Row;
                if (group.Extent == ExcelRowExtent.ExcelTable)
                {
                    if (template == null || !template.Sheets.TryGetValue(preset.Worksheet, out ExcelSheet tableSheet)
                        || !tableSheet.Tables.TryGetValue(group.TableName, out string tableRange))
                        throw new InvalidOperationException("Choose a template containing the Excel table " + group.TableName + ".");
                    string[] corners = tableRange.Split(':');
                    (int Row, int Column) end = ExcelAddress.Parse(corners.Last());
                    int totals = tableSheet.TableTotals[group.TableName];
                    int last = first + rows.Count - 1 + totals;
                    if (totals > 0 && last != end.Row)
                        throw new InvalidOperationException("Use a template without a totals row to resize the table " + group.TableName + ".");
                    result.Sheet(preset.Worksheet).Tables[group.TableName] = corners[0] + ":" + ExcelAddress.Format(last, end.Column);
                }
                for (int index = 0; index < rows.Count; index++)
                    foreach (ExcelCellMapping mapping in columns)
                        Write(result, template, mapping, snapshot.Value, fields, first + index, rows[index], preset.Worksheet);
                // Clear only the previous managed columns and their known contiguous extent.
                if (template != null && template.Sheets.TryGetValue(preset.Worksheet, out ExcelSheet existing))
                {
                    ExcelCellMapping guide = columns.FirstOrDefault(mapping => mapping.Path == group.Path + "." + group.KeyPath)
                        ?? columns.First(mapping => mapping.Kind == ExcelMappingKind.Data);
                    if (existing.Get(first, ExcelAddress.Parse(group.GuideCell.Length > 0 ? group.GuideCell : guide.Cell).Column).Text.Length > 0)
                        foreach (int row in ExcelRowRange.Read(group, columns, existing).Where(row => row >= first + rows.Count))
                            foreach (ExcelCellMapping mapping in columns)
                                Put(result.Sheet(preset.Worksheet), row, ExcelAddress.Parse(mapping.Cell).Column, new ExcelCell(string.Empty, ExcelCellKind.Blank));
                }
            }
            return result;
        }

        /// <summary>Exports current values atomically using the optional template.</summary>
        /// <param name="context">Selected editing session.</param>
        /// <param name="preset">Linked configuration.</param>
        /// <param name="path">Explicit destination selected by the user.</param>
        public static void Export(StudioPresetContext context, ExcelExchangePreset preset, string path)
        {
            ExcelWorkbook template = preset.TemplatePath.Length > 0 ? ExcelWorkbookReader.Read(preset.TemplatePath) : null;
            ExcelWorkbookWriter.Write(path, Build(context, preset, template), preset.TemplatePath);
        }

        /// <summary>Enumerates concrete element positions for a logical nested collection.</summary>
        /// <param name="root">Typed source snapshot.</param>
        /// <param name="path">Deepest repeated collection.</param>
        /// <returns>Outer-to-inner indices in preset order.</returns>
        public static List<int[]> Elements(object root, string path)
        {
            List<int[]> positions = new List<int[]> { Array.Empty<int>() };
            foreach (string collection in ExcelPropertyPath.Collections(path))
            {
                List<int[]> next = new List<int[]>();
                foreach (int[] parents in positions)
                {
                    IList values = (IList)ExcelPropertyPath.Get(root, collection, parents);
                    for (int index = 0; values != null && index < values.Count; index++)
                        next.Add(parents.Concat(new[] { index }).ToArray());
                }
                positions = next;
            }
            return positions;
        }

        #endregion
        #region Cells

        /// <summary>Converts one mapped value while retaining the anchor's template style.</summary>
        /// <param name="output">Managed output workbook.</param>
        /// <param name="template">Optional template cells.</param>
        /// <param name="mapping">Cell conversion settings.</param>
        /// <param name="root">Source snapshot.</param>
        /// <param name="fields">Scoped schema.</param>
        /// <param name="row">Expanded output row.</param>
        /// <param name="indices">Concrete element positions.</param>
        /// <param name="worksheet">Selected workbook tab.</param>
        private static void Write(ExcelWorkbook output, ExcelWorkbook template, ExcelCellMapping mapping,
            object root, List<ExcelField> fields, int row, int[] indices, string worksheet)
        {
            (int Row, int Column) anchor = ExcelAddress.Parse(mapping.Cell);
            if (template != null && template.Sheets.TryGetValue(worksheet, out ExcelSheet targetSheet) && targetSheet.IsMerged(row, anchor.Column))
                throw new FormatException("Unmerge the mapped template cell " + worksheet + "!" + ExcelAddress.Format(row, anchor.Column) + ".");
            ExcelCell value;
            switch (mapping.Kind)
            {
                case ExcelMappingKind.Literal:
                    value = new ExcelCell(mapping.Text);
                    break;
                case ExcelMappingKind.Formula:
                    value = new ExcelCell(string.Empty, ExcelCellKind.Blank, mapping.Text.TrimStart('='));
                    break;
                default:
                    ExcelField field = fields.First(candidate => candidate.Path == mapping.Path);
                    int depth = ExcelPropertyPath.Collections(mapping.Path).Length;
                    value = ExcelValueCodec.Cell(ExcelPropertyPath.Get(root, mapping.Path, indices.Take(depth).ToArray()), field.Type, mapping.Separator);
                    break;
            }
            string style = template != null && template.Sheets.TryGetValue(worksheet, out ExcelSheet sheet)
                ? sheet.Get(anchor.Row, anchor.Column).Style : string.Empty;
            Put(output.Sheet(worksheet), row, anchor.Column, new ExcelCell(value.Text, value.Kind, value.Formula, style));
        }

        /// <summary>Rejects overlapping expanded outputs instead of letting later columns win.</summary>
        /// <param name="sheet">Output worksheet.</param>
        /// <param name="row">Output row.</param>
        /// <param name="column">Output column.</param>
        /// <param name="cell">Converted value.</param>
        private static void Put(ExcelSheet sheet, int row, int column, ExcelCell cell)
        {
            if (sheet.Cells.ContainsKey((row, column)))
                throw new InvalidOperationException("Expanded mappings overlap at " + sheet.Name + "!" + ExcelAddress.Format(row, column));
            if (row > 1048576)
                throw new InvalidOperationException("The exported rows exceed Excel's worksheet limit.");
            sheet.Set(row, column, cell);
        }

        #endregion
        #endregion
    }
}
