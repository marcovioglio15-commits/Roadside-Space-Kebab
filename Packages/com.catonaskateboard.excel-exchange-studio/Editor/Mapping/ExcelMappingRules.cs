using System;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Rejects ambiguous layouts before a workbook can update a proposal.</summary>
    internal static class ExcelMappingRules
    {
        #region Methods
        #region Validation

        /// <summary>Checks source identity, property schema and layout consistency.</summary>
        /// <param name="context">Exact tool proposal receiving values.</param>
        /// <param name="preset">Linked exchange configuration.</param>
        /// <param name="fields">Scoped typed fields.</param>
        /// <param name="export">Whether to validate export-enabled mappings.</param>
        internal static void Validate(StudioPresetContext context, ExcelExchangePreset preset, List<ExcelField> fields, bool export)
        {
            if (context == null || !context.IsCurrent())
                throw new InvalidOperationException("Open this preset in its tool again.");
            if (preset == null || preset.Target != context.Source || preset.Scope != context.Scope)
                throw new InvalidOperationException("Choose an exchange preset linked to this exact preset and settings group.");
            if (string.IsNullOrWhiteSpace(preset.Worksheet))
                throw new InvalidOperationException("Choose a worksheet in Import.");
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExcelCollectionGroup group in preset.Groups)
                if (group == null || string.IsNullOrWhiteSpace(group.Name) || !names.Add(group.Name)
                    || !fields.Any(field => field.Collection == group.Path))
                    throw new InvalidOperationException("Each row group needs a unique name and an existing collection path.");
            HashSet<string> cells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> destinations = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (ExcelCellMapping mapping in preset.Mappings.Where(mapping => Enabled(mapping, export)))
            {
                count++;
                ExcelAddress.Parse(mapping.Cell);
                if (!cells.Add(mapping.Cell.ToUpperInvariant()))
                    throw new InvalidOperationException("Two mappings use the same anchor cell: " + preset.Worksheet + "!" + mapping.Cell);
                ExcelCollectionGroup group = mapping.Group.Length == 0 ? null : preset.Groups.FirstOrDefault(candidate => candidate.Name == mapping.Group);
                if (mapping.Group.Length > 0 && group == null)
                    throw new InvalidOperationException("Missing row group: " + mapping.Group);
                if (mapping.Kind is not (ExcelMappingKind.Data or ExcelMappingKind.ParentKey))
                    continue;
                ExcelField field = fields.FirstOrDefault(candidate => candidate.Path == mapping.Path)
                    ?? throw new InvalidOperationException("Field no longer belongs to this preset: " + mapping.Path);
                if (mapping.Kind == ExcelMappingKind.Data && !destinations.Add(mapping.Path + ":" + string.Join(",", mapping.Indices)))
                    throw new InvalidOperationException("Two columns write the same field: " + field.Label);
                if (group == null && ExcelPropertyPath.Collections(mapping.Path).Length != mapping.Indices.Length
                    || group != null && mapping.Kind == ExcelMappingKind.Data && field.Collection != group.Path)
                    throw new InvalidOperationException("Choose the correct row group or element positions for " + field.Label);
                if (mapping.Kind == ExcelMappingKind.ParentKey && (group == null
                    || !group.Path.StartsWith(field.Collection + ".", StringComparison.Ordinal)))
                    throw new InvalidOperationException("A parent key must identify a parent of its row group.");
            }
            if (count == 0)
                throw new InvalidOperationException("Map at least one cell for this operation.");
            // Related columns always share one worksheet and anchor row.
            foreach (ExcelCollectionGroup group in preset.Groups)
            {
                ExcelCellMapping[] columns = Columns(preset, group, export);
                if (columns.Length == 0)
                    continue;
                if (!columns.Any(mapping => mapping.Kind == ExcelMappingKind.Data)
                    || columns.Any(mapping => ExcelAddress.Parse(mapping.Cell).Row != ExcelAddress.Parse(columns[0].Cell).Row))
                    throw new InvalidOperationException("A row group needs data columns on the same sheet and starting row: " + group.Name);
                if (group.GuideCell.Length > 0 && ExcelAddress.Parse(group.GuideCell).Row != ExcelAddress.Parse(columns[0].Cell).Row)
                    throw new InvalidOperationException("The row guide must start on the same row as its mapped columns: " + group.Name);
                if (group.Match == ExcelMatchMode.Key && !columns.Any(mapping => mapping.Kind == ExcelMappingKind.Data && mapping.Path == group.Path + "." + group.KeyPath))
                    throw new InvalidOperationException("Map the row key " + group.KeyPath + " for " + group.Name);
                string[] parents = ExcelPropertyPath.Collections(group.Path);
                for (int index = 0; index + 1 < parents.Length; index++)
                    if (!columns.Any(mapping => mapping.Kind == ExcelMappingKind.ParentKey
                        && fields.First(field => field.Path == mapping.Path).Collection == parents[index]))
                        throw new InvalidOperationException("Map a parent key for " + parents[index]);
            }
        }

        /// <summary>Checks whether an existing mapping participates in the requested direction.</summary>
        /// <param name="mapping">Configured cell mapping.</param>
        /// <param name="export">Requested operation.</param>
        /// <returns>True when the mapping should be consumed.</returns>
        internal static bool Enabled(ExcelCellMapping mapping, bool export)
        {
            return mapping != null && (mapping.Direction == ExcelDirection.Both
                || mapping.Direction == (export ? ExcelDirection.Export : ExcelDirection.Import))
                && (export || mapping.Kind is not (ExcelMappingKind.Literal or ExcelMappingKind.Formula));
        }

        /// <summary>Collects the active columns for one shared row group.</summary>
        /// <param name="preset">Active exchange configuration.</param>
        /// <param name="group">Collection row policy.</param>
        /// <param name="export">Requested operation.</param>
        /// <returns>Columns in mapping order.</returns>
        internal static ExcelCellMapping[] Columns(ExcelExchangePreset preset, ExcelCollectionGroup group, bool export)
        {
            return preset.Mappings.Where(mapping => Enabled(mapping, export) && mapping.Group == group.Name).ToArray();
        }

        #endregion
        #endregion
    }
}
