using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Owns reversible flag edits without touching project assets before Apply.</summary>
    internal sealed class ObjectFlagDraftSession : ScriptableObject
    {
        #region Fields

        [Header("Draft")]
        [Tooltip("Detached definition edits and pending creations or deletions.")]
        [SerializeField]
        private List<ObjectFlagDefinitionDraft> definitions = new List<ObjectFlagDefinitionDraft>();
        [Tooltip("Pending flag selections and validation cleanup.")]
        [SerializeField]
        private List<ObjectFlagSelectionDraft> selections = new List<ObjectFlagSelectionDraft>();
        [Tooltip("Pending named groups for the tab presets.")]
        [SerializeField]
        private List<ObjectFlagGroupDraft> groups = new List<ObjectFlagGroupDraft>();

        #endregion
        #region State

        private readonly Dictionary<ObjectFlag, ObjectFlagDefinitionDraft> definitionIndex = new Dictionary<ObjectFlag, ObjectFlagDefinitionDraft>();
        private readonly Dictionary<string, ObjectFlagSelectionDraft> selectionIndex = new Dictionary<string, ObjectFlagSelectionDraft>();
        private ObjectFlag[] catalog = Array.Empty<ObjectFlag>();
        private bool catalogDirty = true;
        private readonly HashSet<ObjectFlag> copies = new HashSet<ObjectFlag>();

        #endregion
        #region Properties

        internal event Action Changed;
        internal bool HasChanges { get; private set; }
        internal int Revision { get; private set; }
        internal int Generation { get; private set; }
        internal bool Released { get; private set; }
        internal IReadOnlyList<ObjectFlagDefinitionDraft> Definitions => definitions;
        internal IReadOnlyList<ObjectFlagSelectionDraft> Selections => selections;
        internal IReadOnlyList<ObjectFlagGroupDraft> Groups => groups;
        internal IReadOnlyList<ObjectFlag> Flags
        {
            get
            {
                if (catalogDirty)
                {
                    catalog = ObjectFlagCatalog.Flags.Concat(definitions.Where(entry => entry.Created).Select(entry => entry.Identity))
                        .Where(flag => flag != null && !IsDeleted(flag)).Distinct()
                        .OrderBy(flag => Definition(flag).DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
                    catalogDirty = false;
                }
                return catalog;
            }
        }

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Rebuilds transient lookup tables after an Undo or assembly reload.</summary>
        internal void Restore()
        {
            // Serialized draft objects remain owned by the window across script reloads.
            Generation++;
            definitionIndex.Clear();
            selectionIndex.Clear();
            foreach (ObjectFlagDefinitionDraft entry in definitions)
                if (entry.Identity != null)
                {
                    definitionIndex[entry.Identity] = entry;
                    copies.Add(entry.Working);
                }
            foreach (ObjectFlagSelectionDraft entry in selections)
                selectionIndex[entry.Field.Key] = entry;
            Notify();
        }

        /// <summary>Updates pending state once after an edit, rather than serializing drafts during repaint.</summary>
        internal void Notify()
        {
            HasChanges = definitions.Any(entry => entry.Changed) || selections.Any(entry => entry.Changed) || groups.Any(entry => entry.Changed);
            catalogDirty = true;
            Revision++;
            Changed?.Invoke();
        }

        /// <summary>Reloads clean sources after project changes while preserving pending proposals.</summary>
        internal void RefreshSources()
        {
            if (!HasChanges)
            {
                // Clean copies follow source changes without destroying objects referenced by Redo.
                foreach (ObjectFlagDefinitionDraft entry in definitions)
                    if (!entry.Created && entry.Source != null && JsonUtility.ToJson(entry.Source) != entry.Baseline)
                    {
                        entry.Baseline = JsonUtility.ToJson(entry.Source);
                        JsonUtility.FromJsonOverwrite(entry.Baseline, entry.Working);
                    }
                selections.Clear();
                groups.Clear();
                selectionIndex.Clear();
            }
            Notify();
        }

        /// <summary>Abandons the whole proposal and clears only this tool's draft Undo records.</summary>
        internal void Discard()
        {
            Reset();
        }

        /// <summary>Releases temporary copies after the window has handled its close confirmation.</summary>
        internal void Release()
        {
            Reset();
            Released = true;
        }

        /// <summary>Starts a clean editing generation without retaining references to old working copies.</summary>
        internal void Reset()
        {
            Undo.ClearUndo(this);
            foreach (ObjectFlag copy in copies)
                if (copy != null && !EditorUtility.IsPersistent(copy))
                {
                    Undo.ClearUndo(copy);
                    DestroyImmediate(copy);
                }
            copies.Clear();
            definitions.Clear();
            selections.Clear();
            groups.Clear();
            definitionIndex.Clear();
            selectionIndex.Clear();
            Generation++;
            Notify();
        }

        #endregion
        #region Definitions

        /// <summary>Reads proposed metadata while preserving the original flag's reference identity.</summary>
        /// <param name="flag">Persistent flag or pending new definition.</param>
        /// <returns>The editable definition if one exists, otherwise the source.</returns>
        internal ObjectFlag Definition(ObjectFlag flag)
        {
            return flag != null && definitionIndex.TryGetValue(flag, out ObjectFlagDefinitionDraft entry) ? entry.Working : flag;
        }

        /// <summary>Checks whether a definition is hidden by a pending deletion.</summary>
        /// <param name="flag">Definition to inspect.</param>
        /// <returns>True when deletion is staged.</returns>
        internal bool IsDeleted(ObjectFlag flag)
        {
            return flag != null && definitionIndex.TryGetValue(flag, out ObjectFlagDefinitionDraft entry) && entry.Deleted;
        }

        /// <summary>Creates a detached definition only when metadata editing actually begins.</summary>
        /// <param name="flag">Existing or pending flag.</param>
        /// <returns>The isolated editable copy.</returns>
        internal ObjectFlag Editable(ObjectFlag flag)
        {
            if (flag == null || IsDeleted(flag))
                throw new InvalidOperationException("This definition is no longer available.");
            if (definitionIndex.TryGetValue(flag, out ObjectFlagDefinitionDraft entry))
                return entry.Working;
            RequireWritable(AssetDatabase.GetAssetPath(flag));
            entry = new ObjectFlagDefinitionDraft { Source = flag, Working = Instantiate(flag), Baseline = JsonUtility.ToJson(flag) };
            entry.Working.hideFlags = HideFlags.HideAndDontSave;
            entry.Working.name = flag.name;
            copies.Add(entry.Working);
            definitions.Add(entry);
            definitionIndex.Add(flag, entry);
            return entry.Working;
        }

        /// <summary>Stages one metadata update for a stable bulk selection with one Undo operation.</summary>
        /// <param name="flags">Definitions selected when the command opened.</param>
        /// <param name="edit">Metadata changes applied to their isolated copies.</param>
        /// <param name="label">Undo label.</param>
        internal void Edit(IEnumerable<ObjectFlag> flags, Action<ObjectFlag> edit, string label)
        {
            // Prepare every target before changing any of them.
            ObjectFlag[] working = flags.Where(flag => flag != null).Distinct().Select(Editable).ToArray();
            if (working.Length == 0)
                return;
            Undo.RecordObjects(working, label);
            foreach (ObjectFlag flag in working)
                edit(flag);
            Notify();
        }

        /// <summary>Creates a new definition in memory; Apply creates its persistent asset.</summary>
        /// <param name="name">Unique display name.</param>
        /// <param name="group">Initial selector group.</param>
        /// <param name="description">Initial description.</param>
        /// <returns>The pending flag identity available to this tool's selectors.</returns>
        internal ObjectFlag Create(string name, string group, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || Flags.Any(flag => string.Equals(Definition(flag).DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose a nonempty, unique flag name.");
            Undo.RecordObject(this, "Create identity flag draft");
            ObjectFlag flag = CreateInstance<ObjectFlag>();
            flag.hideFlags = HideFlags.HideAndDontSave;
            flag.DisplayName = name.Trim();
            flag.Group = group ?? string.Empty;
            flag.Description = description ?? string.Empty;
            copies.Add(flag);
            ObjectFlagDefinitionDraft entry = new ObjectFlagDefinitionDraft { Working = flag, Created = true };
            definitions.Add(entry);
            definitionIndex.Add(flag, entry);
            Notify();
            return flag;
        }

        /// <summary>Stages missing Unity tag definitions without creating project files.</summary>
        internal void ImportTags()
        {
            foreach (string tag in UnityEditorInternal.InternalEditorUtility.tags)
                if (!Flags.Any(flag => string.Equals(Definition(flag).DisplayName, tag, StringComparison.OrdinalIgnoreCase)))
                    Create(tag, "Imported", "Identity flag imported from " + tag + ".");
        }

        #endregion
        #region Groups

        /// <summary>Reads the proposed named groups for one tab preset.</summary>
        /// <param name="source">Preset owning named empty groups.</param>
        /// <returns>The staged names or unchanged source names.</returns>
        internal IReadOnlyList<string> GroupNames(ObjectFlagGroupsPreset source)
        {
            return groups.Find(entry => entry.Source == source)?.Values ?? source.Groups;
        }

        /// <summary>Stages a group-list update while leaving navigation filters independent.</summary>
        /// <param name="source">Preset receiving group definitions on Apply.</param>
        /// <param name="edit">Explicit mutation of the proposed names.</param>
        internal void EditGroups(ObjectFlagGroupsPreset source, Action<List<string>> edit)
        {
            RequireWritable(AssetDatabase.GetAssetPath(source));
            Undo.RecordObject(this, "Edit identity flag groups");
            ObjectFlagGroupDraft entry = groups.Find(candidate => candidate.Source == source);
            if (entry == null)
            {
                entry = new ObjectFlagGroupDraft { Source = source, Baseline = new List<string>(source.Groups), Values = new List<string>(source.Groups) };
                groups.Add(entry);
            }
            edit(entry.Values);
            Notify();
        }

        #endregion
        #region Selections and Cleanup

        /// <summary>Reads the proposed field membership without opening its scene or asset again.</summary>
        /// <param name="field">Scanned selection.</param>
        /// <returns>The proposed membership, or original scan values.</returns>
        internal ObjectFlag[] Values(ObjectFlagField field)
        {
            return selectionIndex.TryGetValue(field.Key, out ObjectFlagSelectionDraft entry) ? entry.Values : field.Flags;
        }

        /// <summary>Reports remaining missing slots after staged cleanup or selection replacement.</summary>
        /// <param name="field">Scanned selection.</param>
        /// <returns>Missing slots still visible in the proposal.</returns>
        internal int Missing(ObjectFlagField field)
        {
            return selectionIndex.ContainsKey(field.Key) ? field.Multiple ? Values(field).Count(flag => flag == null) : 0 : field.Missing;
        }

        /// <summary>Captures the source field once, then stages all later popup changes in memory.</summary>
        /// <param name="field">Stable source location.</param>
        /// <param name="values">Explicit replacement membership.</param>
        /// <param name="cleanMissing">Whether unresolved scalar metadata must be cleared too.</param>
        internal void SetSelection(ObjectFlagField field, ObjectFlag[] values, bool cleanMissing = false)
        {
            if (!field.IsFlag)
                throw new InvalidOperationException("Only identity flag fields can be edited here.");
            RequireWritable(field.AssetPath);
            Undo.RecordObject(this, "Edit identity flag selection");
            if (!selectionIndex.TryGetValue(field.Key, out ObjectFlagSelectionDraft entry))
            {
                entry = new ObjectFlagSelectionDraft { Field = field, Baseline = field.ContentHash };
                selections.Add(entry);
                selectionIndex.Add(field.Key, entry);
            }
            entry.Values = field.Multiple ? values : new[] { values.FirstOrDefault() };
            entry.CleanMissing |= cleanMissing;
            Notify();
        }

        /// <summary>Stages only missing flag references; unrelated project references are never changed.</summary>
        /// <param name="fields">Validation results selected for cleanup.</param>
        internal void Clean(IEnumerable<ObjectFlagField> fields)
        {
            ObjectFlagField[] targets = fields.Where(field => field.IsFlag && Missing(field) > 0).ToArray();
            foreach (ObjectFlagField field in targets)
                RequireWritable(field.AssetPath);
            foreach (ObjectFlagField field in targets)
                SetSelection(field, Values(field).Where(flag => flag != null).ToArray(), true);
        }

        /// <summary>Reviews actual project uses and stages their cleanup together with flag deletion.</summary>
        /// <param name="flags">Definitions requested for removal.</param>
        /// <returns>True when the complete deletion proposal was staged.</returns>
        internal bool Delete(IEnumerable<ObjectFlag> flags)
        {
            HashSet<ObjectFlag> targets = new HashSet<ObjectFlag>(flags.Where(flag => flag != null));
            if (targets.Count == 0)
                return true;
            ObjectFlagScan scan = ObjectFlagProject.Scan(true, true, false);
            if (!scan.Complete)
                throw new InvalidOperationException("A complete flag reference scan is required before deletion.");
            ObjectFlagField[] uses = scan.Fields.Where(field => field.Flags.Any(targets.Contains) || Values(field).Any(targets.Contains)).ToArray();
            foreach (ObjectFlagField field in uses)
                RequireWritable(field.AssetPath);
            foreach (ObjectFlag flag in targets)
                Editable(flag);
            if (!ObjectFlagDeleteWindow.Confirm(targets, uses.ToList()))
                return false;
            StageDeletion(targets, uses);
            return true;
        }

        /// <summary>Stages a reviewed deletion and all its known reference updates in one Undo group.</summary>
        /// <param name="flags">Previously reviewed definitions.</param>
        /// <param name="uses">Known project use locations.</param>
        internal void StageDeletion(IEnumerable<ObjectFlag> flags, IEnumerable<ObjectFlagField> uses)
        {
            HashSet<ObjectFlag> targets = new HashSet<ObjectFlag>(flags);
            foreach (ObjectFlag flag in targets)
                Editable(flag);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Stage identity flag deletion");
            try
            {
                foreach (ObjectFlagField field in uses)
                    SetSelection(field, Values(field).Where(flag => flag != null && !targets.Contains(flag)).ToArray());
                foreach (ObjectFlagSelectionDraft entry in selections.ToArray())
                    if (entry.Values.Any(targets.Contains))
                        SetSelection(entry.Field, entry.Values.Where(flag => flag != null && !targets.Contains(flag)).ToArray());
                Undo.RecordObject(this, "Stage identity flag deletion");
                foreach (ObjectFlag flag in targets)
                    definitionIndex[flag].Deleted = true;
                Notify();
            }
            finally
            {
                Undo.CollapseUndoOperations(undo);
            }
        }

        /// <summary>Rejects unavailable files before an operation stages a proposal that cannot be applied.</summary>
        /// <param name="path">Source asset path.</param>
        private static void RequireWritable(string path)
        {
            if (!ObjectFlagAssetAccess.Writable(path))
                throw new InvalidOperationException("This asset is read-only: " + path);
        }

        #endregion
        #endregion
    }
}
