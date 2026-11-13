using System;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Shows only settings relevant to the selected cell and collection policy.</summary>
    internal static class ExcelMappingInspector
    {
        #region Fields

        private static readonly ExcelMappingKind[] kinds = (ExcelMappingKind[])Enum.GetValues(typeof(ExcelMappingKind));
        private static readonly string[] kindLabels = { "Preset Field", "Parent Key", "Export Text", "Export Formula" };

        #endregion
        #region Methods
        #region Selection

        /// <summary>Edits one anchor and its shared collection settings.</summary>
        /// <param name="preset">Detached mapping configuration.</param>
        /// <param name="fields">Scoped field catalog.</param>
        /// <param name="layout">Current grid selection.</param>
        internal static void Draw(ExcelExchangePreset preset, List<ExcelField> fields, ExcelLayoutView layout)
        {
            EditorGUILayout.LabelField("Selected Cell", EditorStyles.boldLabel);
            ExcelLayoutSections.Horizontal();
            if (layout.SelectedMapping < 0 || layout.SelectedMapping >= preset.Mappings.Length)
            {
                EditorGUILayout.LabelField("Left-click applies the brush. Right-click a cell to inspect its mapping.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            ExcelCellMapping mapping = preset.Mappings[layout.SelectedMapping];
            EditorGUILayout.LabelField(ExcelLayoutSchema.Caption(mapping, fields), EditorStyles.wordWrappedLabel);
            if (mapping.Path.Length > 0)
                EditorGUILayout.SelectableLabel(mapping.Path, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(42f));
            ExcelLayoutSections.Horizontal();
            mapping.Cell = StudioFieldGUI.TextField(new GUIContent("Cell", "First cell for this value or repeated column."), mapping.Cell);
            int kind = StudioFieldGUI.Popup(new GUIContent("Content", "Choose a preset field, parent key, export text or formula."), Array.IndexOf(kinds, mapping.Kind), kindLabels);
            if (kind >= 0)
                mapping.Kind = kinds[kind];
            mapping.Direction = (ExcelDirection)StudioFieldGUI.Popup(new GUIContent("Direction", "Use this mapping for Import, Export or both."),
                (int)mapping.Direction, Enum.GetNames(typeof(ExcelDirection)));
            ExcelField field = null;
            if (mapping.Kind is ExcelMappingKind.Data or ExcelMappingKind.ParentKey)
            {
                string collection = ExcelLayoutSchema.Collection(preset, mapping);
                List<ExcelField> choices = fields.Where(candidate => collection.Length == 0
                    || (mapping.Kind == ExcelMappingKind.Data ? candidate.Collection == collection
                        : candidate.Collection.Length > 0 && collection.StartsWith(candidate.Collection + ".", StringComparison.Ordinal))).ToList();
                string[] names = choices.Select(candidate => candidate.Label).ToArray();
                int selected = choices.FindIndex(candidate => candidate.Path == mapping.Path);
                int next = StudioFieldGUI.Popup(new GUIContent("Field", "Logical property inside the linked preset."), selected, names);
                if (next >= 0 && next < choices.Count)
                {
                    mapping.Path = choices[next].Path;
                    field = choices[next];
                    if (mapping.Group.Length == 0 && next != selected)
                        mapping.Indices = new int[ExcelPropertyPath.Collections(mapping.Path).Length];
                }
                if (field != null && ExcelPropertyPath.ElementType(field.Type) != null)
                    mapping.Separator = StudioFieldGUI.TextField(new GUIContent("Separator", "List delimiter. Quoted items may contain the delimiter."), mapping.Separator);
                if (mapping.Kind == ExcelMappingKind.Data && mapping.Direction != ExcelDirection.Export)
                    mapping.KeepEmpty = StudioFieldGUI.Toggle(new GUIContent("Keep Empty", "Leave the current value unchanged when this cell is empty. Disable to clear text or lists."), mapping.KeepEmpty);
            }
            if (mapping.Kind is ExcelMappingKind.Literal or ExcelMappingKind.Formula)
                mapping.Text = StudioFieldGUI.TextField(new GUIContent(mapping.Kind == ExcelMappingKind.Formula ? "Formula" : "Text", "Workbook content written during export."), mapping.Text);
            DrawRepetition(preset, fields, mapping, field);
            if (field != null && mapping.Group.Length == 0)
            {
                string[] collections = ExcelPropertyPath.Collections(field.Path);
                for (int index = 0; index < Math.Min(collections.Length, mapping.Indices.Length); index++)
                    mapping.Indices[index] = StudioFieldGUI.IntField(new GUIContent("Element " + (index + 1), "Zero-based position in " + collections[index]), mapping.Indices[index], 0, 100);
            }
            if (mapping.Group.Length > 0)
            {
                ExcelLayoutSections.Horizontal();
                DrawGroup(preset.Groups.First(group => group.Name == mapping.Group), fields);
            }
            GUILayout.FlexibleSpace();
            if (StudioButton.Draw(new GUIContent("Remove Mapping", "Remove this anchor. Undo restores it.")))
            {
                int remove = layout.SelectedMapping;
                preset.Mappings = preset.Mappings.Where((candidate, index) => index != remove).ToArray();
                preset.Groups = preset.Groups.Where(group => preset.Mappings.Any(candidate => candidate.Group == group.Name)).ToArray();
                layout.SelectedMapping = -1;
                GUI.changed = true;
            }
        }

        #endregion
        #region Collections

        /// <summary>Chooses repetition from declared collections, rather than names of saved example groups.</summary>
        /// <param name="preset">Current mapping draft.</param>
        /// <param name="fields">Scoped declarations.</param>
        /// <param name="mapping">Selected anchor.</param>
        /// <param name="field">Optional data field.</param>
        private static void DrawRepetition(ExcelExchangePreset preset, List<ExcelField> fields, ExcelCellMapping mapping, ExcelField field)
        {
            string[] paths = new[] { string.Empty }.Concat(ExcelLayoutSchema.Collections(fields).Where(path => field == null
                || (mapping.Kind == ExcelMappingKind.ParentKey ? field.Collection.Length > 0 && path.StartsWith(field.Collection + ".", StringComparison.Ordinal)
                    : path == field.Collection))).ToArray();
            string[] labels = paths.Select(path => path.Length == 0 ? "Single cell" : ExcelLayoutSchema.CollectionLabel(path, fields)).ToArray();
            int current = Array.IndexOf(paths, ExcelLayoutSchema.Collection(preset, mapping));
            int next = StudioFieldGUI.Popup(new GUIContent("Repeat For", "Each row supplies one item of this collection. Related columns on the same starting row share their settings."), current, labels);
            if (next == current || next < 0)
                return;
            if (!ExcelAddress.TryParse(mapping.Cell, out (int Row, int Column) position))
                return;
            mapping.Group = paths[next].Length > 0 ? ExcelLayoutSchema.Bind(preset, paths[next], mapping.Cell).Name : string.Empty;
            mapping.Indices = paths[next].Length > 0 ? Array.Empty<int>() : new int[ExcelPropertyPath.Collections(mapping.Path).Length];
        }

        /// <summary>Edits row settings once for every column of the same collection.</summary>
        /// <param name="group">Shared collection policy.</param>
        /// <param name="fields">Scoped type metadata.</param>
        private static void DrawGroup(ExcelCollectionGroup group, List<ExcelField> fields)
        {
            EditorGUILayout.LabelField("Shared Column Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(ExcelLayoutSchema.CollectionLabel(group.Path, fields), EditorStyles.wordWrappedLabel);
            group.Match = (ExcelMatchMode)StudioFieldGUI.Popup(new GUIContent("Match Rows", "Position supports renaming values. Key supports reordering rows."),
                (int)group.Match, new[] { "By Position", "By Key" });
            if (group.Match == ExcelMatchMode.Key)
            {
                string[] keys = fields.Where(field => field.Collection == group.Path && ExcelPropertyPath.ElementType(field.Type) == null)
                    .Select(field => field.Path.Substring(group.Path.Length + 1)).ToArray();
                int key = StudioFieldGUI.Popup(new GUIContent("Row Key", "Unique element property mapped into a data column."), Array.IndexOf(keys, group.KeyPath), keys);
                if (key >= 0 && key < keys.Length)
                    group.KeyPath = keys[key];
            }
            group.GuideCell = StudioFieldGUI.TextField(new GUIContent("Row Guide", "Column anchor defining row presence. Empty uses the key or first data column."), group.GuideCell);
            group.Extent = (ExcelRowExtent)StudioFieldGUI.Popup(new GUIContent("Read Until", "Choose the boundary shared by every mapped column."),
                (int)group.Extent, new[] { "First Empty Key", "Last Row", "Excel Table" });
            switch (group.Extent)
            {
                case ExcelRowExtent.LastRow:
                    group.LastRow = StudioFieldGUI.IntField(new GUIContent("Last Row", "Final included worksheet row."), group.LastRow, 2, 1000);
                    break;
                case ExcelRowExtent.ExcelTable:
                    group.TableName = StudioFieldGUI.TextField(new GUIContent("Table", "Exact named Excel table providing the boundary."), group.TableName);
                    break;
            }
        }

        #endregion
        #endregion
    }
}
