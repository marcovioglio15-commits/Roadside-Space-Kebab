using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Previews master changes and lets the user resolve object slots before importing.</summary>
    internal sealed class ObjectMasterImportWindow : EditorWindow
    {
        #region Fields

        [Header("Import")]
        [Tooltip("Configuration to import.")]
        [SerializeField]
        private ObjectMasterPreset master;
        [Tooltip("Object receiving this configuration.")]
        [SerializeField]
        private GameObject destination;
        [Tooltip("Replace interactions in the selected object slots. Other components stay as they are.")]
        [SerializeField]
        private bool replace;
        [Tooltip("Also import flags and shared item settings.")]
        [SerializeField]
        private bool includeShared = true;
        [Tooltip("Interactions selected for import.")]
        [SerializeField]
        private ObjectImportChoices.Interaction[] choices = Array.Empty<ObjectImportChoices.Interaction>();
        [Tooltip("Object slots selected for import.")]
        [SerializeField]
        private List<ObjectImportChoices.Node> nodes = new List<ObjectImportChoices.Node>();
        [Tooltip("Replacement object links.")]
        [SerializeField]
        private List<ObjectImportChoices.Link> links = new List<ObjectImportChoices.Link>();
        [Tooltip("Scroll position in the import preview.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion

        #region State

        private ObjectMasterImportPlan plan;
        private string warning = string.Empty;
        private bool ready;
        private bool needsCheck = true;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Opens a concrete import preview for the selected master and object.</summary>
        /// <param name="preset">Master configuration.</param>
        /// <param name="target">Existing destination prefab-stage branch.</param>
        internal static void Open(ObjectMasterPreset preset, GameObject target)
        {
            ObjectMasterImportWindow window = CreateInstance<ObjectMasterImportWindow>();
            window.master = preset;
            window.destination = target;
            window.titleContent = new GUIContent("Import object configuration");
            window.minSize = new Vector2(470f, 380f);
            window.Initialize();
            window.Show();
        }

        /// <summary>Schedules isolated validation after editor reload.</summary>
        private void OnEnable()
        {
            needsCheck = true;
            EditorApplication.delayCall += Check;
        }

        /// <summary>Releases the isolated validation result when the preview closes.</summary>
        private void OnDisable()
        {
            EditorApplication.delayCall -= Check;
            plan?.Dispose();
            plan = null;
        }

        /// <summary>Chooses update targets only when they are unambiguous.</summary>
        private void Initialize()
        {
            if (master == null || destination == null)
                return;
            choices = new ObjectImportChoices.Interaction[master.Interactions.Length];
            nodes.Clear();
            links.Clear();
            foreach (ObjectMasterPreset.SharedEntry shared in master.Shared)
                AddNode(shared.Path);
            for (int index = 0; index < choices.Length; index++)
            {
                ObjectMasterPreset.InteractionEntry entry = master.Interactions[index];
                AddNode(entry.Path);
                Type type = entry.Preset?.Configuration != null ? Type.GetType(entry.Preset.Configuration.TypeName, false) : null;
                Transform node = ObjectPresetHierarchy.Find(destination.transform, entry.Path);
                Component[] existing = node != null && type != null ? node.GetComponents(type) : Array.Empty<Component>();
                ObjectInteraction selected = null;
                foreach (Component component in existing)
                    if (ObjectWorkspaceTarget.FileId(component) == entry.SourceId)
                        selected = component as ObjectInteraction;
                if (selected == null && existing.Length == 1 && entry.Index == 0)
                    selected = existing[0] as ObjectInteraction;
                choices[index] = new ObjectImportChoices.Interaction { Target = selected, Add = existing.Length == 0 };
            }
            needsCheck = true;
            EditorApplication.delayCall -= Check;
            EditorApplication.delayCall += Check;
        }

        /// <summary>Adds each hierarchy slot only once.</summary>
        /// <param name="path">Source slot path.</param>
        private void AddNode(string path)
        {
            if (!nodes.Exists(node => node.Path == path))
                nodes.Add(new ObjectImportChoices.Node { Path = path, Target = ObjectPresetHierarchy.Find(destination.transform, path) });
        }

        #endregion

        #region Drawing

        /// <summary>Shows plain import choices, exact update targets and any unresolved links.</summary>
        private void OnGUI()
        {
            if (master == null || destination == null)
            {
                EditorGUILayout.LabelField("Choose a master preset and open the destination prefab.");
                return;
            }
            EditorGUILayout.LabelField(master.name + " → " + destination.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            replace = EditorGUILayout.Toggle(new GUIContent("Replace configuration", "Remove other interactions in the selected slots. Leave off to merge."), replace);
            includeShared = EditorGUILayout.Toggle(new GUIContent("Include shared settings", "Also copy flags and shared item settings."), includeShared);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Object slots", EditorStyles.boldLabel);
            foreach (ObjectImportChoices.Node node in nodes)
                node.Target = (Transform)EditorGUILayout.ObjectField(new GUIContent(string.IsNullOrEmpty(node.Path) ? "Object root" : node.Path, "Choose the object to use for this slot."), node.Target, typeof(Transform), true);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Interactions", EditorStyles.boldLabel);
            for (int index = 0; index < choices.Length; index++)
            {
                ObjectImportChoices.Interaction choice = choices[index];
                ObjectMasterPreset.InteractionEntry entry = master.Interactions[index];
                using EditorGUILayout.VerticalScope row = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                choice.Include = EditorGUILayout.ToggleLeft(new GUIContent(entry.Name, "Import this interaction."), choice.Include);
                using (new EditorGUI.DisabledScope(!choice.Include))
                {
                    choice.Add = EditorGUILayout.Toggle(new GUIContent("Add new", "Add a separate interaction. Leave off to update an existing one."), choice.Add);
                    if (!choice.Add)
                        choice.Target = (ObjectInteraction)EditorGUILayout.ObjectField(new GUIContent("Update", "Choose the existing interaction to update."), choice.Target, typeof(ObjectInteraction), true);
                    EditorGUILayout.LabelField(choice.Add ? "Add " + entry.Name : "Update " + entry.Name, EditorStyles.miniLabel);
                    DrawGroups(choice, entry.Preset);
                    using (new EditorGUI.DisabledScope(!ready || plan?.PreviewInteraction(entry.SourceId) == null))
                        if (GUILayout.Button(new GUIContent("Compare values", "See the values before and after this import.")))
                            ObjectGroupCompareWindow.Open(choice.Add ? null : choice.Target, plan.PreviewInteraction(entry.SourceId));
                }
            }
            if (replace)
            {
                EditorGUILayout.LabelField("Other interactions in these object slots will be removed. Review the selected rows before applying.", EditorStyles.wordWrappedMiniLabel);
                if (plan != null)
                    foreach (string removed in plan.RemovedNames)
                        EditorGUILayout.LabelField("Remove " + removed, EditorStyles.miniLabel);
            }
            if (plan != null && plan.Missing.Count > 0)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Object links", EditorStyles.boldLabel);
                foreach (ObjectPresetLink slot in plan.Missing)
                {
                    ObjectImportChoices.Link replacement = links.Find(link => link.Key == slot.Key);
                    if (replacement == null)
                    {
                        replacement = new ObjectImportChoices.Link { Key = slot.Key };
                        links.Add(replacement);
                    }
                    Type type = Type.GetType(slot.TypeName, false) ?? typeof(UnityEngine.Object);
                    replacement.Target = EditorGUILayout.ObjectField(new GUIContent(string.IsNullOrEmpty(slot.Path) ? slot.Label.Length > 0 ? slot.Label : "Object root" : slot.Path,
                        "Choose the replacement for this link."), replacement.Target, type, true);
                }
            }
            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck())
            {
                ready = false;
                needsCheck = true;
                EditorApplication.delayCall -= Check;
                EditorApplication.delayCall += Check;
            }
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using EditorGUILayout.HorizontalScope actions = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button(new GUIContent("Check configuration", "Check the result without changing the object.")))
            {
                needsCheck = true;
                Check();
            }
            using (new EditorGUI.DisabledScope(!ready || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("Import configuration", "Apply the previewed result to this object.")))
                {
                    ObjectWorkspace state = ObjectWorkspace.instance;
                    if (ObjectWorkspaceDrafts.Count(state) > 0 || ObjectStructureChanges.Count(state) > 0)
                        warning = "Apply or discard this object's drafts before importing a master.";
                    else if (plan.Apply(out warning))
                    {
                        RememberSources(state);
                        ObjectWorkspaceDrafts.Discard(state, false);
                        state.Persist();
                        ObjectsLogicStudioWindow.RefreshOpen();
                        Close();
                    }
                }
        }

        #endregion

        #region Validation

        /// <summary>Remembers imported group sources and clears unchanged cached proposals from before the import.</summary>
        /// <param name="state">Persistent workspace receiving the import provenance.</param>
        private void RememberSources(ObjectWorkspace state)
        {
            state.BrowseTarget.Capture(destination, 0);
            state.Master = master;
            state.SavedDrafts.RemoveAll(draft => draft.Target.PrefabGuid == state.BrowseTarget.PrefabGuid);
            for (int index = 0; index < choices.Length; index++)
            {
                if (!choices[index].Include || plan.PreviewInteraction(master.Interactions[index].SourceId) is not ObjectInteraction feature)
                    continue;
                ObjectWorkspaceTarget route = new ObjectWorkspaceTarget();
                route.Capture(feature.gameObject, 0);
                string key = route.PrefabGuid + ":" + route.ObjectPath + ":" + ObjectWorkspaceTarget.FileId(feature) + "|";
                foreach (ObjectGroupPreset group in master.Interactions[index].Preset.Groups)
                {
                    if (choices[index].ChooseGroups && !choices[index].Groups.Contains(group.Group.Key))
                        continue;
                    string assignment = key + group.Group.Key;
                    state.GroupPresets.RemoveAll(source => source.Key == assignment);
                    state.GroupPresets.Add(new ObjectGroupAssignment { Key = assignment, Preset = group, Baseline = ObjectGroupPresetStore.AssetStamp(group) });
                }
            }
        }

        /// <summary>Lets an import keep local groups while replacing only the selected reusable settings.</summary>
        /// <param name="choice">Interaction import options.</param>
        /// <param name="preset">Main preset supplying its smaller groups.</param>
        private static void DrawGroups(ObjectImportChoices.Interaction choice, ObjectInteractionPreset preset)
        {
            bool previous = choice.ChooseGroups;
            choice.ChooseGroups = EditorGUILayout.Toggle(new GUIContent("Choose groups", "Import only the groups you select below."), choice.ChooseGroups);
            if (!choice.ChooseGroups || preset == null)
                return;
            List<string> selected = choice.Groups;
            if (!previous)
                foreach (ObjectGroupPreset group in preset.Groups)
                    if (group != null && !selected.Contains(group.Group.Key))
                        selected.Add(group.Group.Key);
            using StudioIndentScope indent = new StudioIndentScope();
            foreach (ObjectGroupPreset group in preset.Groups)
            {
                if (group == null)
                    continue;
                bool include = EditorGUILayout.ToggleLeft(new GUIContent(group.Group.Title, "Copy this settings group."), selected.Contains(group.Group.Key));
                if (include && !selected.Contains(group.Group.Key))
                    selected.Add(group.Group.Key);
                else if (!include)
                    selected.Remove(group.Group.Key);
            }
        }

        /// <summary>Refreshes the isolated result only after a deliberate choice changes.</summary>
        private void Check()
        {
            if (!needsCheck || master == null || destination == null)
                return;
            needsCheck = false;
            plan?.Dispose();
            plan = new ObjectMasterImportPlan(master, destination, choices, nodes, links, replace, includeShared);
            ready = plan.Validate(out warning);
            if (ready)
                warning = "Ready to import.";
            Repaint();
        }

        #endregion

        #endregion
    }
}
