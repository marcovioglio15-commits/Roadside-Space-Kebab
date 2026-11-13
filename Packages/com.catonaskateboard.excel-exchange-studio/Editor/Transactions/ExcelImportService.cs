using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatOnASkateboard.StudioColors.Editor;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Builds an isolated import proposal before any tool values can change.</summary>
    public static class ExcelImportService
    {
        #region Methods
        #region Preview

        /// <summary>Previews exact cells and repeated rows using the linked preset's current schema.</summary>
        /// <param name="context">Tool session retaining the current values.</param>
        /// <param name="preset">Linked exchange configuration.</param>
        /// <param name="path">Optional source override for explicit verification.</param>
        /// <returns>An owned preview, including conversion errors.</returns>
        public static ExcelImportPreview Preview(StudioPresetContext context, ExcelExchangePreset preset, string path = null)
        {
            if (context == null || !context.IsCurrent())
                throw new InvalidOperationException("Keep the selected preset open in its tool.");
            ExcelImportPreview preview = new ExcelImportPreview(context, preset, path ?? preset.WorkbookPath);
            try
            {
                List<ExcelField> fields = ExcelFieldCatalog.Build(preview.Before, context.Paths);
                ExcelMappingRules.Validate(context, preset, fields, false);
                ExcelWorkbook workbook = ExcelWorkbookReader.Read(preview.Path);
                HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
                // Single values are independent from structural collection operations.
                foreach (ExcelCellMapping mapping in preset.Mappings.Where(mapping => ExcelMappingRules.Enabled(mapping, false) && mapping.Group.Length == 0))
                    ApplyCell(preview, mapping, workbook.Sheets, fields, ExcelAddress.Parse(mapping.Cell).Row, mapping.Indices, occupied);
                foreach (ExcelCollectionGroup group in preset.Groups.OrderBy(group => ExcelPropertyPath.Collections(group.Path).Length))
                {
                    ExcelCellMapping[] columns = ExcelMappingRules.Columns(preset, group, false);
                    if (columns.Length > 0)
                        ExcelCollectionImporter.Apply(preview, group, columns, workbook.Sheets, fields, occupied);
                }
                string warning = context.Validate?.Invoke(preview.Proposal) ?? Validate(preview.Proposal);
                if (!string.IsNullOrEmpty(warning))
                    preview.Warnings.Add(warning + " Review these settings before applying the preset.");
                StudioPresetActions.Review(context.Source, preview.Before, preview.Proposal, preview.Warnings);
                if (ExcelImportPreview.Hash(preview.Path) != preview.WorkbookHash)
                    throw new InvalidOperationException("The workbook changed while reading. Run Preview again.");
            }
            catch (Exception exception)
            {
                preview.Rows.Add(new ExcelPreviewRow(string.Empty, string.Empty, string.Empty, string.Empty, exception.Message, true));
            }
            return preview;
        }

        /// <summary>Converts one mapped cell, recording errors without changing the real tool.</summary>
        /// <param name="preview">Isolated transaction.</param>
        /// <param name="mapping">Mapped property or parent key.</param>
        /// <param name="sheets">Available source sheets.</param>
        /// <param name="fields">Scoped type metadata.</param>
        /// <param name="row">Expanded source row.</param>
        /// <param name="indices">Resolved collection element positions.</param>
        /// <param name="occupied">Coordinates already consumed by this layout.</param>
        /// <param name="original">Optional original element value for a rebuilt collection.</param>
        internal static void ApplyCell(ExcelImportPreview preview, ExcelCellMapping mapping,
            Dictionary<string, ExcelSheet> sheets, List<ExcelField> fields, int row, int[] indices, HashSet<string> occupied, string original = null)
        {
            string address = preview.Preset.Worksheet + "!" + ExcelAddress.Format(row, ExcelAddress.Parse(mapping.Cell).Column);
            try
            {
                if (!occupied.Add(address))
                    throw new FormatException("Repeated mappings overlap at " + address);
                if (!sheets.TryGetValue(preview.Preset.Worksheet, out ExcelSheet sheet))
                    throw new FormatException("Worksheet not found: " + preview.Preset.Worksheet);
                if (sheet.IsMerged(row, ExcelAddress.Parse(mapping.Cell).Column))
                    throw new FormatException("Unmerge this mapped cell before importing.");
                ExcelCell cell = sheet.Get(row, ExcelAddress.Parse(mapping.Cell).Column);
                if (mapping.Kind != ExcelMappingKind.Data || mapping.KeepEmpty && cell.Text.Length == 0 && cell.Formula.Length == 0)
                    return;
                ExcelField field = fields.First(candidate => candidate.Path == mapping.Path);
                object before = ExcelPropertyPath.Get(preview.Proposal, mapping.Path, indices);
                object after = ExcelValueCodec.Read(cell, field.Type, mapping.Separator, preview.Preset.UseFormulaResults);
                ExcelPropertyPath.Set(preview.Proposal, mapping.Path, after, indices);
                preview.Rows.Add(new ExcelPreviewRow(address, field.Label, original ?? ExcelValueCodec.Format(before, field.Type, mapping.Separator),
                    ExcelValueCodec.Format(after, field.Type, mapping.Separator)));
            }
            catch (Exception exception)
            {
                preview.Rows.Add(new ExcelPreviewRow(address, mapping.Path, string.Empty, string.Empty, exception.Message, true));
            }
        }

        /// <summary>Runs an existing simple preset validator as a warning on the proposal.</summary>
        /// <param name="proposal">Detached typed preset.</param>
        /// <returns>The original validation warning or an empty string.</returns>
        private static string Validate(Object proposal)
        {
            MethodInfo method = proposal.GetType().GetMethod("TryValidate", new[] { typeof(string).MakeByRefType() });
            if (method == null)
                method = proposal.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(candidate =>
                    candidate.Name == "TryGetSettings" && candidate.ReturnType == typeof(bool)
                    && candidate.GetParameters().Length > 0 && candidate.GetParameters().All(parameter => parameter.IsOut)
                    && candidate.GetParameters().Last().ParameterType == typeof(string).MakeByRefType());
            if (method == null || method.ReturnType != typeof(bool))
                return string.Empty;
            // Out-only settings readers validate the snapshot without requiring live scene context.
            object[] arguments = new object[method.GetParameters().Length];
            arguments[arguments.Length - 1] = string.Empty;
            return (bool)method.Invoke(proposal, arguments) ? string.Empty : (string)arguments[arguments.Length - 1];
        }

        #endregion
        #endregion
    }
}
