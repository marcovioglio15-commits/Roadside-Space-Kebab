using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Draws grouped flag rows, desktop range selection and explicit bulk operations.</summary>
    internal sealed class ObjectFlagGroupsView
    {
        #region State

        private ObjectFlagGroupsPreset preset;
        private ObjectFlagGroupsPreset previousPreset;
        private readonly HashSet<ObjectFlag> selected = new HashSet<ObjectFlag>();
        private readonly HashSet<string> collapsed = new HashSet<string>();
        private readonly List<ObjectFlag> visible = new List<ObjectFlag>();
        private readonly List<string> names = new List<string>();
        private ObjectFlag anchor;
        private ObjectFlag drag;
        private Vector2 dragStart;
        private Vector2 scroll;
        private ObjectFlagDraftSession session;
        private bool dirty = true;
        private bool collapseOnRelease;
        private string[] groupLabels = Array.Empty<string>();

        #endregion
        #region Methods
        #region Drawing

        /// <summary>Requests a catalog rebuild after an actual project or Undo change.</summary>
        internal void Invalidate()
        {
            dirty = true;
        }

        /// <summary>Draws the dedicated preset, group filters and selection operations.</summary>
        /// <param name="draft">Shared management proposal.</param>
        internal void Draw(ObjectFlagDraftSession draft)
        {
            session = draft;
            ObjectFlagPresetGUI.Draw(ref preset);
            if (preset == null)
                return;
            if (previousPreset != preset)
            {
                previousPreset = preset;
                selected.Clear();
                dirty = true;
            }
            if (dirty)
                Rebuild();
            DrawToolbar();
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (string group in names)
                {
                    if (preset.FilterGroup && preset.SelectedGroup != group)
                        continue;
                    DrawGroup(group);
                }
            }
        }

        /// <summary>Offers contextual group operations and selection-wide commands.</summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = preset.FilterGroup ? names.IndexOf(preset.SelectedGroup) + 1 : 0;
                int choice = EditorGUILayout.Popup(new GUIContent("Group", "Show all groups or one named group."), current, groupLabels);
                if (choice != current)
                {
                    Undo.RecordObject(preset, "Filter flag groups");
                    preset.FilterGroup = choice > 0;
                    preset.SelectedGroup = choice > 0 ? names[choice - 1] : string.Empty;
                    Save();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("New Flag", "Create a flag inside the selected group."), GUILayout.Width(85f)))
                    ObjectFlagCreateWindow.Open(flag => selected.Add(flag), "", preset.FilterGroup ? preset.SelectedGroup : string.Empty, session);
                if (GUILayout.Button(new GUIContent("Import Tags", "Create flags for project tags without a matching flag."), GUILayout.Width(85f)))
                    session.ImportTags();
                if (GUILayout.Button(new GUIContent("+ Add Group", "Create a named group in the draft."), GUILayout.Width(112f)))
                {
                    ObjectFlagGroupsPreset source = preset;
                    ObjectFlagTextWindow.Open("Add Group", string.Empty, text => AddGroup(source, text.Trim()));
                }
            }
            if (ObjectFlagPresetGUI.Fields(preset, "Search"))
                dirty = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(selected.Count + " selected", "Shift selects a range. Ctrl or Command toggles one flag. Drag selected flags onto a group."), EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("Select Visible", "Select every flag in the expanded, filtered groups.")))
                    selected.UnionWith(visible);
                if (GUILayout.Button(new GUIContent("Clear", "Clear the flag selection.")))
                    selected.Clear();
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Move To…", "Move all selected flags to another group.")))
                        MoveMenu(selected.ToArray());
                    if (GUILayout.Button(new GUIContent("Description…", "Set one description on all selected flags.")))
                    {
                        ObjectFlag[] targets = selected.ToArray();
                        ObjectFlagDescriptionWindow.Open(targets, session);
                    }
                    if (GUILayout.Button(new GUIContent("Delete…", "Inspect usages and delete the selected flags.")))
                        Delete(selected.ToArray());
                }
            }
        }

        /// <summary>Draws one foldout header as a drop target and a contextual group menu.</summary>
        /// <param name="group">Exact group name, including the empty ungrouped name.</param>
        private void DrawGroup(string group)
        {
            // The complete expanded container remains a drop target, including every flag row.
            Rect area = EditorGUILayout.BeginVertical();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool expanded = !collapsed.Contains(group);
                bool next = EditorGUILayout.Foldout(expanded, new GUIContent(Name(group), "Drop selected flags here to move them into this group."), true);
                if (next != expanded)
                {
                    if (next)
                        collapsed.Remove(group);
                    else
                        collapsed.Add(group);
                    dirty = true;
                }
                if (GUILayout.Button(new GUIContent("...", "Select, rename, move or delete this entire group."), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                    GroupMenu(group);
            }
            if (!collapsed.Contains(group))
            {
                foreach (ObjectFlag flag in visible)
                    if ((session.Definition(flag).Group ?? string.Empty) == group)
                        DrawFlag(flag);
                GUILayout.Space(4f);
            }
            EditorGUILayout.EndVertical();
            Drop(area, group);
        }

        /// <summary>Draws a selectable flag row and starts drag selection only after pointer movement.</summary>
        /// <param name="flag">Definition represented by this row.</param>
        private void DrawFlag(ObjectFlag flag)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            if (selected.Contains(flag))
                EditorGUI.DrawRect(row, new Color(0.2f, 0.45f, 0.7f, 0.35f));
            Rect edit = new Rect(row.xMax - 48f, row.y, 48f, row.height);
            ObjectFlag definition = session.Definition(flag);
            GUI.Label(new Rect(row.x + 8f, row.y, row.width - 60f, row.height), new GUIContent(definition.DisplayName, definition.Description));
            if (GUI.Button(edit, new GUIContent("Edit", "Edit this flag's name, group and description."), EditorStyles.miniButton))
                ObjectFlagEditWindow.Open(flag, session);
            GUI.Label(new Rect(row.x, row.y, row.width - 52f, row.height), new GUIContent(string.Empty,
                string.IsNullOrEmpty(definition.Description) ? "No description." : definition.Description));
            Event input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 && row.Contains(input.mousePosition) && !edit.Contains(input.mousePosition))
            {
                collapseOnRelease = !input.control && !input.command && !input.shift && selected.Count > 1 && selected.Contains(flag);
                if (!collapseOnRelease)
                    Select(flag, input);
                drag = flag;
                dragStart = input.mousePosition;
                input.Use();
            }
            if (input.type == EventType.MouseUp && drag == flag)
            {
                if (collapseOnRelease)
                {
                    selected.Clear();
                    selected.Add(flag);
                    anchor = flag;
                }
                drag = null;
                input.Use();
            }
            if (input.type == EventType.MouseDrag && drag == flag && Vector2.Distance(dragStart, input.mousePosition) > 5f)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = selected.Cast<UnityEngine.Object>().ToArray();
                DragAndDrop.StartDrag("Move identity flags");
                drag = null;
                input.Use();
            }
        }

        #endregion
        #region Selection and Groups

        /// <summary>Implements Shift ranges and Ctrl or Command toggles over visible flag rows.</summary>
        /// <param name="flag">Clicked definition.</param>
        /// <param name="input">Pointer event carrying selection modifiers.</param>
        private void Select(ObjectFlag flag, Event input)
        {
            // Ranges follow visible row order and exclude hidden or collapsed groups.
            if (input.shift && anchor != null && visible.Contains(anchor))
            {
                if (!input.control && !input.command)
                    selected.Clear();
                int first = visible.IndexOf(anchor);
                int last = visible.IndexOf(flag);
                for (int index = Math.Min(first, last); index <= Math.Max(first, last); index++)
                    selected.Add(visible[index]);
                return;
            }
            if (!input.control && !input.command)
            {
                selected.Clear();
                selected.Add(flag);
            }
            else if (!selected.Remove(flag))
                selected.Add(flag);
            anchor = flag;
        }

        /// <summary>Accepts flag assets dragged from this tool or the Project browser.</summary>
        /// <param name="rect">Group header drop area.</param>
        /// <param name="group">Destination group.</param>
        private void Drop(Rect rect, string group)
        {
            Event input = Event.current;
            if (!rect.Contains(input.mousePosition) || input.type is not (EventType.DragUpdated or EventType.DragPerform)
                || !DragAndDrop.objectReferences.Any(value => value is ObjectFlag))
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            if (input.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Move(DragAndDrop.objectReferences.OfType<ObjectFlag>().ToArray(), group);
                GUIUtility.ExitGUI();
            }
            input.Use();
        }

        /// <summary>Builds operations for all definitions in a group, independent of the search filter.</summary>
        /// <param name="group">Group selected by the menu.</param>
        private void GroupMenu(string group)
        {
            ObjectFlag[] targets = session.Flags.Where(flag => (session.Definition(flag).Group ?? string.Empty) == group).ToArray();
            ObjectFlagGroupsPreset source = preset;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Select Entire Group"), false, () => selected.UnionWith(targets));
            menu.AddItem(new GUIContent("New Flag"), false, () => ObjectFlagCreateWindow.Open(null, "", group, session));
            menu.AddItem(new GUIContent("Edit Descriptions..."), false, () => ObjectFlagDescriptionWindow.Open(targets, session));
            if (group.Length > 0)
                menu.AddItem(new GUIContent("Rename Group…"), false, () => ObjectFlagTextWindow.Open("Rename Group", group, text => Rename(source, group, text.Trim())));
            foreach (string destination in names)
                if (destination != group)
                    menu.AddItem(new GUIContent("Move Entire Group To/" + Name(destination)), false, () => Move(targets, destination));
            menu.AddItem(new GUIContent("Delete Group and Flags…"), false, () =>
            {
                if (targets.Length == 0 || StageDelete(targets))
                {
                    session.EditGroups(source, values => values.Remove(group));
                    dirty = true;
                }
            });
            menu.ShowAsContext();
        }

        /// <summary>Offers all existing groups as destinations for a stable selection snapshot.</summary>
        /// <param name="targets">Definitions selected when the menu opened.</param>
        private void MoveMenu(ObjectFlag[] targets)
        {
            GenericMenu menu = new GenericMenu();
            foreach (string group in names)
                menu.AddItem(new GUIContent(Name(group)), false, () => Move(targets, group));
            menu.ShowAsContext();
        }

        /// <summary>Moves definitions without changing their asset identities.</summary>
        /// <param name="targets">Definitions to move.</param>
        /// <param name="group">Destination group.</param>
        /// <returns>True when every definition was moved.</returns>
        private bool Move(ObjectFlag[] targets, string group)
        {
            try
            {
                session.Edit(targets, flag => flag.Group = group, "Move identity flags");
            }
            catch (InvalidOperationException exception)
            {
                ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
                return false;
            }
            dirty = true;
            return true;
        }

        /// <summary>Deletes a selection through the shared usage and cleanup operation.</summary>
        /// <param name="targets">Definitions requested for deletion.</param>
        private void Delete(ObjectFlag[] targets)
        {
            if (StageDelete(targets))
                selected.Clear();
            dirty = true;
            GUIUtility.ExitGUI();
        }

        /// <summary>Adds an empty named group without creating placeholder flag assets.</summary>
        /// <param name="source">Preset selected when the group form opened.</param>
        /// <param name="group">Requested group name.</param>
        private void AddGroup(ObjectFlagGroupsPreset source, string group)
        {
            if (group.Length == 0 || names.Contains(group))
                return;
            session.EditGroups(source, values => values.Add(group));
            dirty = true;
        }

        /// <summary>Renames or merges a group and updates its stored empty-group definition.</summary>
        /// <param name="oldName">Existing group name.</param>
        /// <param name="newName">Requested replacement name.</param>
        /// <param name="source">Preset selected when the rename form opened.</param>
        private void Rename(ObjectFlagGroupsPreset source, string oldName, string newName)
        {
            if (newName.Length == 0 || newName == oldName)
                return;
            if (!Move(session.Flags.Where(flag => session.Definition(flag).Group == oldName).ToArray(), newName))
                return;
            session.EditGroups(source, values =>
            {
                values.Remove(oldName);
                if (!values.Contains(newName))
                    values.Add(newName);
            });
            if (source == preset && preset.SelectedGroup == oldName)
                preset.SelectedGroup = newName;
            dirty = true;
        }

        /// <summary>Saves tab settings after an explicit group or filter edit.</summary>
        private void Save()
        {
            EditorUtility.SetDirty(preset);
            dirty = true;
        }

        /// <summary>Rebuilds groups and visible range order only after relevant state changes.</summary>
        private void Rebuild()
        {
            // Keep catalog queries and sorting outside ordinary repaint passes.
            names.Clear();
            names.AddRange(session.GroupNames(preset).Concat(session.Flags.Select(flag => session.Definition(flag).Group ?? string.Empty))
                .Concat(preset.FilterGroup ? new[] { preset.SelectedGroup } : Array.Empty<string>())
                .Append(string.Empty).Distinct().OrderBy(group => group, StringComparer.OrdinalIgnoreCase));
            groupLabels = new[] { "All Groups" }.Concat(names.Select(Name)).ToArray();
            visible.Clear();
            foreach (string group in names)
                if (!collapsed.Contains(group) && (!preset.FilterGroup || preset.SelectedGroup == group))
                    visible.AddRange(session.Flags.Where(flag => (session.Definition(flag).Group ?? string.Empty) == group
                        && (preset.Search.Length == 0 || (session.Definition(flag).DisplayName + " " + session.Definition(flag).Description).Contains(preset.Search, StringComparison.OrdinalIgnoreCase))));
            selected.RemoveWhere(flag => flag == null || session.IsDeleted(flag));
            dirty = false;
        }

        /// <summary>Gives ungrouped flags a clear menu label.</summary>
        /// <param name="group">Serialized group name.</param>
        /// <returns>The group label shown by the tool.</returns>
        private static string Name(string group)
        {
            return group.Length == 0 ? "Ungrouped" : group;
        }

        /// <summary>Stages a deletion while keeping save and permission errors inside the tool.</summary>
        /// <param name="targets">Selected definitions.</param>
        /// <returns>True when deletion was accepted and staged.</returns>
        private bool StageDelete(ObjectFlag[] targets)
        {
            try
            {
                return session.Delete(targets);
            }
            catch (InvalidOperationException exception)
            {
                ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
                return false;
            }
        }

        /// <summary>Saves navigation preferences separately from pending group content.</summary>
        internal void SaveNavigation()
        {
            if (preset != null)
                AssetDatabase.SaveAssetIfDirty(preset);
        }

        /// <summary>Leaves a discarded rename's temporary filter without recreating a phantom group.</summary>
        internal void DiscardNavigation()
        {
            if (preset == null || !preset.FilterGroup || session.GroupNames(preset).Contains(preset.SelectedGroup)
                || session.Flags.Any(flag => session.Definition(flag).Group == preset.SelectedGroup))
                return;
            preset.FilterGroup = false;
            EditorUtility.SetDirty(preset);
            dirty = true;
        }

        #endregion
        #endregion
    }
}
