using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Combines optional views, a searchable interaction catalog and small independent settings editors.</summary>
    internal sealed class ObjectsLogicStudioWindow : EditorWindow
    {
        #region State

        private ObjectWorkspace state;
        private SerializedObject data;
        private GameObject currentObject;
        private ObjectInteraction[] interactions = Array.Empty<ObjectInteraction>();
        private List<ObjectGroupInfo> groups = new List<ObjectGroupInfo>();
        private string status = string.Empty;
        private bool refreshGroups = true;
        private bool openingPrefab;
        private readonly ObjectPresetLibrary library = new ObjectPresetLibrary();
        private readonly ObjectInteractionList catalog = new ObjectInteractionList();
        private readonly ObjectSettingsTree tree = new ObjectSettingsTree();
        private readonly ObjectMasterView master = new ObjectMasterView();

        #endregion

        #region Properties

        /// <summary>Whether the retained branch belongs to the current writable prefab workspace.</summary>
        private bool CanEdit => state.Target.IsOpen && currentObject != null && !EditorUtility.IsPersistent(currentObject);

        #endregion

        #region Methods

        #region Opening

        /// <summary>Opens the saved workspace without applying its pending settings.</summary>
        [MenuItem("Tools/Objects Logic Studio %o")]
        private static void OpenWindow()
        {
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            window.RestoreEditing();
        }

        /// <summary>Opens a prefab branch while retaining drafts from other selections.</summary>
        /// <param name="target">Prefab asset branch or existing prefab-stage object.</param>
        internal static void Open(GameObject target)
        {
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            if (ObjectWorkspaceDrafts.SelectObject(window.state, target, out window.status))
                window.OpenPrefab();
            window.Refresh();
        }

        /// <summary>Opens the requested hover through the common catalog selection.</summary>
        /// <param name="hover">Existing hover component.</param>
        internal static void Open(ObjectHover hover)
        {
            OpenInteraction(hover);
        }

        /// <summary>Opens the requested single-action component through the common catalog selection.</summary>
        /// <param name="feature">Existing single-action component.</param>
        internal static void Open(ObjectSingleInteraction feature)
        {
            OpenInteraction(feature);
        }

        /// <summary>Opens the exact requested extended interaction, including repeated types.</summary>
        /// <param name="feature">Existing extended interaction.</param>
        internal static void Open(ObjectExtendedInteraction feature)
        {
            OpenInteraction(feature);
        }

        /// <summary>Selects a component without discarding another interaction's proposal.</summary>
        /// <param name="feature">Component to select.</param>
        private static void OpenInteraction(ObjectInteraction feature)
        {
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            if (ObjectWorkspaceDrafts.Select(window.state, feature, out window.status))
            {
                window.state.Navigation.Views |= ObjectStudioNavigation.Panels.Interactions;
                window.OpenPrefab();
                window.Refresh();
            }
        }

        /// <summary>Refreshes already open windows after a group applies its draft.</summary>
        internal static void RefreshOpen()
        {
            foreach (ObjectsLogicStudioWindow window in Resources.FindObjectsOfTypeAll<ObjectsLogicStudioWindow>())
                window.Refresh();
        }

        #endregion

        #region Lifetime

        /// <summary>Restores the workspace and listens for structural changes outside repaint.</summary>
        private void OnEnable()
        {
            state = ObjectWorkspace.instance;
            data = new SerializedObject(state);
            minSize = new Vector2(640f, 420f);
            if (state.Position.width >= minSize.x && state.Position.height >= minSize.y)
                position = state.Position;
            Undo.undoRedoPerformed += HandleUndo;
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.projectChanged += Refresh;
            Selection.selectionChanged += FollowSelection;
            PrefabStage.prefabStageOpened += StageChanged;
            PrefabStage.prefabStageClosing += StageChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.quitting += Persist;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                state.Observer.Refresh();
            Refresh();
            OnFocus();
        }

        /// <summary>Restores editing after window recovery or returning from another prefab context.</summary>
        private void OnFocus()
        {
            EditorApplication.delayCall -= RestoreEditing;
            EditorApplication.delayCall += RestoreEditing;
        }

        /// <summary>Saves layout and drafts without applying object settings on close.</summary>
        private void OnDisable()
        {
            Persist();
            Undo.undoRedoPerformed -= HandleUndo;
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.projectChanged -= Refresh;
            Selection.selectionChanged -= FollowSelection;
            PrefabStage.prefabStageOpened -= StageChanged;
            PrefabStage.prefabStageClosing -= StageChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.quitting -= Persist;
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall -= RestoreEditing;
            data?.Dispose();
            library.Dispose();
        }

        /// <summary>Retains both the active proposal and the workspace position.</summary>
        private void Persist()
        {
            if (state == null || EditorApplication.isPlaying)
                return;
            ObjectWorkspaceDrafts.Park(state);
            state.Position = position;
            state.Persist();
        }

        /// <summary>Retains edit proposals at Play boundaries and refreshes returned references afterward.</summary>
        /// <param name="change">Unity Play transition.</param>
        private void PlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Persist();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    state.Observer.Refresh();
                    Refresh();
                    break;
            }
        }

        /// <summary>Updates retained drafts after Undo and saves restored prefab contents.</summary>
        private void HandleUndo()
        {
            GameObject target = state.Target.Resolve();
            if (target != null && state.Target.IsOpen)
                ObjectAuthoringSave.Save(target);
            state.Observer.Refresh();
            state.Persist();
            Refresh();
        }

        #endregion

        #region Selection

        /// <summary>Opens the retained prefab without reselecting interactions or replacing pending values.</summary>
        internal void RestoreEditing()
        {
            if (state == null || state.Prefab == null || state.Target.IsOpen || state.Target.IsPreview
                || openingPrefab || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            OpenPrefab();
            Refresh();
        }

        /// <summary>Refreshes component and preset caches only when the editor reports a change.</summary>
        private void Refresh()
        {
            if (state == null || openingPrefab)
                return;
            currentObject = ObjectWorkspaceContext.Target(state);
            interactions = ObjectWorkspaceContext.Interactions(state);
            UnlockInteractionControls.Refresh(state.Target.Resolve());
            AssemblyControls.Refresh(state.Target.Resolve());
            library.Invalidate();
            refreshGroups = true;
            Repaint();
        }

        /// <summary>Waits until Unity finishes replacing a prefab stage before resolving its objects.</summary>
        /// <param name="stage">Stage being opened or closed.</param>
        private void StageChanged(PrefabStage stage)
        {
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
        }

        /// <summary>Follows prefab hierarchy selections while preserving existing interaction drafts.</summary>
        private void FollowSelection()
        {
            if (state == null || openingPrefab || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            GameObject selected = Selection.activeGameObject;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (selected == null || selected == currentObject || stage == null || !stage.IsPartOfPrefabContents(selected))
                return;
            if (ObjectWorkspaceDrafts.SelectObject(state, selected, out status))
                Refresh();
        }

        /// <summary>Opens a writable prefab in Unity's existing preview and hierarchy.</summary>
        private void OpenPrefab()
        {
            string path = AssetDatabase.GetAssetPath(state.Prefab);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsOpenForEdit(state.Prefab))
            {
                status = "Choose a writable prefab. Make a variant for imported models.";
                return;
            }
            openingPrefab = true;
            try
            {
                PrefabStageUtility.OpenPrefab(path);
                Selection.activeGameObject = state.Target.Resolve();
            }
            finally
            {
                openingPrefab = false;
            }
        }

        #endregion

        #region Drawing

        /// <summary>Draws foldable object settings above adjacent catalog, interaction and preset columns.</summary>
        private void OnGUI()
        {
            if (state == null || data == null || openingPrefab)
                return;
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(110f);
            using StudioFieldRenderScope render = new StudioFieldRenderScope(ObjectNumericFields.Draw);
            ObjectNavigationHistory.HandleKeys(state);
            state.Navigation.Draw(state);
            DrawSelection();
            RebuildIndex();
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            using (EditorGUILayout.ScrollViewScope page = new EditorGUILayout.ScrollViewScope(state.Scroll))
            {
                state.Scroll = page.scrollPosition;
                using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(locked);
                if ((state.Navigation.Views & ObjectStudioNavigation.Panels.Object) != 0
                    && ObjectWorkspaceGUI.Foldout(state, "View.Object", "Object", "Settings for the selected object."))
                    using (new StudioIndentScope())
                        DrawObject();
                if ((state.Navigation.Views & (ObjectStudioNavigation.Panels.Interactions | ObjectStudioNavigation.Panels.Presets | ObjectStudioNavigation.Panels.SettingsTree)) != 0)
                {
                    state.Navigation.DrawFilters(state);
                    DrawColumns();
                }
                if ((state.Navigation.Views & ObjectStudioNavigation.Panels.SceneSetup) != 0
                    && ObjectWorkspaceGUI.Foldout(state, "View.SceneSetup", "Scene Setup", "Camera, player and shared displays."))
                    using (new StudioIndentScope())
                        state.Observer.Draw(state);
            }
            DrawFooter(locked);
        }

        /// <summary>Refreshes all column indexes once after their context changes.</summary>
        private void RebuildIndex()
        {
            if (!refreshGroups)
                return;
            groups = ObjectInteractionPanel.Groups(state, data);
            ObjectSettingsSearch.Refresh(state, interactions, groups);
            catalog.Rebuild(state, interactions);
            tree.Rebuild(state, groups);
            refreshGroups = false;
        }

        /// <summary>Keeps the prefab picker available regardless of visible panels.</summary>
        private void DrawSelection()
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            EditorGUI.BeginChangeCheck();
            GameObject requested = (GameObject)StudioFieldGUI.ObjectField(new GUIContent("Prefab", "Choose the object to configure."), state.Prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && ObjectWorkspaceDrafts.SelectObject(state, requested, out status))
            {
                RestoreEditing();
                Refresh();
            }
            using (new EditorGUI.DisabledScope(state.Prefab == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("Open", "Open the prefab workspace."), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    OpenPrefab();
                    Refresh();
                }
        }

        /// <summary>Shows shared object controls with compact master commands.</summary>
        private void DrawObject()
        {
            if (currentObject == null)
            {
                EditorGUILayout.LabelField("Choose a prefab to configure.", EditorStyles.miniLabel);
                return;
            }
            EditorGUILayout.LabelField(currentObject.name, EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                ObjectIdentityControls.Draw(currentObject, ObjectWorkspaceDrafts.Count(state) > 0);
                if (currentObject.GetComponent<ObjectItem>() == null && StudioButton.Draw(new GUIContent("Add shared item state", "Use this object in contact, storage and recipes.")))
                    ExtendedInteractionAuthoring.Prepare(currentObject);
                master.Draw(state, currentObject);
            }
        }

        /// <summary>Keeps catalog and settings beside a third preset column when requested.</summary>
        private void DrawColumns()
        {
            bool showInteraction = (state.Navigation.Views & (ObjectStudioNavigation.Panels.Interactions | ObjectStudioNavigation.Panels.SettingsTree)) != 0;
            bool showPresets = (state.Navigation.Views & ObjectStudioNavigation.Panels.Presets) != 0;
            float height = Mathf.Clamp(position.height * 0.65f, 300f, 1000f);
            float width = Mathf.Max(position.width - 24f, showInteraction ? showPresets ? 900f : 600f : 280f);
            float catalogWidth = Mathf.Clamp(width * 0.24f, 220f, 360f);
            float presetsWidth = showPresets ? Mathf.Clamp(width * 0.28f, 280f, 460f) : 0f;
            using EditorGUILayout.HorizontalScope columns = new EditorGUILayout.HorizontalScope(GUILayout.Width(width));
            if (showInteraction)
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(catalogWidth)))
                    DrawCatalog(height);
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(width - catalogWidth - presetsWidth - (showPresets ? 16f : 8f))))
                    DrawInteraction(height);
            }
            if (showPresets)
            {
                GUILayout.Space(8f);
                using EditorGUILayout.VerticalScope column = showInteraction
                    ? new EditorGUILayout.VerticalScope(GUILayout.Width(presetsWidth))
                    : new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true));
                if (ObjectWorkspaceGUI.Foldout(state, "View.Presets", "Presets", "Presets for the visible interaction types."))
                    using (new StudioIndentScope())
                        using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(state.Navigation.PresetsScroll, GUILayout.Height(height)))
                        {
                            state.Navigation.PresetsScroll = scroll.scrollPosition;
                            ObjectInteraction feature = ObjectWorkspaceDrafts.Selected(state);
                            if (feature != null && ObjectSettingsSearch.Matches(state, feature))
                                ObjectInteractionActions.DrawPreset(state, feature);
                            library.Draw(state, catalog.Visible);
                        }
            }
        }

        /// <summary>Draws the searchable interaction picker with a compact adjacent Add button.</summary>
        /// <param name="height">Available column height.</param>
        private void DrawCatalog(float height)
        {
            if (!ObjectWorkspaceGUI.Foldout(state, "View.Interactions", "Interactions", "Choose an interaction on this object."))
                return;
            using StudioIndentScope sectionContent4 = new StudioIndentScope();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName("ObjectsLogicStudio.Search");
                EditorGUI.BeginChangeCheck();
                state.Navigation.Search = EditorGUILayout.TextField(new GUIContent(string.Empty, "Find an interaction by name or type."), state.Navigation.Search, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    state.Persist();
                    catalog.Rebuild(state, interactions);
                }
                using (new EditorGUI.DisabledScope(!CanEdit))
                    if (GUILayout.Button(new GUIContent("Add", "Add an interaction."), EditorStyles.miniPullDown, GUILayout.Width(48f)))
                        ShowAddMenu();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "ObjectsLogicStudio.Search" && OpenSearchResult())
                GUIUtility.ExitGUI();
            using EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(state.Navigation.ListScroll, GUILayout.Height(height - 24f));
            state.Navigation.ListScroll = scroll.scrollPosition;
            ObjectInteraction chosen = catalog.Draw(state);
            if (chosen != null)
            {
                ObjectWorkspaceDrafts.Select(state, chosen, out status);
                RestoreEditing();
                Refresh();
                GUIUtility.ExitGUI();
            }
            if (catalog.Visible.Count == 0)
                EditorGUILayout.LabelField(currentObject == null ? "Choose an object." : "No interactions match these filters.", EditorStyles.wordWrappedMiniLabel);
            DrawStructure();
        }

        /// <summary>Shows broad settings groups or their optional navigable tree.</summary>
        /// <param name="height">Available column height.</param>
        private void DrawInteraction(float height)
        {
            if (!ObjectWorkspaceGUI.Foldout(state, "View.Interaction", "Interaction", "Settings for the selected interaction."))
                return;
            using StudioIndentScope sectionContent5 = new StudioIndentScope();
            ObjectInteraction feature = ObjectWorkspaceDrafts.Selected(state);
            if (feature == null || !ObjectSettingsSearch.Matches(state, feature))
            {
                EditorGUILayout.LabelField("Select an interaction from the list.", EditorStyles.miniLabel);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(feature.InteractionName + " · " + ObjectInteractionCatalog.Name(feature), EditorStyles.boldLabel);
                if (GUILayout.Button(new GUIContent("Tree", "Show or hide the settings tree."), EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    state.Navigation.Views ^= ObjectStudioNavigation.Panels.SettingsTree;
                    state.Persist();
                }
                using (new EditorGUI.DisabledScope(!CanEdit))
                    if (GUILayout.Button(new GUIContent("Remove", "Remove this interaction after Apply."), EditorStyles.miniButton, GUILayout.Width(60f)))
                        ObjectStructureChanges.Remove(state, feature);
            }
            if ((state.Navigation.Views & ObjectStudioNavigation.Panels.SettingsTree) != 0)
                tree.Draw(state, height - 30f);
            else
                using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(state.Navigation.SettingsScroll, GUILayout.Height(height - 30f)))
                {
                    state.Navigation.SettingsScroll = scroll.scrollPosition;
                    ObjectInteractionPanel.Overview(state, groups, state.Navigation.Search);
                    ObjectInteractionActions.Draw(state, feature);
                }
        }

        /// <summary>Selects the first result, or opens a group when an explicit setting token is used.</summary>
        /// <returns>True when an interaction or settings group was selected.</returns>
        private bool OpenSearchResult()
        {
            foreach (ObjectInteraction feature in catalog.Visible)
            {
                if (!ObjectWorkspaceDrafts.Select(state, feature, out status))
                    continue;
                if (state.Navigation.Search.IndexOf("group:", StringComparison.OrdinalIgnoreCase) < 0 && state.Navigation.Search.IndexOf("setting:", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Event.current.Use();
                    Refresh();
                    return true;
                }
                using SerializedObject source = new SerializedObject(state);
                groups = ObjectInteractionPanel.Groups(state, source);
                ObjectSettingsSearch.Refresh(state, interactions, groups);
                foreach (ObjectGroupInfo group in groups)
                    if (ObjectSettingsSearch.MatchesGroup(group, state.Navigation.Search))
                    {
                        ObjectGroupWindow.Open(state, group);
                        Event.current.Use();
                        Refresh();
                        return true;
                    }
            }
            return false;
        }

        /// <summary>Lists staged structure operations with compact cancellation controls.</summary>
        private void DrawStructure()
        {
            for (int index = 0; index < state.Structure.Count; index++)
            {
                ObjectStructureChange change = state.Structure[index];
                if (change.Target.PrefabGuid != state.Target.PrefabGuid)
                    continue;
                using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField((change.Add ? "Add " : "Remove ") + ObjectInteractionCatalog.Find(ObjectStructureChanges.ResolveType(change.TypeName)).Name + " · after Apply", EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("Cancel", "Cancel this change."), EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    state.Structure.RemoveAt(index);
                    state.Persist();
                    break;
                }
            }
        }

        /// <summary>Offers additions only from checked categories.</summary>
        private void ShowAddMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (ObjectInteractionCatalog.Entry entry in ObjectInteractionCatalog.Entries)
            {
                if ((state.Navigation.Filters & entry.Category) == 0)
                    continue;
                ObjectInteractionCatalog.Entry choice = entry;
                GUIContent label = new GUIContent(ObjectStudioNavigation.CategoryName(entry.Category) + "/" + entry.Name);
                if (entry.Type.IsDefined(typeof(DisallowMultipleComponent), true) && currentObject.GetComponent(entry.Type) != null
                    || state.Structure.Exists(change => change.Add && change.TypeName == entry.Type.FullName && change.Target.ObjectPath == state.BrowseTarget.ObjectPath))
                    menu.AddDisabledItem(label);
                else
                    menu.AddItem(label, false, () => { ObjectStructureChanges.Add(state, currentObject, choice.Type); Repaint(); });
            }
            if (state.Navigation.Filters == ObjectStudioNavigation.Categories.Nothing)
                menu.AddDisabledItem(new GUIContent("Select a category first"));
            menu.ShowAsContext();
        }

        /// <summary>Offers compact object transaction actions without filling the full window width.</summary>
        /// <param name="locked">Whether Play suspends editing.</param>
        private void DrawFooter(bool locked)
        {
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            int count = ObjectWorkspaceDrafts.Count(state) + ObjectStructureChanges.Count(state);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(locked || count == 0 && !state.Observer.HasChanges))
            {
                if (GUILayout.Button(new GUIContent("Apply object", "Save pending changes."), EditorStyles.toolbarButton, GUILayout.Width(95f)))
                {
                    RestoreEditing();
                    if (ObjectWorkspaceDrafts.Apply(state, out status))
                        status = "Object saved. Save the scene if Scene Setup changed.";
                    Refresh();
                }
                if (GUILayout.Button(new GUIContent("Discard object", "Drop pending changes."), EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    ObjectWorkspaceDrafts.Discard(state);
                    status = string.Empty;
                    Refresh();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(count > 0 ? count + " pending changes" : "Up to date", EditorStyles.miniLabel);
        }

        #endregion

        #endregion
    }
}
