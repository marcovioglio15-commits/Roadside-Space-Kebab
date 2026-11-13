using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Resolves nested parents and applies one collection policy per group.</summary>
    internal static class ExcelCollectionImporter
    {
        #region Types

        /// <summary>Retains rows belonging to one exact parent collection.</summary>
        private sealed class Bucket
        {
            internal readonly int[] Parents;
            internal readonly List<int> Rows = new List<int>();

            #region Methods

            /// <summary>Captures parent indices resolved from explicit workbook keys.</summary>
            /// <param name="parents">Outer collection element positions.</param>
            internal Bucket(int[] parents)
            {
                Parents = parents;
            }

            #endregion
        }

        #endregion
        #region Methods
        #region Grouping

        /// <summary>Groups source rows by their parent and updates only those collections.</summary>
        /// <param name="preview">Detached import proposal.</param>
        /// <param name="group">Collection matching and replacement policy.</param>
        /// <param name="columns">Shared source columns.</param>
        /// <param name="sheets">Source workbook.</param>
        /// <param name="fields">Scoped schema.</param>
        /// <param name="occupied">Already mapped expanded coordinates.</param>
        internal static void Apply(ExcelImportPreview preview, ExcelCollectionGroup group, ExcelCellMapping[] columns,
            Dictionary<string, ExcelSheet> sheets, List<ExcelField> fields, HashSet<string> occupied)
        {
            if (!sheets.TryGetValue(preview.Preset.Worksheet, out ExcelSheet sheet))
                throw new FormatException("Worksheet not found: " + preview.Preset.Worksheet);
            Dictionary<string, Bucket> buckets = new Dictionary<string, Bucket>(StringComparer.Ordinal);
            string[] collections = ExcelPropertyPath.Collections(group.Path);
            foreach (int row in ExcelRowRange.Read(group, columns, sheet))
            {
                List<int> parents = new List<int>();
                for (int depth = 0; depth + 1 < collections.Length; depth++)
                {
                    ExcelCellMapping key = columns.First(mapping => mapping.Kind == ExcelMappingKind.ParentKey
                        && fields.First(field => field.Path == mapping.Path).Collection == collections[depth]);
                    ExcelField field = fields.First(candidate => candidate.Path == key.Path);
                    object incoming = ExcelValueCodec.Read(sheet.Get(row, ExcelAddress.Parse(key.Cell).Column), field.Type,
                        key.Separator, preview.Preset.UseFormulaResults);
                    IList candidates = (IList)ExcelPropertyPath.Get(preview.Proposal, collections[depth], parents.ToArray());
                    int found = -1;
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        int[] indices = parents.Concat(new[] { index }).ToArray();
                        if (!Equals(ExcelPropertyPath.Get(preview.Proposal, key.Path, indices), incoming))
                            continue;
                        if (found >= 0)
                            throw new FormatException("Duplicate parent key: " + sheet.Get(row, ExcelAddress.Parse(key.Cell).Column).Text);
                        found = index;
                    }
                    if (found < 0)
                        throw new FormatException("Parent not found at " + ExcelAddress.Format(row, ExcelAddress.Parse(key.Cell).Column)
                            + ": " + sheet.Get(row, ExcelAddress.Parse(key.Cell).Column).Text);
                    parents.Add(found);
                }
                string identity = string.Join(",", parents);
                if (!buckets.TryGetValue(identity, out Bucket bucket))
                    buckets.Add(identity, bucket = new Bucket(parents.ToArray()));
                bucket.Rows.Add(row);
            }
            if (buckets.Count == 0 && group.Policy == ExcelCollectionPolicy.ReplaceCollection)
            {
                // An empty nested import needs an explicit parent; never clear every parent's list.
                if (collections.Length == 1)
                    buckets.Add(string.Empty, new Bucket(Array.Empty<int>()));
                else
                    throw new FormatException("An empty worksheet cannot identify a parent. Import the parent collection to replace it. No collections were cleared.");
            }
            foreach (Bucket bucket in buckets.Values)
                ApplyBucket(preview, group, columns, sheets, sheet, fields, occupied, bucket);
        }

        #endregion
        #region Collections

        /// <summary>Resolves element identity once, then writes every mapped column for those elements.</summary>
        /// <param name="preview">Detached transaction.</param>
        /// <param name="group">Collection policy.</param>
        /// <param name="columns">Active mapped columns.</param>
        /// <param name="sheets">Workbook sheet lookup.</param>
        /// <param name="sheet">Current source sheet.</param>
        /// <param name="fields">Field type metadata.</param>
        /// <param name="occupied">Expanded cell ownership.</param>
        /// <param name="bucket">Rows for one exact parent.</param>
        private static void ApplyBucket(ExcelImportPreview preview, ExcelCollectionGroup group, ExcelCellMapping[] columns,
            Dictionary<string, ExcelSheet> sheets, ExcelSheet sheet, List<ExcelField> fields, HashSet<string> occupied, Bucket bucket)
        {
            IList current = (IList)ExcelPropertyPath.Get(preview.Proposal, group.Path, bucket.Parents);
            List<object> previous = current == null ? new List<object>() : current.Cast<object>().ToList();
            Type collectionType = DeclaredType(preview.Proposal.GetType(), group.Path);
            Type elementType = ExcelPropertyPath.ElementType(collectionType);
            List<object> elements = group.Policy == ExcelCollectionPolicy.ReplaceCollection ? new List<object>()
                : current == null ? new List<object>() : current.Cast<object>().ToList();
            int originalCount = current?.Count ?? 0;
            if (group.Match == ExcelMatchMode.Position && group.Policy == ExcelCollectionPolicy.UpdateExisting && originalCount != bucket.Rows.Count)
                throw new FormatException(group.Name + ": workbook has " + bucket.Rows.Count + " rows; preset has " + originalCount
                    + ". Choose Merge or Replace Collection, then preview again.");
            ExcelCellMapping key = group.Match == ExcelMatchMode.Key ? columns.First(mapping => mapping.Path == group.Path + "." + group.KeyPath) : null;
            Type keyType = key == null ? null : fields.First(field => field.Path == key.Path).Type;
            Dictionary<string, int> existing = new Dictionary<string, int>(StringComparer.Ordinal);
            if (key != null)
                for (int index = 0; index < previous.Count; index++)
                {
                    object value = ExcelPropertyPath.Get(previous[index], group.KeyPath);
                    string identity = ExcelValueCodec.Format(value, keyType);
                    if (!existing.TryAdd(identity, index) && group.Policy != ExcelCollectionPolicy.ReplaceCollection)
                        throw new FormatException("Duplicate existing key in " + group.Name + ": " + identity);
                }
            HashSet<string> incomingKeys = new HashSet<string>(StringComparer.Ordinal);
            List<int> destinations = new List<int>();
            List<int> originals = new List<int>();
            for (int position = 0; position < bucket.Rows.Count; position++)
            {
                int destination = position;
                int original = position < previous.Count ? position : -1;
                if (key != null)
                {
                    ExcelCell cell = sheet.Get(bucket.Rows[position], ExcelAddress.Parse(key.Cell).Column);
                    string identity = ExcelValueCodec.Format(ExcelValueCodec.Read(cell, keyType, key.Separator, preview.Preset.UseFormulaResults), keyType);
                    if (identity.Length == 0 || !incomingKeys.Add(identity))
                        throw new FormatException("Empty or duplicate row key in " + group.Name + ": " + identity);
                    original = existing.TryGetValue(identity, out int matched) ? matched : -1;
                    destination = group.Policy == ExcelCollectionPolicy.ReplaceCollection ? elements.Count : original >= 0 ? original : elements.Count;
                }
                if (destination >= elements.Count)
                {
                    if (group.Policy == ExcelCollectionPolicy.UpdateExisting)
                        throw new FormatException("Row key not found in " + group.Name + ". Choose Merge or Replace Collection.");
                    elements.Add(Activator.CreateInstance(elementType));
                }
                destinations.Add(destination);
                originals.Add(original);
            }
            ExcelPropertyPath.Set(preview.Proposal, group.Path, ExcelPropertyPath.Collection(collectionType, elements), bucket.Parents);
            if (group.Policy == ExcelCollectionPolicy.ReplaceCollection || elements.Count != originalCount)
                preview.Rows.Add(new ExcelPreviewRow(group.Name, group.Path, originalCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    elements.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), group.Policy == ExcelCollectionPolicy.ReplaceCollection
                        ? "Collection rebuilt. Every unmapped field uses its declared default." : "New elements use declared defaults."));
            for (int position = 0; position < bucket.Rows.Count; position++)
            {
                int[] indices = bucket.Parents.Concat(new[] { destinations[position] }).ToArray();
                object original = originals[position] >= 0 ? previous[originals[position]] : null;
                if (group.Policy == ExcelCollectionPolicy.ReplaceCollection && original != null)
                    AddDefaults(preview, group, columns, fields, original, elements[destinations[position]], bucket.Rows[position]);
                foreach (ExcelCellMapping mapping in columns)
                {
                    string before = mapping.Kind == ExcelMappingKind.Data ? ExcelValueCodec.Format(
                        original == null ? null : ExcelPropertyPath.Get(original, mapping.Path.Substring(group.Path.Length + 1)),
                        fields.First(field => field.Path == mapping.Path).Type, mapping.Separator) : null;
                    ExcelImportService.ApplyCell(preview, mapping, sheets, fields, bucket.Rows[position], indices, occupied, before);
                }
            }
        }

        /// <summary>Lists actual unmapped default resets beside the structural replacement notice.</summary>
        /// <param name="preview">Review receiving reset differences.</param>
        /// <param name="group">Rebuilt collection.</param>
        /// <param name="columns">Explicitly mapped values.</param>
        /// <param name="fields">Scoped schema.</param>
        /// <param name="before">Existing matched element.</param>
        /// <param name="after">Fresh element using declared defaults.</param>
        /// <param name="row">Source row identifying the replacement.</param>
        private static void AddDefaults(ExcelImportPreview preview, ExcelCollectionGroup group, ExcelCellMapping[] columns,
            List<ExcelField> fields, object before, object after, int row)
        {
            foreach (ExcelField field in fields.Where(field => field.Collection == group.Path
                && !columns.Any(mapping => mapping.Kind == ExcelMappingKind.Data && mapping.Path == field.Path)))
            {
                string relative = field.Path.Substring(group.Path.Length + 1);
                string original = ExcelValueCodec.Format(ExcelPropertyPath.Get(before, relative), field.Type);
                string replacement = ExcelValueCodec.Format(ExcelPropertyPath.Get(after, relative), field.Type);
                if (original != replacement)
                    preview.Rows.Add(new ExcelPreviewRow("Row " + row, field.Label, original, replacement, "Reset to default."));
            }
        }

        /// <summary>Resolves a declared collection type even when its current value is null.</summary>
        /// <param name="type">Typed root document.</param>
        /// <param name="path">Logical collection path.</param>
        /// <returns>The declared array or list type.</returns>
        internal static Type DeclaredType(Type type, string path)
        {
            string[] segments = path.Split('.');
            for (int index = 0; index < segments.Length; index++)
            {
                bool repeated = segments[index].EndsWith("[]", StringComparison.Ordinal);
                type = ExcelPropertyPath.Field(type, repeated ? segments[index].Substring(0, segments[index].Length - 2) : segments[index]).FieldType;
                if (repeated && index + 1 < segments.Length)
                    type = ExcelPropertyPath.ElementType(type);
            }
            return type;
        }

        #endregion
        #endregion
    }
}
