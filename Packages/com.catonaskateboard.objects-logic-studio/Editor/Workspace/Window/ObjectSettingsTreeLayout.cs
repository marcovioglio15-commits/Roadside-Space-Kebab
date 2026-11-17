using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds a detailed settings hierarchy, including sections, nested options and linked presets.</summary>
    internal sealed class ObjectSettingsTreeLayout : IDisposable
    {
        #region Types

        /// <summary>Distinguishes structural headings from individual option blocks.</summary>
        internal enum NodeKind { Root, Interaction, Theme, Section, Field }

        /// <summary>Stores one navigable block and the metadata needed to expand its descendants.</summary>
        internal sealed class Node
        {
            internal readonly string Key;
            internal readonly string Label;
            internal readonly string Tooltip;
            internal readonly string Path;
            internal readonly ObjectGroupInfo Group;
            internal readonly int Parent;
            internal readonly int Depth;
            internal readonly NodeKind Kind;
            internal readonly ScriptableObject Asset;
            internal readonly List<int> Children = new List<int>();
            internal ScriptableObject Reference;
            internal Rect Rect;
            internal bool Expandable;
            internal float Height;
            internal ObjectBranchSession Branch;

            #region Methods

            /// <summary>Captures a block once when its branch is rebuilt.</summary>
            /// <param name="key">Unique expansion identity.</param>
            /// <param name="label">Option or section name.</param>
            /// <param name="tooltip">Short explanation.</param>
            /// <param name="path">Serialized option path.</param>
            /// <param name="group">Thematic editor containing this branch.</param>
            /// <param name="parent">Parent block index.</param>
            /// <param name="depth">Horizontal level.</param>
            /// <param name="kind">Structural role.</param>
            /// <param name="asset">Optional linked preset owning the option.</param>
            internal Node(string key, string label, string tooltip, string path, ObjectGroupInfo group, int parent, int depth, NodeKind kind, ScriptableObject asset = null)
            {
                Key = key;
                Label = label;
                Tooltip = tooltip;
                Path = path;
                Group = group;
                Parent = parent;
                Depth = depth;
                Kind = kind;
                Asset = asset;
            }

            #endregion
        }

        #endregion

        #region State

        internal readonly List<Node> Nodes = new List<Node>();
        internal Vector2 Size;
        private readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collapsed = new HashSet<string>(StringComparer.Ordinal);
        private ObjectWorkspace document;
        private GameObject owner;
        private readonly List<ObjectBranchSession> branches = new List<ObjectBranchSession>();
        private bool expandAll;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures all visible interactions under one object root without changing workspace selection.</summary>
        /// <param name="state">Shared interaction drafts.</param>
        /// <param name="target">Object represented by the root node.</param>
        /// <param name="features">Filtered interactions in the current object scope.</param>
        internal void Build(ObjectWorkspace state, GameObject target, IReadOnlyList<ObjectInteraction> features)
        {
            // Independent documents keep each interaction's typed paths available at the same time.
            Dispose();
            document = state;
            if (owner != target)
            {
                expanded.Clear();
                collapsed.Clear();
                expandAll = false;
            }
            owner = target;
            foreach (ObjectInteraction feature in features)
                branches.Add(ObjectBranchSession.Create(state, feature));
            BuildVisible();
        }

        /// <summary>Releases cached interaction snapshots when the object changes or its workspace closes.</summary>
        public void Dispose()
        {
            // Release every interaction snapshot before rebuilding the object scope.
            foreach (ObjectBranchSession branch in branches)
                branch.Dispose();
            branches.Clear();
        }

        /// <summary>Rebuilds visible nodes from cached documents after a branch arrow changes.</summary>
        private void BuildVisible()
        {
            // Expansion changes only layout; no draft is reselected, instantiated or serialized here.
            Nodes.Clear();
            if (owner == null)
            {
                Size = Vector2.zero;
                return;
            }
            Nodes.Add(new Node("Root", owner.name, "Open all interactions and their preset groups.", string.Empty, null, -1, 0, NodeKind.Root)
                { Expandable = branches.Count > 0 });
            if (Expanded(Nodes[0]))
                foreach (ObjectBranchSession branch in branches)
                    Interaction(branch);
            Measure(0);
            Position(0, 16f);
            Size = new Vector2(220f, Nodes[0].Height + 32f);
            foreach (Node node in Nodes)
                Size.x = Mathf.Max(Size.x, node.Rect.xMax + 20f);
        }

        /// <summary>Adds one interaction and exactly one node per reusable preset group.</summary>
        /// <param name="branch">Cached interaction proposal.</param>
        private void Interaction(ObjectBranchSession branch)
        {
            // Stable draft keys distinguish repeated types and preserve their expansion states.
            ObjectInteraction feature = branch.Feature;
            if (feature == null)
                return;
            int interaction = Add(new Node(branch.Document.DraftKey, feature.InteractionName + " · " + ObjectInteractionCatalog.Name(feature),
                "Open every preset group for this interaction.", string.Empty, null, 0, 1, NodeKind.Interaction)
                { Branch = branch, Expandable = branch.Groups.Count > 0 });
            if (!Expanded(Nodes[interaction]))
                return;
            foreach (ObjectGroupInfo group in branch.Groups)
            {
                if (!string.IsNullOrEmpty(ObjectGroupAvailability.Requirement(branch.Data, group, out _)))
                    continue;
                string assignment = branch.Document.DraftKey + "|" + group.Key;
                ObjectGroupPreset preset = document.GroupPresets.Find(entry => entry.Key == assignment)?.Preset;
                int theme = Add(new Node(Nodes[interaction].Key + "/" + group.Key, preset != null ? group.Title + " · " + preset.name : group.Title,
                    group.Tooltip, string.Empty, group, interaction, 2, NodeKind.Theme));
                Nodes[theme].Expandable = group.Paths.Length > 0;
                if (!Expanded(Nodes[theme]))
                    continue;
                Sections(branch.Data, group, theme);
            }
        }

        /// <summary>Combines split metadata into one third-level node per actual section dropdown.</summary>
        /// <param name="data">Interaction document.</param>
        /// <param name="group">Single reusable preset group.</param>
        /// <param name="parent">Second-level preset node.</param>
        private void Sections(SerializedObject data, ObjectGroupInfo group, int parent)
        {
            // Several small field sets may belong to the same dropdown; display that dropdown only once.
            Dictionary<string, List<string>> paths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Dictionary<string, ObjectGroupInfo> sections = new Dictionary<string, ObjectGroupInfo>(StringComparer.Ordinal);
            foreach (ObjectGroupInfo section in group.Segments.Length > 0 ? group.Segments : new[] { group })
            {
                if (!string.IsNullOrEmpty(ObjectGroupAvailability.Requirement(data, section, out _)))
                    continue;
                string key = ObjectFieldFocus.SectionKey(section.Parent, section.Section);
                if (!paths.TryGetValue(key, out List<string> fields))
                {
                    fields = new List<string>();
                    paths.Add(key, fields);
                    sections.Add(key, section);
                }
                foreach (string path in section.Paths)
                    if (!fields.Contains(path))
                        fields.Add(path);
            }
            foreach (KeyValuePair<string, List<string>> section in paths)
            {
                ObjectGroupInfo info = sections[section.Key];
                int index = Add(new Node(Nodes[parent].Key + "/" + section.Key, info.Section, info.Tooltip,
                    section.Key, group, parent, 3, NodeKind.Section) { Expandable = section.Value.Count > 0 });
                if (Expanded(Nodes[index]))
                    Fields(data, section.Value.ToArray(), group, index, 4);
            }
        }

        /// <summary>Toggles one branch while retaining every other expansion choice.</summary>
        /// <param name="node">Branch to toggle.</param>
        internal void Toggle(Node node)
        {
            if (expandAll)
            {
                foreach (Node visible in Nodes)
                    if (visible.Expandable)
                        expanded.Add(visible.Key);
                expandAll = false;
            }
            bool open = Expanded(node);
            expanded.Remove(node.Key);
            collapsed.Remove(node.Key);
            if (open)
                collapsed.Add(node.Key);
            else
                expanded.Add(node.Key);
            BuildVisible();
        }

        /// <summary>Expands all supported option branches or returns to the clear initial hierarchy.</summary>
        /// <param name="all">Whether linked and nested branches should be expanded.</param>
        internal void Expand(bool all)
        {
            expandAll = all;
            expanded.Clear();
            collapsed.Clear();
            BuildVisible();
        }

        /// <summary>Reports whether a block currently shows its children.</summary>
        /// <param name="node">Settings block.</param>
        /// <returns>True for an expanded branch.</returns>
        internal bool Expanded(Node node)
        {
            return !collapsed.Contains(node.Key) && (expandAll || expanded.Contains(node.Key) || node.Depth == 0);
        }

        /// <summary>Adds option names while retaining exact paths for editor navigation.</summary>
        /// <param name="data">Document owning the options.</param>
        /// <param name="paths">Section fields.</param>
        /// <param name="group">Thematic editor.</param>
        /// <param name="parent">Section block index.</param>
        /// <param name="depth">Option level.</param>
        private void Fields(SerializedObject data, string[] paths, ObjectGroupInfo group, int parent, int depth)
        {
            foreach (string path in paths)
            {
                using SerializedProperty field = data.FindProperty(path);
                if (field != null)
                    Field(field, group, parent, depth, null);
            }
        }

        /// <summary>Adds a block to both rendering order and its parent's child list.</summary>
        /// <param name="node">Block to append.</param>
        /// <returns>The block index.</returns>
        private int Add(Node node)
        {
            int index = Nodes.Count;
            node.Branch ??= Nodes[node.Parent].Branch;
            Nodes.Add(node);
            Nodes[node.Parent].Children.Add(index);
            return index;
        }

        /// <summary>Expands arrays, structs and supported preset references without changing their values.</summary>
        /// <param name="field">Serialized option.</param>
        /// <param name="group">Containing group editor.</param>
        /// <param name="parent">Parent block index.</param>
        /// <param name="depth">Tree level.</param>
        /// <param name="asset">Optional linked preset owning this option.</param>
        private void Field(SerializedProperty field, ObjectGroupInfo group, int parent, int depth, ScriptableObject asset)
        {
            if (Nodes.Count >= 4096 || !ObjectTreeFieldVisibility.IsVisible(field))
                return;
            ScriptableObject reference = field.propertyType == SerializedPropertyType.ObjectReference ? field.objectReferenceValue as ScriptableObject : null;
            if (reference != null && !ObjectSettingsTreeNavigation.Supports(reference))
                reference = null;
            string label = field.name switch
            {
                "TargetId" => "Target interaction",
                "ReplacementId" => "Replacement interaction",
                "OrdersCompletionId" => "Completion source",
                "SourceIds" or "TargetIds" => "Related interactions",
                _ => reference != null && field.propertyPath.EndsWith("]", StringComparison.Ordinal) ? reference.name : field.displayName
            };
            int index = Add(new Node(Nodes[parent].Key + "/" + field.propertyPath, label, ObjectFieldText.Tooltip(field), field.propertyPath, group, parent, depth, NodeKind.Field, asset));
            bool array = field.isArray && field.propertyType != SerializedPropertyType.String;
            Nodes[index].Reference = reference;
            Nodes[index].Expandable = depth < 12 && (reference != null ? !Repeated(reference, parent)
                : array ? field.arraySize > 0 : field.propertyType != SerializedPropertyType.String && field.hasVisibleChildren);
            if (!Nodes[index].Expandable || !Expanded(Nodes[index]))
                return;
            if (reference != null)
            {
                Preset(reference, group, index, depth + 1);
                return;
            }
            if (array)
            {
                for (int item = 0; item < field.arraySize && Nodes.Count < 4096; item++)
                    using (SerializedProperty child = field.GetArrayElementAtIndex(item))
                        Field(child, group, index, depth + 1, asset);
                return;
            }
            using SerializedProperty current = field.Copy();
            using SerializedProperty end = field.GetEndProperty();
            if (!current.NextVisible(true))
                return;
            do
            {
                if (current.depth <= field.depth || SerializedProperty.EqualContents(current, end))
                    break;
                Field(current, group, index, depth + 1, asset);
            }
            while (current.NextVisible(false));
        }

        /// <summary>Shows the option names inside a referenced entry, catalog or interaction preset.</summary>
        /// <param name="asset">Referenced settings asset.</param>
        /// <param name="group">Original group for navigation context.</param>
        /// <param name="parent">Reference block index.</param>
        /// <param name="depth">Nested preset level.</param>
        private void Preset(ScriptableObject asset, ObjectGroupInfo group, int parent, int depth)
        {
            using SerializedObject data = new SerializedObject(asset);
            using SerializedProperty field = data.GetIterator();
            if (!field.NextVisible(true))
                return;
            do
            {
                if (!field.name.StartsWith("m_", StringComparison.Ordinal))
                    Field(field, group, parent, depth, asset);
            }
            while (field.NextVisible(false));
        }

        /// <summary>Stops reference cycles such as dialogue entries returning to an earlier preset.</summary>
        /// <param name="asset">Reference about to expand.</param>
        /// <param name="parent">Ancestor to inspect.</param>
        /// <returns>True when the preset is already on this branch.</returns>
        private bool Repeated(ScriptableObject asset, int parent)
        {
            for (int index = parent; index >= 0; index = Nodes[index].Parent)
                if (Nodes[index].Asset == asset || Nodes[index].Reference == asset)
                    return true;
            return false;
        }

        #endregion

        #region Arrangement

        /// <summary>Measures branches without overlapping sibling blocks.</summary>
        /// <param name="index">Branch root.</param>
        /// <returns>Reserved branch height.</returns>
        private float Measure(int index)
        {
            float height = 0f;
            foreach (int child in Nodes[index].Children)
                height += Measure(child) + 12f;
            Nodes[index].Height = Mathf.Max(38f, height > 0f ? height - 12f : 0f);
            return Nodes[index].Height;
        }

        /// <summary>Centers parents along their visible descendants.</summary>
        /// <param name="index">Block to arrange.</param>
        /// <param name="top">Branch origin.</param>
        private void Position(int index, float top)
        {
            Node node = Nodes[index];
            node.Rect = new Rect(16f + node.Depth * 230f, top + (node.Height - 38f) * 0.5f, 192f, 38f);
            float offset = top;
            foreach (int child in node.Children)
            {
                Position(child, offset);
                offset += Nodes[child].Height + 12f;
            }
        }

        #endregion

        #endregion
    }
}
