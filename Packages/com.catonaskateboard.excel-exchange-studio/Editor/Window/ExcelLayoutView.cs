using System;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Maps declared preset fields on a blank worksheet without opening the selected workbook.</summary>
    internal sealed class ExcelLayoutView
    {
        #region Fields

        internal ExcelField SelectedField;
        internal int SelectedMapping = -1;
        internal string Collection = string.Empty;
        internal Action Changed;
        private string selectedCell = "A1";
        private ExcelCollectionPolicy importMode;
        private ExcelExchangePreset activePreset;
        private readonly Action<bool, bool, int> editStructure;
        private string search = string.Empty;
        private int filter;
        private int mappedFilter;
        private int fieldRole;
        private List<ExcelField> cachedFields;
        private string[] collections = Array.Empty<string>();
        private string[] collectionLabels = Array.Empty<string>();
        private int brush;
        private int cellWidth = 180;
        private bool gridSettings;
        private Vector2 fieldScroll;
        private Vector2 inspectorScroll;
        private GUIStyle fieldStyle;
        private readonly ExcelLayoutGrid grid = new ExcelLayoutGrid();
        private static readonly string[] filters = { "All fields", "Text", "Numbers", "Booleans", "References", "Lists", "Enums", "Vectors & Colors" };
        private static readonly string[] brushes = { "Preset Field", "Export Text", "Export Formula", "Erase" };
        private static readonly string[] mappingFilters = { "All fields", "Mapped", "Unmapped" };

        #endregion
        #region Methods
        #region Construction

        /// <summary>Caches the structural callback shared by buttons and separator menus.</summary>
        internal ExcelLayoutView()
        {
            editStructure = EditStructure;
        }

        #endregion
        #region Layout

        /// <summary>Draws destination choices, declared fields and anchor settings independently of workbook values.</summary>
        /// <param name="preset">Detached mapping configuration.</param>
        /// <param name="fields">Cached target declarations.</param>
        /// <param name="status">Receives concise interaction feedback.</param>
        internal void Draw(ExcelExchangePreset preset, List<ExcelField> fields, ref string status)
        {
            activePreset = preset;
            if (cachedFields != fields)
            {
                // Type declarations remain available even when collections contain no items.
                cachedFields = fields;
                collections = new[] { string.Empty }.Concat(ExcelLayoutSchema.Collections(fields)).ToArray();
                collectionLabels = collections.Select(path => path.Length == 0 ? "Single values" : ExcelLayoutSchema.CollectionLabel(path, fields)).ToArray();
            }
            using EditorGUILayout.HorizontalScope panes = new EditorGUILayout.HorizontalScope();
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(260f)))
            {
                using StudioFieldLayout.WidthScope width = new StudioFieldLayout.WidthScope(260f);
                DrawDestination(preset, fields);
                ExcelLayoutSections.Horizontal();
                DrawFields(fields, preset);
            }
            ExcelLayoutSections.Vertical();
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawToolbar(preset);
                DrawGridSettings(preset);
                ExcelLayoutSections.Horizontal();
                if (preset.Rows < 1 || preset.Columns < 1 || cellWidth < 1)
                    EditorGUILayout.LabelField("Use positive grid dimensions.", EditorStyles.wordWrappedMiniLabel);
                else if (grid.Draw(preset, fields, cellWidth, SelectedMapping, editStructure, out string cell, out int index, out bool select))
                    Click(preset, cell, index, select, ref status);
            }
            ExcelLayoutSections.Vertical();
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(330f)))
            {
                using StudioFieldLayout.WidthScope width = new StudioFieldLayout.WidthScope(330f);
                using EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll);
                inspectorScroll = scroll.scrollPosition;
                ExcelMappingInspector.Draw(preset, fields, this);
            }
        }

        /// <summary>Lists collections from the preset type, including those not mapped yet.</summary>
        /// <param name="preset">Current mappings and import policies.</param>
        /// <param name="fields">Collection captions and instructions.</param>
        private void DrawDestination(ExcelExchangePreset preset, List<ExcelField> fields)
        {
            EditorGUILayout.LabelField("Mapping Destination", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(new GUIContent("Rows Represent", "Each spreadsheet row supplies one item of this collection. Single values address explicit cells."));
            int current = Array.IndexOf(collections, Collection);
            int next = StudioFieldGUI.Popup(new GUIContent(string.Empty, "Choose a collection declared by this preset."), Math.Max(0, current), collectionLabels);
            if (next != current && next >= 0)
            {
                Collection = collections[next];
                SelectedField = null;
                fieldRole = 0;
                importMode = preset.Groups.FirstOrDefault(group => group.Path == Collection)?.Policy ?? ExcelCollectionPolicy.UpdateExisting;
            }
            EditorGUILayout.LabelField(Collection.Length > 0
                ? fields.FirstOrDefault(field => field.Collection == Collection)?.CollectionTooltip ?? "One collection item per row."
                : "One property per cell. Choose positions for array items.", EditorStyles.wordWrappedMiniLabel);
            if (Collection.Length == 0)
                return;
            ExcelLayoutSections.Horizontal();
            ExcelCollectionPolicy previous = preset.Groups.FirstOrDefault(group => group.Path == Collection)?.Policy ?? importMode;
            importMode = ExcelCollectionControls.Draw(previous);
            if (importMode != previous)
                foreach (ExcelCollectionGroup group in preset.Groups.Where(group => group.Path == Collection))
                    group.Policy = importMode;
        }

        /// <summary>Keeps the brush label beside its control and exposes structural edits for the selected cell.</summary>
        /// <param name="preset">Current grid dimensions.</param>
        private void DrawToolbar(ExcelExchangePreset preset)
        {
            EditorGUILayout.LabelField("Mapping Grid", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Brush", "Left-click applies the brush. Right-click selects a cell."), GUILayout.Width(44f));
                brush = EditorGUILayout.Popup(brush, brushes, GUILayout.Width(180f));
                if (brush == 0 && SelectedField != null)
                    GUILayout.Label(new GUIContent(SelectedField.Label, SelectedField.Tooltip), EditorStyles.wordWrappedMiniLabel);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(selectedCell, "Right-click a cell to choose the row and column for these edits."), GUILayout.Width(42f));
                (int Row, int Column) cell = ExcelAddress.Parse(selectedCell);
                if (StudioButton.Draw(new GUIContent("+ Row", "Insert a row before the selected cell. Move mapping anchors; leave the workbook unchanged.")))
                    EditStructure(true, true, cell.Row);
                using (new EditorGUI.DisabledScope(preset.Rows <= 1))
                    if (StudioButton.Draw(new GUIContent("− Row", "Remove the selected row and its mappings. Undo restores the edit.")))
                        EditStructure(true, false, cell.Row);
                if (StudioButton.Draw(new GUIContent("+ Column", "Insert a column before the selected cell. Move mapping anchors; leave the workbook unchanged.")))
                    EditStructure(false, true, cell.Column);
                using (new EditorGUI.DisabledScope(preset.Columns <= 1))
                    if (StudioButton.Draw(new GUIContent("− Column", "Remove the selected column and its mappings. Undo restores the edit.")))
                        EditStructure(false, false, cell.Column);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Keeps view dimensions separate from collection import boundaries.</summary>
        /// <param name="preset">Saved mapping grid dimensions.</param>
        private void DrawGridSettings(ExcelExchangePreset preset)
        {
            gridSettings = EditorGUILayout.Foldout(gridSettings, new GUIContent("Grid View", "Display settings only. Imported rows follow the mapping boundary."), true);
            if (!gridSettings)
                return;
            preset.Rows = StudioFieldGUI.IntField(new GUIContent("Visible Rows", "Grid extent. Does not limit imported items."), preset.Rows, 1, 200);
            preset.Columns = StudioFieldGUI.IntField(new GUIContent("Visible Columns", "Grid columns. Scroll horizontally for additional anchors."), preset.Columns, 1, 30);
            cellWidth = StudioFieldGUI.IntField(new GUIContent("Cell Width", "Widen cells to display longer field paths."), cellWidth, 160, 360);
        }

        /// <summary>Shows collection values and clearly labelled ancestor keys from the cached schema.</summary>
        /// <param name="fields">Declared preset properties.</param>
        /// <param name="preset">Current mappings used by field markers.</param>
        private void DrawFields(List<ExcelField> fields, ExcelExchangePreset preset)
        {
            EditorGUILayout.LabelField("Preset Fields", EditorStyles.boldLabel);
            bool nested = ExcelPropertyPath.Collections(Collection).Length > 1;
            if (nested)
                fieldRole = StudioFieldGUI.Popup(new GUIContent("Fields", "Item properties update this collection. Parent keys identify which parent receives each row."), fieldRole,
                    new[] { "Item properties", "Parent keys" });
            search = StudioFieldGUI.TextField(new GUIContent("Search", "Match all words against field names, paths and tooltips."), search);
            filter = StudioFieldGUI.Popup(new GUIContent("Value Type", "Show only compatible field types."), filter, filters);
            mappedFilter = StudioFieldGUI.Popup(new GUIContent("Mapping", "Show all fields or only mapped or unmapped properties."), mappedFilter, mappingFilters);
            string[] tokens = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            fieldStyle ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 0f, wordWrap = true,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4) };
            using EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(fieldScroll);
            fieldScroll = scroll.scrollPosition;
            // Filter item values separately from ancestor fields used to link nested rows.
            foreach (ExcelField field in fields)
            {
                bool parent = Collection.Length > 0 && field.Collection.Length > 0 && Collection.StartsWith(field.Collection + ".", StringComparison.Ordinal);
                if (Collection.Length > 0 && (nested && fieldRole == 1 ? !parent || ExcelPropertyPath.ElementType(field.Type) != null : field.Collection != Collection) || !Matches(field)
                    || tokens.Any(token => (field.Label + " " + field.Path + " " + field.Tooltip).IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                bool mapped = preset.Mappings.Any(mapping => mapping.Kind is ExcelMappingKind.Data or ExcelMappingKind.ParentKey && mapping.Path == field.Path);
                if (mappedFilter == 1 && !mapped || mappedFilter == 2 && mapped)
                    continue;
                string caption = Collection.Length > 0 && !parent ? ExcelLayoutSchema.Label(field.Path.Substring(Collection.Length + 1)) : field.Label;
                GUIContent label = new GUIContent((mapped ? "● " : string.Empty) + caption + (parent ? " (parent key)" : string.Empty),
                    field.Tooltip + "\n" + field.Path + "\nSelect, then click its first cell.");
                Rect rect = GUILayoutUtility.GetRect(label, fieldStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32f));
                if (SelectedField == field)
                    EditorGUI.DrawRect(rect, new Color(0.25f, 0.55f, 0.75f, 0.35f));
                if (GUI.Button(rect, label, fieldStyle))
                {
                    SelectedField = field;
                    brush = 0;
                }
            }
        }

        /// <summary>Checks a field's declared value family without reading its current value.</summary>
        /// <param name="field">Cached declaration.</param>
        /// <returns>Whether this field matches the selected filter.</returns>
        private bool Matches(ExcelField field)
        {
            return filter switch
            {
                1 => field.Type == typeof(string),
                2 => field.Type.IsPrimitive && field.Type != typeof(bool) && field.Type != typeof(char),
                3 => field.Type == typeof(bool),
                4 => typeof(UnityEngine.Object).IsAssignableFrom(field.Type),
                5 => ExcelPropertyPath.ElementType(field.Type) != null,
                6 => field.Type.IsEnum,
                7 => field.Type.IsValueType && !field.Type.IsPrimitive && !field.Type.IsEnum,
                _ => true
            };
        }

        #endregion
        #region Mapping

        /// <summary>Restores the destination from a selected mapping without inspecting workbook values.</summary>
        /// <param name="preset">Saved configuration or null for an empty selection.</param>
        internal void Select(ExcelExchangePreset preset)
        {
            SelectedMapping = -1;
            SelectedField = null;
            Collection = preset?.Groups.FirstOrDefault()?.Path ?? string.Empty;
            importMode = preset?.Groups.FirstOrDefault()?.Policy ?? ExcelCollectionPolicy.UpdateExisting;
            fieldRole = 0;
            fieldScroll = Vector2.zero;
            selectedCell = "A1";
        }

        /// <summary>Places a generic anchor or selects the existing column, including its continuation.</summary>
        /// <param name="preset">Detached mapping configuration.</param>
        /// <param name="cell">Clicked Excel address.</param>
        /// <param name="existing">Existing anchor index or minus one.</param>
        /// <param name="select">Whether the right button requested selection.</param>
        /// <param name="status">Receives concise next-step guidance.</param>
        private void Click(ExcelExchangePreset preset, string cell, int existing, bool select, ref string status)
        {
            selectedCell = cell;
            if (select)
            {
                SelectedMapping = existing;
                if (existing < 0)
                    return;
                Collection = ExcelLayoutSchema.Collection(preset, preset.Mappings[existing]);
                fieldRole = preset.Mappings[existing].Kind == ExcelMappingKind.ParentKey ? 1 : 0;
                return;
            }
            if (brush == 3)
            {
                if (existing >= 0)
                    preset.Mappings = preset.Mappings.Where((mapping, index) => index != existing).ToArray();
                preset.Groups = preset.Groups.Where(group => preset.Mappings.Any(mapping => mapping.Group == group.Name)).ToArray();
                SelectedMapping = -1;
                GUI.changed = true;
                return;
            }
            if (brush == 0 && SelectedField == null)
            {
                status = "Select a preset field, then click its first spreadsheet cell.";
                return;
            }
            ExcelCellMapping added = new ExcelCellMapping { Cell = cell,
                Kind = brush switch { 1 => ExcelMappingKind.Literal, 2 => ExcelMappingKind.Formula, _ => ExcelMappingKind.Data } };
            if (brush == 0)
            {
                added.Path = SelectedField.Path;
                if (Collection.Length > 0 && SelectedField.Collection != Collection)
                    added.Kind = ExcelMappingKind.ParentKey;
                if (Collection.Length == 0)
                    added.Indices = new int[ExcelPropertyPath.Collections(added.Path).Length];
            }
            if (Collection.Length > 0 && added.Kind is ExcelMappingKind.Data or ExcelMappingKind.ParentKey)
            {
                ExcelCollectionGroup group = ExcelLayoutSchema.Bind(preset, Collection, cell);
                group.Policy = importMode;
                added.Group = group.Name;
            }
            if (existing >= 0)
                preset.Mappings[existing] = added;
            else
                preset.Mappings = preset.Mappings.Concat(new[] { added }).ToArray();
            SelectedMapping = existing >= 0 ? existing : preset.Mappings.Length - 1;
            preset.Groups = preset.Groups.Where(group => preset.Mappings.Any(mapping => mapping.Group == group.Name)).ToArray();
            GUI.changed = true;
        }

        /// <summary>Shares structural editing between toolbar buttons and separator context menus.</summary>
        /// <param name="row">Whether to edit rows.</param>
        /// <param name="insert">Whether to insert a new coordinate.</param>
        /// <param name="position">One-based affected coordinate.</param>
        private void EditStructure(bool row, bool insert, int position)
        {
            if (activePreset == null)
                return;
            ExcelCellMapping selected = SelectedMapping >= 0 && SelectedMapping < activePreset.Mappings.Length ? activePreset.Mappings[SelectedMapping] : null;
            try
            {
                ExcelGridStructure.Change(activePreset, row, insert, position);
                SelectedMapping = Array.IndexOf(activePreset.Mappings, selected);
                selectedCell = ExcelAddress.Format(Math.Min(ExcelAddress.Parse(selectedCell).Row, activePreset.Rows), Math.Min(ExcelAddress.Parse(selectedCell).Column, activePreset.Columns));
                Changed?.Invoke();
                GUI.changed = true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException)
            {
                Debug.LogWarning(exception.Message);
            }
        }

        #endregion
        #endregion
    }
}
