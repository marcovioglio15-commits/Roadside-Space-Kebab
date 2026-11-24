using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Builds the shared-reference graph once and filters its cached nodes without rebuilding paths.</summary>
    internal sealed class ObjectFlagTree
    {
        #region Types

        /// <summary>Retains indexed navigation, current visibility and actual edit targets.</summary>
        private sealed class Node
        {
            internal readonly SortedDictionary<string, Node> Children = new SortedDictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<ObjectFlagField> Fields = new List<ObjectFlagField>();
            internal Node Parent;
            internal string SearchText = string.Empty;
            internal bool Visible;
        }

        #endregion
        #region State

        private readonly Node root = new Node();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly HashSet<ObjectFlagField> filtered = new HashSet<ObjectFlagField>();
        private readonly List<Node> rows = new List<Node>();
        private readonly List<Node> visibleNodes = new List<Node>();
        private readonly Dictionary<ObjectFlagField, string> flagNames = new Dictionary<ObjectFlagField, string>();
        private readonly HashSet<ObjectFlagField> assigned = new HashSet<ObjectFlagField>();
        private int flagRevision = -1;
        private bool searchActive;
        private bool validation;
        private Vector2 scroll;

        #endregion
        #region Properties

        internal IEnumerable<ObjectFlagField> FilteredFields => filtered;

        #endregion
        #region Methods
        #region Indexing

        /// <summary>Indexes source fields and contextual shared references only after scan or scope changes.</summary>
        /// <param name="scan">Cached explicit scan.</param>
        /// <param name="isValidation">Whether only missing flag selections should be shown.</param>
        /// <param name="follow">Whether shared settings appear beneath their consumers.</param>
        /// <param name="savedExpanded">Preset navigation state.</param>
        /// <param name="target">Optional root asset or exact preset subasset.</param>
        internal void Build(ObjectFlagScan scan, bool isValidation, bool follow, List<string> savedExpanded, UnityEngine.Object target)
        {
            root.Children.Clear();
            root.Fields.Clear();
            rows.Clear();
            visibleNodes.Clear();
            flagNames.Clear();
            flagRevision = -1;
            expanded.Clear();
            expanded.UnionWith(savedExpanded);
            validation = isValidation;
            ObjectFlagField[] fields = scan.Fields.Where(field => field.IsFlag && (!validation || field.Missing > 0)).ToArray();
            string assetPath = target != null ? AssetDatabase.GetAssetPath(target) : string.Empty;
            string exact = target != null && target is ScriptableObject && !AssetDatabase.IsMainAsset(target)
                ? assetPath + ":" + ObjectFlagAssetAccess.Id(target) : string.Empty;
            foreach (ObjectFlagField field in fields)
                if (RootOwner(field.OwnerKey, assetPath, exact))
                    Add(field.Root + "/" + field.Label, field);
            if (!follow)
                return;
            Dictionary<string, ObjectFlagField[]> byOwner = fields.GroupBy(field => field.OwnerKey).ToDictionary(group => group.Key, group => group.ToArray());
            Dictionary<string, ObjectFlagLink[]> links = scan.Links.GroupBy(link => link.Owner).ToDictionary(group => group.Key, group => group.ToArray());
            Dictionary<string, List<string>> parents = new Dictionary<string, List<string>>();
            foreach (ObjectFlagLink link in scan.Links)
            {
                if (!parents.TryGetValue(link.Target, out List<string> sources))
                {
                    sources = new List<string>();
                    parents.Add(link.Target, sources);
                }
                sources.Add(link.Owner);
            }
            // Reverse traversal visits each owner once instead of repeatedly rescanning every edge.
            HashSet<string> relevant = new HashSet<string>(byOwner.Keys);
            Queue<string> pending = new Queue<string>(relevant);
            while (pending.Count > 0)
                if (parents.TryGetValue(pending.Dequeue(), out List<string> sources))
                    foreach (string source in sources)
                        if (relevant.Add(source))
                            pending.Enqueue(source);
            foreach (ObjectFlagLink link in scan.Links)
                if (relevant.Contains(link.Target) && RootOwner(link.Owner, assetPath, exact))
                    Follow(link.Target, link.Root + "/" + link.Label, byOwner, links, relevant, new HashSet<string> { link.Owner });
        }

        /// <summary>Restricts graph roots while retaining shared fields as contextual descendants.</summary>
        /// <param name="owner">Candidate owner key.</param>
        /// <param name="path">Selected asset path, or empty for the project.</param>
        /// <param name="exact">Optional exact preset subasset key.</param>
        /// <returns>True when this owner can be a visible root.</returns>
        private static bool RootOwner(string owner, string path, string exact)
        {
            return exact.Length > 0 ? owner == exact : path.Length == 0 || owner.StartsWith(path + ":", StringComparison.Ordinal);
        }

        /// <summary>Projects shared selections beneath a consumer and stops reference cycles.</summary>
        /// <param name="owner">Shared owner identity.</param>
        /// <param name="prefix">Consumer path.</param>
        /// <param name="fields">Selections indexed by owner.</param>
        /// <param name="links">Further shared references.</param>
        /// <param name="relevant">Owners leading to flag selections.</param>
        /// <param name="stack">Owners already visited on this path.</param>
        private void Follow(string owner, string prefix, Dictionary<string, ObjectFlagField[]> fields,
            Dictionary<string, ObjectFlagLink[]> links, HashSet<string> relevant, HashSet<string> stack)
        {
            if (!stack.Add(owner))
                return;
            if (fields.TryGetValue(owner, out ObjectFlagField[] selections))
                foreach (ObjectFlagField field in selections)
                    Add(prefix + "/" + field.Label, field);
            if (links.TryGetValue(owner, out ObjectFlagLink[] references))
                foreach (ObjectFlagLink link in references)
                    if (relevant.Contains(link.Target))
                        Follow(link.Target, prefix + "/" + link.Label, fields, links, relevant, stack);
            stack.Remove(owner);
        }

        /// <summary>Adds one contextual path and caches searchable labels on its nodes.</summary>
        /// <param name="path">Complete named path.</param>
        /// <param name="field">Actual source selection.</param>
        private void Add(string path, ObjectFlagField field)
        {
            Node current = root;
            foreach (string segment in path.Split('/'))
            {
                if (!current.Children.TryGetValue(segment, out Node child))
                {
                    child = new Node { Parent = current };
                    current.Children.Add(segment, child);
                }
                current = child;
            }
            if (current.Fields.Count == 0)
                rows.Add(current);
            if (!current.Fields.Contains(field))
                current.Fields.Add(field);
            current.SearchText = (path + " " + field.AssetPath).ToLowerInvariant();
        }

        #endregion
        #region Filtering

        /// <summary>Updates visibility in place without querying assets or expanding the reference graph again.</summary>
        /// <param name="search">Current text filter.</param>
        /// <param name="session">Proposal supplying pending cleanup state.</param>
        /// <param name="flagSearch">Assigned flag display-name filter.</param>
        /// <param name="hideUnassigned">Whether fields with no valid assigned flags are hidden.</param>
        internal void Filter(string search, ObjectFlagDraftSession session, string flagSearch = "", bool hideUnassigned = false)
        {
            filtered.Clear();
            foreach (Node node in visibleNodes)
                node.Visible = false;
            visibleNodes.Clear();
            searchActive = !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(flagSearch);
            string query = search.Trim().ToLowerInvariant();
            string flags = flagSearch.Trim().ToLowerInvariant();
            if (!validation && (flags.Length > 0 || hideUnassigned))
                IndexFlags(session);
            // Search only final rows. Matching rows reveal each ancestor at most once.
            foreach (Node node in rows)
            {
                if (query.Length > 0 && node.SearchText.IndexOf(query, StringComparison.Ordinal) < 0)
                    continue;
                bool visible = false;
                foreach (ObjectFlagField field in node.Fields)
                    if (validation ? session.Missing(field) > 0
                        : (!hideUnassigned || assigned.Contains(field))
                        && (flags.Length == 0 || flagNames[field].IndexOf(flags, StringComparison.Ordinal) >= 0))
                    {
                        visible = true;
                        filtered.Add(field);
                    }
                if (!visible)
                    continue;
                for (Node current = node; current != null && !current.Visible; current = current.Parent)
                {
                    current.Visible = true;
                    visibleNodes.Add(current);
                }
            }
        }

        /// <summary>Caches current flag labels once per draft revision, including staged replacements and renames.</summary>
        /// <param name="session">Proposal supplying current memberships and display names.</param>
        private void IndexFlags(ObjectFlagDraftSession session)
        {
            if (flagRevision == session.Revision)
                return;
            flagNames.Clear();
            assigned.Clear();
            foreach (Node node in rows)
                foreach (ObjectFlagField field in node.Fields)
                {
                    if (flagNames.ContainsKey(field))
                        continue;
                    StringBuilder names = new StringBuilder();
                    foreach (ObjectFlag flag in session.Values(field))
                        if (flag != null)
                        {
                            assigned.Add(field);
                            names.Append(session.Definition(flag).DisplayName.ToLowerInvariant()).Append('\n');
                        }
                    flagNames.Add(field, names.ToString());
                }
            flagRevision = session.Revision;
        }

        #endregion
        #region Navigation

        /// <summary>Expands all indexed branches and retains their state when searches are cleared.</summary>
        /// <param name="savedExpanded">Window navigation receiving expanded paths.</param>
        internal void ExpandAll(List<string> savedExpanded)
        {
            Expand(root, string.Empty);
            savedExpanded.Clear();
            savedExpanded.AddRange(expanded.OrderBy(path => path, StringComparer.Ordinal));
        }

        /// <summary>Visits each branch once when the explicit Expand All command is used.</summary>
        /// <param name="node">Parent branch.</param>
        /// <param name="prefix">Parent navigation key.</param>
        private void Expand(Node node, string prefix)
        {
            foreach (KeyValuePair<string, Node> child in node.Children)
            {
                string path = prefix + "/" + child.Key;
                expanded.Add(path);
                Expand(child.Value, path);
            }
        }

        #endregion
        #region Drawing

        /// <summary>Draws cached visible branches above the main window's persistent action footer.</summary>
        /// <param name="savedExpanded">Preset navigation state.</param>
        /// <param name="session">Shared proposal.</param>
        /// <param name="changed">Refresh callback after a staged choice.</param>
        /// <param name="clean">Stages cleanup of one result.</param>
        /// <returns>True when navigation state changed.</returns>
        internal bool Draw(List<string> savedExpanded, ObjectFlagDraftSession session, Action changed, Action<ObjectFlagField> clean)
        {
            using EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            bool navigationChanged = DrawNode(root, "", 0, session, changed, clean);
            if (navigationChanged)
            {
                savedExpanded.Clear();
                savedExpanded.AddRange(expanded.OrderBy(path => path, StringComparer.Ordinal));
            }
            return navigationChanged;
        }

        /// <summary>Visits only visible, expanded branches during ordinary repaint.</summary>
        /// <param name="node">Indexed branch.</param>
        /// <param name="prefix">Parent navigation key.</param>
        /// <param name="depth">Indentation depth.</param>
        /// <param name="session">Shared proposal.</param>
        /// <param name="changed">Proposal refresh callback.</param>
        /// <param name="clean">Single-result cleanup callback.</param>
        /// <returns>True when a foldout changed.</returns>
        private bool DrawNode(Node node, string prefix, int depth, ObjectFlagDraftSession session, Action changed, Action<ObjectFlagField> clean)
        {
            bool navigationChanged = false;
            foreach (KeyValuePair<string, Node> child in node.Children)
            {
                if (!child.Value.Visible)
                    continue;
                string key = prefix + "/" + child.Key;
                EditorGUI.indentLevel = depth;
                bool opened = searchActive || expanded.Contains(key);
                bool next = EditorGUILayout.Foldout(opened, new GUIContent(child.Key, key.TrimStart('/')), true);
                if (next != opened && !searchActive)
                {
                    if (next)
                        expanded.Add(key);
                    else
                        expanded.Remove(key);
                    navigationChanged = true;
                }
                if (!next)
                    continue;
                foreach (ObjectFlagField field in child.Value.Fields)
                    if (filtered.Contains(field))
                        DrawField(field, session, changed, clean, depth + 1);
                navigationChanged |= DrawNode(child.Value, key, depth + 1, session, changed, clean);
            }
            EditorGUI.indentLevel = 0;
            return navigationChanged;
        }

        /// <summary>Stages a leaf selection and opens its existing package editor on Ping.</summary>
        /// <param name="field">Actual source field.</param>
        /// <param name="session">Shared proposal.</param>
        /// <param name="changed">Proposal refresh callback.</param>
        /// <param name="clean">Cleanup callback.</param>
        /// <param name="depth">Indentation depth.</param>
        private void DrawField(ObjectFlagField field, ObjectFlagDraftSession session, Action changed, Action<ObjectFlagField> clean, int depth)
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            GUILayout.Space(depth * 14f);
            if (validation)
            {
                EditorGUILayout.LabelField(new GUIContent(session.Missing(field) + " missing flag reference(s)", field.Location));
                if (GUILayout.Button(new GUIContent("Clean", "Stage removal of missing flags in this field."), GUILayout.Width(55f)))
                    clean(field);
            }
            else
                ObjectFlagSelectionGUI.Draw(field, changed, session);
            if (GUILayout.Button(new GUIContent("Ping ↗", "Open the editor for this exact field owner.\n" + field.Location), GUILayout.Width(62f)))
                ObjectFlagAssignment.Ping(field);
        }

        #endregion
        #endregion
    }
}
