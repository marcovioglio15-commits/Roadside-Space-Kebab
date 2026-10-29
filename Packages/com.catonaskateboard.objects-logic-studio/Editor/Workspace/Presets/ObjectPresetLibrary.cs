using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Browses reusable presets for the interaction types visible in the adjacent catalog.</summary>
    internal sealed class ObjectPresetLibrary : IDisposable
    {
        #region State

        private readonly List<ScriptableObject> presets = new List<ScriptableObject>();
        private readonly List<ScriptableObject> matches = new List<ScriptableObject>();
        private UnityEditor.Editor inspector;
        private ScriptableObject selected;
        private string search = string.Empty;
        private string previousSearch;
        private bool dirty = true;
        private int contextHash;

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Schedules one project scan after an asset or browsing-context change.</summary>
        internal void Invalidate()
        {
            dirty = true;
        }

        /// <summary>Loads preset assets and subassets only when the project index changes.</summary>
        private void Refresh()
        {
            dirty = false;
            presets.Clear();
            HashSet<UnityEngine.Object> found = new HashSet<UnityEngine.Object>();
            string[] types = { "ObjectMasterPreset", "ObjectInteractionPreset", "ObjectGroupPreset", "HoverPreset", "SingleInteractionPreset", "ExtendedInteractionPreset", "DialogueEntry", "OrderCatalog", "UnlockRuleSetPreset" };
            foreach (string type in types)
                foreach (string guid in AssetDatabase.FindAssets("t:" + type))
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (asset is ScriptableObject preset && preset is ObjectMasterPreset or ObjectInteractionPreset or ObjectGroupPreset
                            or HoverPreset or SingleInteractionPreset or ExtendedInteractionPreset or DialogueEntry or OrderCatalog or UnlockRuleSetPreset && found.Add(asset))
                            presets.Add(preset);
            presets.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Rebuilds the filtered library only after its types or search text change.</summary>
        /// <param name="visible">Visible catalog components.</param>
        private void Filter(IReadOnlyList<ObjectInteraction> visible)
        {
            int hash = 17;
            foreach (ObjectInteraction feature in visible)
                hash = unchecked(hash * 31 + feature.GetType().GetHashCode());
            bool rescan = dirty;
            if (rescan)
                Refresh();
            if (!rescan && hash == contextHash && search == previousSearch)
                return;
            contextHash = hash;
            previousSearch = search;
            matches.Clear();
            foreach (ScriptableObject preset in presets)
                if (ObjectPresetFilter.Matches(preset, visible) && (string.IsNullOrWhiteSpace(search)
                    || preset.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || Label(preset).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    matches.Add(preset);
            if (selected != null && !matches.Contains(selected))
            {
                selected = null;
                Dispose();
            }
        }

        #endregion

        #region Drawing

        /// <summary>Shows a contextual library below the selected interaction's main preset controls.</summary>
        /// <param name="state">Workspace retaining foldouts and the selected master.</param>
        /// <param name="visible">Exact components shown by the catalog filters.</param>
        internal void Draw(ObjectWorkspace state, IReadOnlyList<ObjectInteraction> visible)
        {
            if (!ObjectWorkspaceGUI.Foldout(state, "Presets.Library", "Library", "Presets for the visible interaction types."))
                return;
            using StudioIndentScope sectionContent1 = new StudioIndentScope();
            search = EditorGUILayout.TextField(new GUIContent(string.Empty, "Find a matching preset."), search, EditorStyles.toolbarSearchField);
            Filter(visible);
            if (matches.Count == 0)
                EditorGUILayout.LabelField(visible.Count == 0 ? "No interaction types are visible." : "No matching presets.", EditorStyles.miniLabel);
            string[] sections = { "Object masters", "Main presets", "Settings groups", "Dialogue & orders" };
            foreach (string section in sections)
            {
                if (!matches.Exists(preset => Label(preset) == section)
                    || !ObjectWorkspaceGUI.Foldout(state, "Library." + section, section, "Show matching presets."))
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                foreach (ScriptableObject preset in matches)
                    if (Label(preset) == section && ObjectWorkspaceGUI.Row(preset.name, selected == preset))
                    {
                        selected = preset;
                        UnityEditor.Editor.CreateCachedEditor(selected, null, ref inspector);
                    }
            }
            if (selected == null)
                return;
            EditorGUILayout.Space(6f);
            if (ObjectWorkspaceGUI.Foldout(state, "Presets.Selected", selected.name, "Edit the selected reusable preset."))
            {
                using StudioIndentScope sectionContent3 = new StudioIndentScope();
                if (selected is ObjectMasterPreset master && GUILayout.Button(new GUIContent("Use for this object", "Choose this object master."), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    state.Master = master;
                    state.Navigation.Views |= ObjectStudioNavigation.Panels.Object;
                    state.Persist();
                }
                inspector?.OnInspectorGUI();
            }
        }

        /// <summary>Groups assets by their role rather than by internal class names.</summary>
        /// <param name="preset">Reusable asset.</param>
        /// <returns>The library dropdown heading.</returns>
        private static string Label(ScriptableObject preset)
        {
            return preset switch
            {
                ObjectMasterPreset => "Object masters",
                ObjectGroupPreset => "Settings groups",
                DialogueEntry or OrderCatalog => "Dialogue & orders",
                _ => "Main presets"
            };
        }

        #endregion

        #region Lifetime

        /// <summary>Releases the cached asset inspector without changing the selected preset.</summary>
        public void Dispose()
        {
            if (inspector != null)
                UnityEngine.Object.DestroyImmediate(inspector);
            inspector = null;
        }

        #endregion

        #endregion
    }
}
