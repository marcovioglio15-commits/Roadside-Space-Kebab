using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Resolves mapping choices from preset declarations, independently of workbook contents.</summary>
    internal static class ExcelLayoutSchema
    {
        #region Methods
        #region Labels

        /// <summary>Lists every repeated destination, including parents of nested fields.</summary>
        /// <param name="fields">Scoped preset declarations.</param>
        /// <returns>Stable collection paths without element names or current values.</returns>
        internal static string[] Collections(List<ExcelField> fields)
        {
            return fields.SelectMany(field => ExcelPropertyPath.Collections(field.Path)).Distinct().ToArray();
        }

        /// <summary>Formats a logical path without hiding its collection hierarchy.</summary>
        /// <param name="path">Field or collection path.</param>
        /// <returns>A readable declaration label.</returns>
        internal static string Label(string path)
        {
            return string.Join(" / ", path.Replace("[]", string.Empty).Split('.').Select(ObjectNames.NicifyVariableName));
        }

        /// <summary>Keeps complete collection paths visible as flat menu choices.</summary>
        /// <param name="path">Declared collection path.</param>
        /// <param name="fields">Optional preset declarations supplying custom captions.</param>
        /// <returns>A caption without Unity menu separators.</returns>
        internal static string CollectionLabel(string path, List<ExcelField> fields = null)
        {
            return (fields?.FirstOrDefault(field => field.Collection == path)?.CollectionLabel ?? Label(path)).Replace(" / ", " → ");
        }

        /// <summary>Describes a cell without reading the mapped preset or workbook value.</summary>
        /// <param name="mapping">Configured anchor.</param>
        /// <param name="fields">Optional declared display names.</param>
        /// <returns>Wrapped field and role labels for the grid.</returns>
        internal static string Caption(ExcelCellMapping mapping, List<ExcelField> fields = null)
        {
            return mapping.Kind switch
            {
                ExcelMappingKind.Literal => mapping.Text + "\nExport text",
                ExcelMappingKind.Formula => "=" + mapping.Text.TrimStart('=') + "\nExport formula",
                ExcelMappingKind.ParentKey => (fields?.FirstOrDefault(field => field.Path == mapping.Path)?.Label ?? Label(mapping.Path)) + "\nParent key ↓",
                _ => (mapping.Group.Length == 0 ? fields?.FirstOrDefault(field => field.Path == mapping.Path)?.Label ?? FieldLabel(mapping) : FieldLabel(mapping)) + "\n" + RepeatLabel(mapping)
            };
        }

        /// <summary>Shortens repeated field paths while accepting incomplete edits.</summary>
        /// <param name="mapping">Current anchor declaration.</param>
        /// <returns>The field path relative to its repeated item.</returns>
        private static string FieldLabel(ExcelCellMapping mapping)
        {
            int start = mapping.Path.LastIndexOf("[].", StringComparison.Ordinal);
            return Label(mapping.Group.Length > 0 && start >= 0 ? mapping.Path.Substring(start + 3) : mapping.Path);
        }

        /// <summary>Names a mapping's repetition without repeating its field on every row.</summary>
        /// <param name="mapping">Configured anchor.</param>
        /// <returns>One-cell or repeated-column caption.</returns>
        private static string RepeatLabel(ExcelCellMapping mapping)
        {
            return mapping.Group.Length > 0 ? "Column ↓" : "Single cell";
        }

        #endregion
        #region Row Binding

        /// <summary>Shares policies only between columns of the same collection, sheet and starting row.</summary>
        /// <param name="preset">Detached configuration.</param>
        /// <param name="path">Collection represented by each row.</param>
        /// <param name="cell">New column anchor.</param>
        /// <returns>The existing row policy or a new generic one.</returns>
        internal static ExcelCollectionGroup Bind(ExcelExchangePreset preset, string path, string cell)
        {
            int row = ExcelAddress.Parse(cell).Row;
            foreach (ExcelCollectionGroup group in preset.Groups.Where(group => group.Path == path))
            {
                ExcelCellMapping anchor = preset.Mappings.FirstOrDefault(mapping => mapping.Group == group.Name);
                if (anchor == null || ExcelAddress.TryParse(anchor.Cell, out (int Row, int Column) position) && position.Row == row)
                    return group;
            }
            // Group identities describe declarations and coordinates, never example element values.
            string title = Label(path) + " · row " + row;
            string name = title;
            for (int suffix = 2; preset.Groups.Any(group => group.Name == name); suffix++)
                name = title + " (" + suffix + ")";
            ExcelCollectionGroup created = new ExcelCollectionGroup { Name = name, Path = path };
            preset.Groups = preset.Groups.Concat(new[] { created }).ToArray();
            return created;
        }

        /// <summary>Resolves a saved row identity while keeping arbitrary group names out of selection menus.</summary>
        /// <param name="preset">Current mappings.</param>
        /// <param name="mapping">Selected anchor.</param>
        /// <returns>The repeated collection or an empty single-cell destination.</returns>
        internal static string Collection(ExcelExchangePreset preset, ExcelCellMapping mapping)
        {
            return preset.Groups.FirstOrDefault(group => group.Name == mapping.Group)?.Path ?? string.Empty;
        }

        #endregion
        #endregion
    }
}
