using System;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Reviews import errors first, then groups field changes and default resets by item.</summary>
    internal sealed class ExcelPreviewView
    {
        #region Types

        /// <summary>Retains expansion state for related changes or one column's errors.</summary>
        private sealed class Group
        {
            internal readonly string Title;
            internal readonly List<ExcelPreviewRow> Rows = new List<ExcelPreviewRow>();
            internal bool Expanded;
            internal bool Defaults;

            #region Methods

            /// <summary>Names one review group.</summary>
            /// <param name="title">Item or field name.</param>
            internal Group(string title)
            {
                // Keep expansion state beside the rows it controls.
                Title = title;
            }

            #endregion
        }

        #endregion
        #region Fields

        private ExcelImportPreview current;
        private readonly List<Group> errors = new List<Group>();
        private readonly List<Group> items = new List<Group>();
        private readonly List<ExcelPreviewRow> lists = new List<ExcelPreviewRow>();
        private Vector2 scroll;
        private bool changesOnly = true;
        private bool errorsOnly;
        private bool listDetails;
        private int errorCount;
        private int changed;
        private int defaults;
        private GUIStyle errorStyle;

        #endregion
        #region Methods
        #region Review

        /// <summary>Shows actionable corrections before the per-item comparison.</summary>
        /// <param name="preview">Completed import proposal.</param>
        /// <param name="edit">Opens the affected mapping in Layout.</param>
        internal void Draw(ExcelImportPreview preview, Action<ExcelPreviewRow> edit)
        {
            // Rebuild groups only when the proposal changes.
            if (current != preview)
                Build(preview);
            errorStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = new Color(1f, 0.5f, 0.4f) } };
            EditorGUILayout.LabelField(preview.Imported ? "Imported into the preset tool. Use Apply there to save."
                : errorCount > 0 ? errors.Count + " fields need attention (" + errorCount + " cells)."
                : changed + " field changes in " + items.Count + " items", EditorStyles.boldLabel);
            if (defaults > 0)
                EditorGUILayout.LabelField(defaults + " other fields will reset to their default values. Expand an item to review them.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                changesOnly = EditorGUILayout.ToggleLeft(new GUIContent("Changes Only", "Hide fields that already have the same value."), changesOnly, GUILayout.Width(140f));
                if (errorCount > 0)
                    errorsOnly = EditorGUILayout.ToggleLeft(new GUIContent("Errors Only", "Show only the cells that need correcting."), errorsOnly, GUILayout.Width(140f));
            }
            using EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            // Show corrections before list changes and ordinary value comparisons.
            foreach (Group group in errors)
                DrawError(group, edit);
            if (errorsOnly && errorCount > 0)
                return;
            if (preview.Warnings.Count > 0)
            {
                ExcelLayoutSections.Horizontal();
                EditorGUILayout.LabelField("Check in the preset tool", EditorStyles.boldLabel);
                foreach (string warning in preview.Warnings)
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedLabel);
            }
            if (lists.Count > 0)
            {
                ExcelLayoutSections.Horizontal();
                listDetails = EditorGUILayout.Foldout(listDetails, new GUIContent("List changes (" + lists.Count + ")", "See which lists will grow or be replaced."), true);
                if (listDetails)
                    foreach (ExcelPreviewRow row in lists)
                    {
                        EditorGUILayout.LabelField(row.Item + " · " + row.Field + ": " + row.Before + " → " + row.After + " items", EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField(row.Message, EditorStyles.wordWrappedMiniLabel);
                    }
            }
            foreach (Group group in items)
                DrawItem(group);
        }

        /// <summary>Builds groups once for a new preview, avoiding repeated sorting during repaint.</summary>
        /// <param name="preview">New completed review.</param>
        private void Build(ExcelImportPreview preview)
        {
            // Discard the previous proposal's groups and expansion state.
            current = preview;
            errors.Clear();
            items.Clear();
            lists.Clear();
            scroll = Vector2.zero;
            errorCount = 0;
            changed = 0;
            defaults = 0;
            Dictionary<string, Group> errorGroups = new Dictionary<string, Group>();
            Dictionary<string, Group> itemGroups = new Dictionary<string, Group>();
            // Group errors by their mapping and changes by their parent/item identity.
            foreach (ExcelPreviewRow row in preview.Rows)
            {
                if (row.Error)
                {
                    string key = row.Mapping + "!" + row.Anchor + ":" + row.Field;
                    if (!errorGroups.TryGetValue(key, out Group group))
                    {
                        group = new Group(row.Field);
                        errorGroups.Add(key, group);
                        errors.Add(group);
                    }
                    group.Rows.Add(row);
                    errorCount++;
                    continue;
                }
                if (row.Kind == ExcelPreviewKind.Collection)
                {
                    lists.Add(row);
                    continue;
                }
                if (!itemGroups.TryGetValue(row.Item, out Group item))
                {
                    item = new Group(row.Item);
                    itemGroups.Add(row.Item, item);
                    items.Add(item);
                }
                item.Rows.Add(row);
                if (row.Before == row.After)
                    continue;
                if (row.Kind == ExcelPreviewKind.Default)
                    defaults++;
                else
                    changed++;
            }
            if (items.Count <= 3)
                foreach (Group item in items)
                    item.Expanded = true;
            listDetails = lists.Count <= 3;
        }

        #endregion
        #region Corrections

        /// <summary>Lists exact failing cells and a direct route to their column mapping.</summary>
        /// <param name="group">Errors for one mapped field.</param>
        /// <param name="edit">Layout navigation callback.</param>
        private void DrawError(Group group, Action<ExcelPreviewRow> edit)
        {
            // Present one correction per affected field, with exact cells available below it.
            ExcelLayoutSections.Horizontal();
            ExcelPreviewRow first = group.Rows[0];
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Where: " + Locations(group.Rows), EditorStyles.wordWrappedLabel);
            foreach (IGrouping<string, ExcelPreviewRow> problem in group.Rows.GroupBy(row => row.Message))
            {
                EditorGUILayout.LabelField(problem.Key, errorStyle);
                string[] values = problem.Select(row => Display(row.After)).Distinct().Take(4).ToArray();
                if (first.Mapping.Length > 0 || values.Any(value => value != "(empty)"))
                    EditorGUILayout.LabelField("Read from file: " + string.Join(", ", values), EditorStyles.wordWrappedLabel);
            }
            if (first.Fix.Length > 0)
                EditorGUILayout.LabelField(first.Fix, EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (first.Anchor.Length > 0 && StudioButton.Draw(new GUIContent("Edit Mapping", "Open this field's starting cell in Layout.")))
                    edit(first);
                group.Expanded = EditorGUILayout.Foldout(group.Expanded, group.Rows.Count + " affected cell" + (group.Rows.Count == 1 ? string.Empty : "s"), true);
            }
            if (group.Expanded)
                foreach (ExcelPreviewRow row in group.Rows)
                    EditorGUILayout.LabelField(row.Cell + " · " + row.Item + " · " + Display(row.After), EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Compresses contiguous cells in one worksheet column into an exact range.</summary>
        /// <param name="rows">Related cell errors.</param>
        /// <returns>A complete location list without repeating item values.</returns>
        private static string Locations(List<ExcelPreviewRow> rows)
        {
            // Compress only truly contiguous cells; retain every address for sparse errors.
            string[] cells = rows.Select(row => row.Cell).Distinct().ToArray();
            int separator = cells[0].LastIndexOf('!');
            if (separator < 0 || !cells.All(cell => cell.StartsWith(cells[0].Substring(0, separator + 1), StringComparison.Ordinal)))
                return string.Join(", ", cells);
            (int Row, int Column)[] positions = cells.Select(cell => ExcelAddress.Parse(cell.Substring(separator + 1))).OrderBy(cell => cell.Row).ToArray();
            if (positions.All(cell => cell.Column == positions[0].Column) && positions.Last().Row - positions[0].Row + 1 == positions.Length)
                return cells[0].Substring(0, separator + 1) + ExcelAddress.Format(positions[0].Row, positions[0].Column)
                    + (positions.Length > 1 ? ":" + ExcelAddress.Format(positions.Last().Row, positions.Last().Column) : string.Empty);
            return string.Join(", ", cells);
        }

        #endregion
        #region Changes

        /// <summary>Keeps ordinary changes and default resets separate within each item.</summary>
        /// <param name="group">One affected preset item.</param>
        private void DrawItem(Group group)
        {
            // Hide unchanged items while keeping default resets visible for review.
            int count = group.Rows.Count(row => row.Kind == ExcelPreviewKind.Value && row.Before != row.After);
            int resets = group.Rows.Count(row => row.Kind == ExcelPreviewKind.Default);
            if (changesOnly && count == 0 && resets == 0)
                return;
            ExcelLayoutSections.Horizontal();
            group.Expanded = EditorGUILayout.Foldout(group.Expanded, new GUIContent(group.Title + " · " + count + " changed fields", group.Title), true);
            if (!group.Expanded)
                return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Excel cell", GUILayout.Width(115f));
                GUILayout.Label("Field", GUILayout.Width(210f));
                GUILayout.Label("Current", GUILayout.ExpandWidth(true));
                GUILayout.Label("After import", GUILayout.ExpandWidth(true));
            }
            foreach (ExcelPreviewRow row in group.Rows.Where(row => row.Kind == ExcelPreviewKind.Value && (!changesOnly || row.Before != row.After)))
                DrawValue(row);
            if (resets == 0)
                return;
            group.Defaults = EditorGUILayout.Foldout(group.Defaults, resets + " other fields reset to defaults", true);
            if (group.Defaults)
                foreach (ExcelPreviewRow row in group.Rows.Where(row => row.Kind == ExcelPreviewKind.Default))
                    DrawValue(row);
        }

        /// <summary>Wraps comparison values so the complete field and both values remain readable.</summary>
        /// <param name="row">One field change.</param>
        private static void DrawValue(ExcelPreviewRow row)
        {
            // Size the row for both values so longer entries remain readable in the window.
            float width = Math.Max(180f, (EditorGUIUtility.currentViewWidth - 370f) * 0.5f);
            float height = Math.Max(22f, Math.Max(EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(Display(row.Before)), width),
                EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(Display(row.After)), width)) + 6f);
            height = Math.Max(height, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(row.Field), 210f) + 6f);
            using EditorGUILayout.HorizontalScope values = new EditorGUILayout.HorizontalScope();
            GUILayout.Label(new GUIContent(row.Cell, row.Cell), EditorStyles.wordWrappedMiniLabel, GUILayout.Width(115f), GUILayout.Height(height));
            GUILayout.Label(new GUIContent(row.Field, row.Field), EditorStyles.wordWrappedLabel, GUILayout.Width(210f), GUILayout.Height(height));
            GUILayout.Label(new GUIContent(Display(row.Before), row.Before), EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            GUILayout.Label(new GUIContent(Display(row.After), row.After), EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true), GUILayout.Height(height));
        }

        /// <summary>Makes empty values and list separators easy to scan in the review.</summary>
        /// <param name="value">Stored value text.</param>
        /// <returns>Readable display text without changing the imported value.</returns>
        private static string Display(string value)
        {
            // Display formatting leaves the actual workbook values untouched.
            return value.Length == 0 ? "(empty)" : value.Replace(";", "; ");
        }

        #endregion
        #endregion
    }
}
