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
        private string search = string.Empty;
        private string[] groups;
        private int group;
        private bool selectedOnly;
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
            selected = ObjectFlagSelector.Read(owner, path, out multiple);
            RefreshGroups();
        }

        /// <summary>Rebuilds group filters after explicit catalog creation or import.</summary>
        private void RefreshGroups()
        {
            // A newly imported group becomes available without closing the selector.
            List<string> names = new List<string> { "All Groups" };
            foreach (ObjectFlag flag in ObjectFlagCatalog.Flags)
                if (!string.IsNullOrEmpty(flag.Group) && !names.Contains(flag.Group))
                    names.Add(flag.Group);
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
            search = EditorGUILayout.TextField(new GUIContent("Search", "Filter by flag name or group."), search);
            group = EditorGUILayout.Popup(new GUIContent("Group", "Show definitions from one catalog group."), group, groups);
            if (multiple)
                selectedOnly = EditorGUILayout.Toggle(new GUIContent("Selected Only", "Show only flags included in this field."), selectedOnly);
            if (GUILayout.Button(new GUIContent("Clear Selection", "Remove this field's selected flags."), EditorStyles.miniButton))
            {
                ObjectFlagSelector.Assign(owner, path, null, false);
                selected.Clear();
                if (!multiple)
                    editorWindow.Close();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ObjectFlag flag in ObjectFlagCatalog.Flags)
            {
                if (selectedOnly && !selected.Contains(flag) || group > 0 && flag.Group != groups[group]
                    || search.Length > 0 && (flag.DisplayName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0
                    && (flag.Group?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    continue;
                GUIContent label = new GUIContent(flag.DisplayName + (!string.IsNullOrEmpty(flag.Group) ? " · " + flag.Group : string.Empty), flag.Description);
                bool wasSelected = selected.Contains(flag);
                bool chosen = GUILayout.Toggle(wasSelected, label, multiple ? EditorStyles.toggle : EditorStyles.miniButton);
                if (chosen == wasSelected)
                    continue;
                ObjectFlagSelector.Assign(owner, path, flag, chosen);
                if (chosen)
                    selected.Add(flag);
                else
                    selected.Remove(flag);
                if (!multiple)
                    editorWindow.Close();
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button(new GUIContent("+ Create Object Flag", "Define a new custom flag and assign it to this field.")))
            {
                ObjectFlagSelector.Create(owner, path, current, search);
                editorWindow.Close();
            }
            if (GUILayout.Button(new GUIContent("Create Missing Flags from Unity Tags", "Create one definition for each project tag without a same-named flag. Existing flags are preserved.")))
            {
                ObjectFlagCatalog.ImportProjectTags();
                RefreshGroups();
                editorWindow.Repaint();
            }
        }

        #endregion

        #endregion
    }
}
