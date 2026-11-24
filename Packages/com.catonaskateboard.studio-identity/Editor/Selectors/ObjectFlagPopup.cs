using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Filters flag choices by name, group and current selection inside one reusable popup.</summary>
    internal sealed class ObjectFlagPopup : PopupWindowContent
    {
        #region State

        private readonly UnityEngine.Object owner;
        private readonly string path;
        private readonly HashSet<ObjectFlag> selected;
        private readonly bool multiple;
        private readonly Func<bool> current;
        private readonly ObjectFlagAssignment assignment;
        private readonly ObjectFlagDraftSession session;
        private string search = string.Empty;
        private string[] groups;
        private int group;
        private bool selectedOnly;
        private bool missing;
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Captures the field identity and its current selection.</summary>
        /// <param name="target">Serialized object containing the flag field.</param>
        /// <param name="propertyPath">Field path retained across GUI events.</param>
        /// <param name="isCurrent">Checks that a recoverable workspace still edits the same object and card.</param>
        internal ObjectFlagPopup(UnityEngine.Object target, string propertyPath, Func<bool> isCurrent)
        {
            // Catalog discovery is cached globally; this popup keeps only selection and filter state.
            owner = target;
            path = propertyPath;
            current = isCurrent;
            selected = ObjectFlagSelector.Read(owner, path, out multiple, out missing);
            RefreshGroups();
        }

        /// <summary>Opens the same picker for a project field that may belong to a closed scene or portable preset.</summary>
        /// <param name="field">Scanned destination with stable owner identity.</param>
        /// <param name="changed">Refreshes the assignment view after a saved change.</param>
        /// <param name="draft">Optional management proposal.</param>
        internal ObjectFlagPopup(ObjectFlagField field, Action changed, ObjectFlagDraftSession draft = null)
        {
            session = draft;
            assignment = new ObjectFlagAssignment(field, changed, draft);
            current = () => assignment.Valid;
            selected = new HashSet<ObjectFlag>(draft != null ? draft.Values(field) : field.Flags);
            selected.Remove(null);
            multiple = field.Multiple;
            missing = (draft != null ? draft.Missing(field) : field.Missing) > 0;
            RefreshGroups();
        }

        /// <summary>Rebuilds group filters after explicit catalog creation or import.</summary>
        private void RefreshGroups()
        {
            // A newly imported group becomes available without closing the selector.
            List<string> names = new List<string> { "All Groups" };
            foreach (ObjectFlag flag in session != null ? session.Flags : ObjectFlagCatalog.Flags)
            {
                ObjectFlag definition = session != null ? session.Definition(flag) : flag;
                if (!string.IsNullOrEmpty(definition.Group) && !names.Contains(definition.Group))
                    names.Add(definition.Group);
            }
            groups = names.ToArray();
            group = 0;
        }

        /// <summary>Sizes the searchable menu consistently for both single and combined selections.</summary>
        /// <returns>The preferred popup size.</returns>
        public override Vector2 GetWindowSize()
        {
            // The scroll area bounds large catalogs without growing the tool's layout.
            return new Vector2(370f, 420f);
        }

        #endregion

        #region Drawing

        /// <summary>Draws filtered choices and explicit catalog creation actions.</summary>
        /// <param name="rect">Popup content rectangle.</param>
        public override void OnGUI(Rect rect)
        {
            // Discard a menu opened on a workspace selection that no longer exists.
            if (!current())
            {
                editorWindow.Close();
                return;
            }
            // Filters affect only presentation and never mutate the selected flag combination.
            search = StudioGUI.TextField(new GUIContent("Search", "Filter by flag name or group."), search);
            group = StudioGUI.Popup(new GUIContent("Group", "Show definitions from one catalog group."), group, groups);
            if (multiple)
                selectedOnly = StudioGUI.Toggle(new GUIContent("Selected Only", "Show only flags included in this field."), selectedOnly);
            if (StudioButton.Draw(new GUIContent("Clear Selection", "Remove this field's selected flags."), EditorStyles.miniButton))
            {
                Assign(null, false);
                selected.Clear();
                missing = false;
                if (!multiple)
                    editorWindow.Close();
            }
            if (missing && StudioButton.Draw(new GUIContent("Remove Missing Flags", "Remove unassigned or deleted references from this selection. Assigned flags are preserved."), EditorStyles.miniButton))
            {
                if (assignment != null)
                    assignment.RemoveMissing();
                else
                    ObjectFlagSelector.RemoveMissing(owner, path);
                missing = false;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ObjectFlag flag in session != null ? session.Flags : ObjectFlagCatalog.Flags)
            {
                ObjectFlag definition = session != null ? session.Definition(flag) : flag;
                if (selectedOnly && !selected.Contains(flag) || group > 0 && definition.Group != groups[group]
                    || search.Length > 0 && (definition.DisplayName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0
                    && (definition.Group?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    continue;
                GUIContent label = new GUIContent(definition.DisplayName + (!string.IsNullOrEmpty(definition.Group) ? " · " + definition.Group : string.Empty), definition.Description);
                bool wasSelected = selected.Contains(flag);
                bool chosen = GUILayout.Toggle(wasSelected, label, multiple ? EditorStyles.toggle : EditorStyles.miniButton);
                if (chosen == wasSelected)
                    continue;
                Assign(flag, chosen);
                if (chosen)
                    selected.Add(flag);
                else
                    selected.Remove(flag);
                if (!multiple)
                    editorWindow.Close();
            }
            EditorGUILayout.EndScrollView();
            if (StudioButton.Draw(new GUIContent("Manage Flags", "Edit or delete definitions in the shared flag catalog.")))
            {
                ObjectFlagCatalogWindow.Open();
                editorWindow.Close();
            }
            if (StudioButton.Draw(new GUIContent("+ Create Object Flag", "Define a new custom flag and assign it to this field.")))
            {
                if (assignment != null)
                    ObjectFlagCreateWindow.Open(flag => assignment.Assign(flag, true), search, string.Empty, session);
                else
                    ObjectFlagSelector.Create(owner, path, current, search);
                editorWindow.Close();
            }
            if (StudioButton.Draw(new GUIContent("Create Missing Flags from Unity Tags", "Create one definition for each project tag without a same-named flag. Existing flags are preserved.")))
            {
                if (session != null)
                    session.ImportTags();
                else
                    ObjectFlagCatalog.ImportProjectTags();
                RefreshGroups();
                editorWindow.Repaint();
            }
        }

        #endregion

        #region Assignment

        /// <summary>Routes popup changes through native workspace guards or stable project field resolution.</summary>
        /// <param name="flag">Selected flag, or null to clear.</param>
        /// <param name="include">Whether this flag is selected.</param>
        private void Assign(ObjectFlag flag, bool include)
        {
            if (assignment != null)
                assignment.Assign(flag, include);
            else
                ObjectFlagSelector.Assign(owner, path, flag, include);
        }

        #endregion

        #endregion
    }
}
