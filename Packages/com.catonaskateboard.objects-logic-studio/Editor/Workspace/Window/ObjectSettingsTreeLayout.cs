using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds a detailed settings hierarchy, including sections, nested options and linked presets.</summary>
    internal sealed class ObjectSettingsTreeLayout
    {
        #region Types

        /// <summary>Distinguishes structural headings from individual option blocks.</summary>
        internal enum NodeKind { Root, Theme, Section, Field }

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
        private List<ObjectGroupInfo> groups;
        private bool expandAll;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Builds the selected type with themes, sections and their option names visible by default.</summary>
        /// <param name="state">Selected draft.</param>
        /// <param name="settings">Thematic groups belonging to that draft.</param>
        internal void Build(ObjectWorkspace state, List<ObjectGroupInfo> settings)
        {
            document = state;
            groups = settings;
            Nodes.Clear();
            ObjectInteraction feature = ObjectWorkspaceDrafts.Selected(state);
            if (feature == null)
            {
                Size = Vector2.zero;
                return;
            }
            Nodes.Add(new Node("Root", ObjectInteractionCatalog.Name(feature), feature.InteractionName, string.Empty, null, -1, 0, NodeKind.Root));
            using SerializedObject data = new SerializedObject(state);
            foreach (ObjectGroupInfo group in settings)
            {
                if (!string.IsNullOrEmpty(ObjectGroupAvailability.Requirement(data, group, out _)))
                    continue;
                int theme = Add(new Node(group.Key, group.Title, group.Tooltip, string.Empty, group, 0, 1, NodeKind.Theme));
                Nodes[theme].Expandable = group.Paths.Length > 0;
                if (!Expanded(Nodes[theme]))
                    continue;
                if (group.Segments is not { Length: > 0 })
                {
                    Fields(data, group.Paths, group, theme, 2);
                    continue;
                }
                foreach (ObjectGroupInfo section in group.Segments)
                {
                    if (!string.IsNullOrEmpty(ObjectGroupAvailability.Requirement(data, section, out _)))
                        continue;
                    int branch = Add(new Node(group.Key + "/" + section.Key, section.Title, section.Tooltip, string.Empty, group, theme, 2, NodeKind.Section));
                    Nodes[branch].Expandable = section.Paths.Length > 0;
                    if (Expanded(Nodes[branch]))
                        Fields(data, section.Paths, group, branch, 3);
                }
            }
            Measure(0);
            Position(0, 16f);
            Size = new Vector2(220f, Nodes[0].Height + 32f);
            foreach (Node node in Nodes)
                Size.x = Mathf.Max(Size.x, node.Rect.xMax + 20f);
        }

        /// <summary>Expands option blocks only after a branch arrow is clicked.</summary>
        /// <param name="node">Branch to toggle.</param>
        internal void Toggle(Node node)
        {
            if (expandAll)
            {
                foreach (Node visible in Nodes)
                    if (visible.Expandable && visible.Kind == NodeKind.Field)
                        expanded.Add(visible.Key);
                expandAll = false;
            }
            HashSet<string> keys = node.Kind == NodeKind.Field ? expanded : collapsed;
            if (!keys.Add(node.Key))
                keys.Remove(node.Key);
            Build(document, groups);
        }

        /// <summary>Expands all supported option branches or returns to the clear initial hierarchy.</summary>
        /// <param name="all">Whether linked and nested branches should be expanded.</param>
        internal void Expand(bool all)
        {
            expandAll = all;
            expanded.Clear();
            collapsed.Clear();
            Build(document, groups);
        }

        /// <summary>Reports whether a block currently shows its children.</summary>
        /// <param name="node">Settings block.</param>
        /// <returns>True for an expanded branch.</returns>
        internal bool Expanded(Node node)
        {
            return expandAll || (node.Kind == NodeKind.Field ? expanded.Contains(node.Key) : !collapsed.Contains(node.Key));
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
            if (Nodes.Count >= 4096)
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
