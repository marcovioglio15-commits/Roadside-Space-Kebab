using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Adds item names and practical correction steps to import results.</summary>
    internal static class ExcelImportMessages
    {
        #region Methods
        #region Item Names

        /// <summary>Names a spreadsheet item from its parent keys and mapped name.</summary>
        /// <param name="group">Collection receiving the row.</param>
        /// <param name="columns">Mapped columns.</param>
        /// <param name="sheet">Source worksheet.</param>
        /// <param name="row">Source row.</param>
        /// <param name="includeItem">Whether to include the child's name after its parents.</param>
        /// <returns>A readable item identity independent of internal array positions.</returns>
        internal static string Item(ExcelCollectionGroup group, ExcelCellMapping[] columns, ExcelSheet sheet, int row, bool includeItem = true)
        {
            // Parent keys distinguish identically named items in different lists.
            List<string> parts = columns.Where(mapping => mapping.Kind == ExcelMappingKind.ParentKey)
                .OrderBy(mapping => ExcelPropertyPath.Collections(mapping.Path).Length)
                .Select(mapping => sheet.Get(row, ExcelAddress.Parse(mapping.Cell).Column).Text).Where(value => value.Length > 0).ToList();
            if (includeItem)
            {
                ExcelCellMapping name = columns.FirstOrDefault(mapping => mapping.Path == group.Path + "." + group.KeyPath)
                    ?? columns.FirstOrDefault(mapping => mapping.Path == group.Path + ".Name");
                string label = name != null ? sheet.Get(row, ExcelAddress.Parse(name.Cell).Column).Text : string.Empty;
                parts.Add(label.Length > 0 ? label : "Row " + row);
            }
            return parts.Count > 0 ? string.Join(" / ", parts) : "All items";
        }

        #endregion
        #region Corrections

        /// <summary>Creates a correction for a missing parent, duplicate name or row-matching problem.</summary>
        /// <param name="preview">Current review.</param>
        /// <param name="mapping">Affected input column.</param>
        /// <param name="fields">Friendly field names.</param>
        /// <param name="row">Source row.</param>
        /// <param name="item">Affected item.</param>
        /// <param name="message">Problem description.</param>
        /// <param name="fix">Required correction.</param>
        /// <param name="value">Value read from the sheet.</param>
        /// <returns>An error retaining the exact source location.</returns>
        internal static ExcelImportProblem Problem(ExcelImportPreview preview, ExcelCellMapping mapping, List<ExcelField> fields,
            int row, string item, string message, string fix, string value = "")
        {
            // Carry mapping coordinates with the error so the review can open Layout directly.
            return new ExcelImportProblem(new ExcelPreviewRow(preview.Preset.Worksheet + "!" + ExcelAddress.Format(row, ExcelAddress.Parse(mapping.Cell).Column),
                fields.FirstOrDefault(field => field.Path == mapping.Path)?.Label ?? mapping.Path, string.Empty, value, message, true, item,
                fix: fix, mapping: mapping.Path, anchor: mapping.Cell));
        }

        /// <summary>Explains a failed value with the exact cell, selected field and column title.</summary>
        /// <param name="preview">Review receiving the error.</param>
        /// <param name="mapping">Affected column.</param>
        /// <param name="sheet">Available source worksheet.</param>
        /// <param name="field">Expected field declaration.</param>
        /// <param name="row">Failed source row.</param>
        /// <param name="item">Parent and item names.</param>
        /// <param name="exception">Original conversion failure.</param>
        internal static void Cell(ExcelImportPreview preview, ExcelCellMapping mapping, ExcelSheet sheet, ExcelField field,
            int row, string item, Exception exception)
        {
            // Pair the value that failed with the expected input and the actual column heading.
            int column = ExcelAddress.Parse(mapping.Cell).Column;
            string value = sheet?.Get(row, column).Text ?? string.Empty;
            string heading = sheet?.Get(Math.Max(1, ExcelAddress.Parse(mapping.Cell).Row - 1), column).Text ?? string.Empty;
            string expected = field != null ? Expected(field.Type, mapping.Separator) : "a value for the selected field";
            string fix = "Expected " + expected + ". ";
            if (heading.Length > 0)
                fix += "The Excel column is titled '" + heading + "'. ";
            string suggestion = Suggest(preview.Preset, mapping, sheet);
            fix += suggestion.Length > 0 ? "In Edit Mapping, set Cell to " + suggestion + ". Otherwise, correct the values in this Excel column."
                : "Correct the cell, or use Edit Mapping and set Cell to the right column's starting cell.";
            preview.Rows.Add(new ExcelPreviewRow(preview.Preset.Worksheet + "!" + ExcelAddress.Format(row, column),
                field?.Label ?? mapping.Path, string.Empty, value, Friendly(exception, field?.Type), true, item,
                fix: fix, mapping: mapping.Path, anchor: mapping.Cell));
        }

        /// <summary>Suggests an existing column when its header matches the configured export heading.</summary>
        /// <param name="preset">Current column mappings.</param>
        /// <param name="mapping">Failing field.</param>
        /// <param name="sheet">Source cells.</param>
        /// <returns>A possible starting cell, or empty when the heading is not unique.</returns>
        private static string Suggest(ExcelExchangePreset preset, ExcelCellMapping mapping, ExcelSheet sheet)
        {
            // Suggest a moved column only when the workbook has one matching heading.
            if (sheet == null)
                return string.Empty;
            (int Row, int Column) anchor = ExcelAddress.Parse(mapping.Cell);
            ExcelCellMapping heading = preset.Mappings.FirstOrDefault(candidate => candidate.Kind == ExcelMappingKind.Literal
                && ExcelAddress.Parse(candidate.Cell).Column == anchor.Column && ExcelAddress.Parse(candidate.Cell).Row == anchor.Row - 1);
            if (heading == null || heading.Text.Length == 0)
                return string.Empty;
            string expected = Normalize(heading.Text);
            int[] columns = sheet.Cells.Where(cell => cell.Key.Row == anchor.Row - 1 && HeaderMatches(Normalize(cell.Value.Text), expected))
                .Select(cell => cell.Key.Column).ToArray();
            return columns.Length == 1 && columns[0] != anchor.Column ? ExcelAddress.Format(anchor.Row, columns[0]) : string.Empty;
        }

        /// <summary>Accepts a simple plural heading only when it still identifies a unique column.</summary>
        /// <param name="actual">Normalized Excel heading.</param>
        /// <param name="expected">Normalized configured heading.</param>
        /// <returns>True for an exact or singular/plural match.</returns>
        private static bool HeaderMatches(string actual, string expected)
        {
            // Allow common heading differences such as Flag and Flags.
            return actual == expected || actual == expected + "s" || actual + "s" == expected;
        }

        /// <summary>Matches headings despite spaces and punctuation differences.</summary>
        /// <param name="text">Header text.</param>
        /// <returns>Comparable heading letters and digits.</returns>
        private static string Normalize(string text)
        {
            // Punctuation and spacing do not change a heading's identity.
            return new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        /// <summary>Describes acceptable cell values without exposing C# type names.</summary>
        /// <param name="type">Expected value or list type.</param>
        /// <param name="separator">List delimiter.</param>
        /// <returns>A short input example or value family.</returns>
        internal static string Expected(Type type, string separator = ";")
        {
            // Describe list entries before considering scalar input examples.
            Type element = ExcelPropertyPath.ElementType(type);
            if (element != null)
                return typeof(UnityEngine.Object).IsAssignableFrom(element) ? "project item names separated by '" + separator + "'" : "values separated by '" + separator + "'";
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return "the name of an existing project item";
            if (type.IsEnum)
                return "one of: " + string.Join(", ", Enum.GetNames(type));
            return type.Name switch
            {
                "Boolean" => "TRUE or FALSE",
                "String" => "text",
                "Char" => "one character",
                "Vector2" or "Vector2Int" => "two numbers separated by |, for example 1|2",
                "Vector3" or "Vector3Int" => "three numbers separated by |, for example 1|2|3",
                "Vector4" or "Quaternion" => "four numbers separated by |",
                "Color" or "Color32" => "a color such as #FFFFFF or four numbers separated by |",
                _ => "a number, for example 0.2"
            };
        }

        /// <summary>Keeps ordinary conversion errors short while retaining useful project-reference details.</summary>
        /// <param name="exception">Failed conversion.</param>
        /// <param name="type">Optional expected field type.</param>
        /// <returns>A concise explanation of the problem.</returns>
        private static string Friendly(Exception exception, Type type)
        {
            // Replace framework errors while preserving useful missing-item names.
            if (exception is OverflowException)
                return "This number is too large for the selected field.";
            if (type?.IsEnum ?? false)
                return "This value is not one of the available choices.";
            if (type != null && type.IsPrimitive && type != typeof(bool) && type != typeof(char) && exception is FormatException
                && exception.Message.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0)
                return "This value is not a valid number.";
            return exception.Message.Replace("Use TRUE or FALSE in this cell.", "This field needs TRUE or FALSE.")
                .Replace("Input string was not in a correct format.", "This value has the wrong format.");
        }

        /// <summary>Makes existing preset warnings easier to read without changing validation rules.</summary>
        /// <param name="warning">Original preset warning.</param>
        /// <returns>A compact warning for the review.</returns>
        internal static string Warning(string warning)
        {
            // Retain the preset's warning while using the labels shown by this tool.
            return warning.Replace("additive or subtractive flags", "required or excluded flags")
                .Replace("declared defaults", "default values").Replace("authored", "saved");
        }

        #endregion
        #endregion
    }
}
