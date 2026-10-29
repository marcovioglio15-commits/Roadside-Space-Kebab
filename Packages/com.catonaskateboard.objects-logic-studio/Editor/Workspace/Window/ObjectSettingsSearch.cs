using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Indexes names and settings at refresh boundaries and applies simple catalog filters.</summary>
    internal static class ObjectSettingsSearch
    {
        #region Types

        /// <summary>Keeps searchable text for one existing interaction.</summary>
        private sealed class Entry
        {
            internal string Text;
            internal string Name;
            internal string Type;
            internal string Tool = string.Empty;
            internal string Action = string.Empty;
            internal string Preset = string.Empty;
        }

        #endregion

        #region State

        private static readonly Dictionary<ObjectInteraction, Entry> entries = new Dictionary<ObjectInteraction, Entry>();
        private static readonly Dictionary<string, string> groupText = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> tools = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly SortedSet<string> actions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly SortedSet<string> presets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex words = new Regex("(?:[^\\s\"]+|\"[^\"]*\")+", RegexOptions.Compiled);
        private static string lastQuery;
        private static string[] tokens = Array.Empty<string>();

        #endregion

        #region Methods

        #region Index

        /// <summary>Refreshes the current object's index after selection, hierarchy or asset changes.</summary>
        /// <param name="state">Workspace containing the selected draft.</param>
        /// <param name="interactions">Components inside the browsing scope.</param>
        /// <param name="groups">Selected interaction's thematic groups.</param>
        internal static void Refresh(ObjectWorkspace state, ObjectInteraction[] interactions, List<ObjectGroupInfo> groups)
        {
            entries.Clear();
            groupText.Clear();
            tools.Clear();
            actions.Clear();
            presets.Clear();
            foreach (ObjectInteraction feature in interactions)
            {
                ObjectInteractionCatalog.Entry definition = ObjectInteractionCatalog.Find(feature.GetType());
                StringBuilder text = new StringBuilder(feature.InteractionName + " " + definition.Name + " " + definition.Keywords);
                Entry entry = new Entry { Type = definition.Name.ToLowerInvariant(), Name = (feature.InteractionName + " " + definition.Name).ToLowerInvariant() };
                using SerializedObject data = new SerializedObject(feature);
                using SerializedProperty field = data.GetIterator();
                while (field.Next(true))
                {
                    text.Append(' ').Append(field.displayName).Append(' ').Append(field.propertyPath).Append(' ').Append(field.tooltip);
                    if (field.propertyType != SerializedPropertyType.ObjectReference || field.objectReferenceValue == null)
                        continue;
                    UnityEngine.Object reference = field.objectReferenceValue;
                    text.Append(' ').Append(reference.name);
                    switch (reference)
                    {
                        case CatOnASkateboard.PlayerStudio.PlayerTool tool:
                            entry.Tool += " " + tool.name + " " + tool.DisplayName;
                            tools.Add(tool.name);
                            break;
                        case UnityEngine.InputSystem.InputActionReference action:
                            entry.Action += " " + action.name + " " + action.action?.name;
                            actions.Add(action.action?.name ?? action.name);
                            break;
                    }
                    if (field.name is "settingsPreset" or "preset")
                    {
                        entry.Preset += " " + reference.name;
                        presets.Add(reference.name);
                    }
                }
                if (ObjectWorkspaceTarget.FileId(feature) == state.Navigation.SelectedId)
                    foreach (ObjectGroupInfo group in groups)
                        text.Append(' ').Append(Describe(group, null));
                else
                {
                    ObjectWorkspace document = ObjectPresetCapture.Document(feature);
                    try
                    {
                        using SerializedObject fields = new SerializedObject(document);
                        foreach (ObjectGroupInfo group in ObjectInteractionPanel.Groups(document, fields))
                            text.Append(' ').Append(Describe(group, null));
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(document);
                    }
                }
                entry.Text = Expand(text.ToString());
                entry.Tool = entry.Tool.ToLowerInvariant();
                entry.Action = entry.Action.ToLowerInvariant();
                entry.Preset = entry.Preset.ToLowerInvariant();
                entries[feature] = entry;
            }
            using SerializedObject workspace = new SerializedObject(state);
            foreach (ObjectGroupInfo group in groups)
                groupText[group.Key] = Describe(group, workspace);
        }

        /// <summary>Includes inner section names and fields in settings navigation.</summary>
        /// <param name="group">Thematic settings group.</param>
        /// <param name="data">Optional document for field descriptions.</param>
        /// <returns>Lowercase settings text with common synonyms.</returns>
        internal static string Describe(ObjectGroupInfo group, SerializedObject data)
        {
            StringBuilder text = new StringBuilder(group.Title + " " + group.Tooltip);
            if (group.Segments != null)
                foreach (ObjectGroupInfo segment in group.Segments)
                    text.Append(' ').Append(segment.Title);
            foreach (string path in group.Paths)
            {
                text.Append(' ').Append(path);
                if (data == null)
                    continue;
                using SerializedProperty property = data.FindProperty(path);
                if (property == null)
                    continue;
                text.Append(' ').Append(property.displayName).Append(' ').Append(property.tooltip);
                using SerializedProperty child = property.Copy();
                using SerializedProperty end = property.GetEndProperty();
                while (child.Next(true) && child.depth > property.depth && !SerializedProperty.EqualContents(child, end))
                    text.Append(' ').Append(child.displayName).Append(' ').Append(child.propertyPath).Append(' ').Append(child.tooltip);
            }
            return Expand(text.ToString());
        }

        #endregion

        #region Matching

        /// <summary>Sorts by type or name with stable component identities as the final tie-breaker.</summary>
        /// <param name="left">First component.</param>
        /// <param name="right">Second component.</param>
        /// <param name="order">Requested grouping.</param>
        /// <returns>The relative list order.</returns>
        internal static int Compare(ObjectInteraction left, ObjectInteraction right, ObjectStudioNavigation.Order order)
        {
            int result = order == ObjectStudioNavigation.Order.Name ? 0
                : string.Compare(ObjectInteractionCatalog.Name(left), ObjectInteractionCatalog.Name(right), StringComparison.OrdinalIgnoreCase);
            if (result == 0)
                result = string.Compare(left.InteractionName, right.InteractionName, StringComparison.OrdinalIgnoreCase);
            return result != 0 ? result : ObjectWorkspaceTarget.FileId(left).CompareTo(ObjectWorkspaceTarget.FileId(right));
        }

        /// <summary>Applies category, enabled state and text filters to a cached component.</summary>
        /// <param name="state">Current filter preferences.</param>
        /// <param name="feature">Existing component.</param>
        /// <returns>True when the component is visible.</returns>
        internal static bool Matches(ObjectWorkspace state, ObjectInteraction feature)
        {
            if (feature == null || (state.Navigation.Filters & ObjectInteractionCatalog.Find(feature.GetType()).Category) == 0
                || !entries.TryGetValue(feature, out Entry entry))
                return false;
            if (state.Navigation.State == ObjectStudioNavigation.Status.Enabled && !feature.enabled
                || state.Navigation.State == ObjectStudioNavigation.Status.Disabled && feature.enabled)
                return false;
            foreach (string token in Tokens(state.Navigation.Search))
            {
                int separator = token.IndexOf(':');
                string key = separator > 0 ? token.Substring(0, separator) : string.Empty;
                string term = (separator > 0 ? token.Substring(separator + 1) : token).Trim('"');
                string text = key switch
                {
                    "type" => entry.Type,
                    "tool" => entry.Tool,
                    "action" => entry.Action,
                    "preset" => entry.Preset,
                    "group" or "setting" => entry.Text,
                    _ => entry.Name
                };
                if (!text.Contains(term))
                    return false;
            }
            return true;
        }

        /// <summary>Narrows settings groups only for explicit group or setting search tokens.</summary>
        /// <param name="group">Thematic group.</param>
        /// <param name="query">Search terms.</param>
        /// <returns>True when this group contains a match.</returns>
        internal static bool MatchesGroup(ObjectGroupInfo group, string query)
        {
            string text = groupText.TryGetValue(group.Key, out string found) ? found : Describe(group, null);
            foreach (string token in Tokens(query))
            {
                int separator = token.IndexOf(':');
                if (separator < 1 || !(token.StartsWith("group:", StringComparison.Ordinal) || token.StartsWith("setting:", StringComparison.Ordinal)))
                    continue;
                string term = token.Substring(separator + 1).Trim('"');
                if (!text.Contains(term))
                    return false;
            }
            return true;
        }

        /// <summary>Parses optional quoted terms only when the search text changes.</summary>
        /// <param name="query">Current search.</param>
        /// <returns>Lowercase terms.</returns>
        private static string[] Tokens(string query)
        {
            if (query == lastQuery)
                return tokens;
            lastQuery = query;
            MatchCollection matches = words.Matches(query ?? string.Empty);
            tokens = new string[matches.Count];
            for (int index = 0; index < matches.Count; index++)
                tokens[index] = matches[index].Value.ToLowerInvariant();
            return tokens;
        }

        /// <summary>Adds familiar synonyms to the index.</summary>
        /// <param name="text">Names and descriptions.</param>
        /// <returns>Expanded lowercase search text.</returns>
        private static string Expand(string text)
        {
            return text.ToLowerInvariant().Replace("distance", "distance range reach").Replace("grab", "grab pickup pick up")
                .Replace("collision", "collision walls obstacles").Replace("duration", "duration time seconds");
        }

        #endregion

        #region Filter menus

        /// <summary>Adds tool, input and preset-name checks from the current object's index.</summary>
        /// <param name="menu">Filter menu.</param>
        /// <param name="state">Workspace retaining search tokens.</param>
        internal static void Filters(GenericMenu menu, ObjectWorkspace state)
        {
            AddFilter(menu, state, "Tool", "tool", tools);
            AddFilter(menu, state, "Input action", "action", actions);
            AddFilter(menu, state, "Preset", "preset", presets);
        }

        /// <summary>Offers indexed names without another asset scan.</summary>
        /// <param name="menu">Filter menu.</param>
        /// <param name="state">Workspace retaining the query.</param>
        /// <param name="label">Menu heading.</param>
        /// <param name="key">Token key.</param>
        /// <param name="values">Known names.</param>
        private static void AddFilter(GenericMenu menu, ObjectWorkspace state, string label, string key, SortedSet<string> values)
        {
            menu.AddItem(new GUIContent(label + "/Any"), false, () => SetToken(state, key, string.Empty));
            foreach (string value in values)
            {
                string choice = value;
                menu.AddItem(new GUIContent(label + "/" + value), false, () => SetToken(state, key, choice));
            }
        }

        /// <summary>Replaces one reference filter while preserving the remaining search text.</summary>
        /// <param name="state">Workspace retaining the query.</param>
        /// <param name="key">Token key.</param>
        /// <param name="value">Selected name or empty to clear.</param>
        private static void SetToken(ObjectWorkspace state, string key, string value)
        {
            List<string> kept = new List<string>();
            foreach (Match match in words.Matches(state.Navigation.Search))
                if (!match.Value.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                    kept.Add(match.Value);
            if (!string.IsNullOrEmpty(value))
                kept.Add(key + ":\"" + value.Replace("\"", string.Empty) + "\"");
            state.Navigation.Search = string.Join(" ", kept);
            state.Persist();
            ObjectsLogicStudioWindow.RefreshOpen();
        }

        #endregion

        #endregion
    }
}
