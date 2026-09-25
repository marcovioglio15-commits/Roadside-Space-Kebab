using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits persistent object-interaction drafts with dropdown sections and one Apply/Discard footer.</summary>
    internal sealed class ObjectsLogicStudioWindow : EditorWindow
    {
        #region State

        private ObjectWorkspace state;
        private SerializedObject data;
        private GameObject currentObject;
        private ObjectHover[] interactions = Array.Empty<ObjectHover>();
        private GUIContent[] names = Array.Empty<GUIContent>();
        private string status = string.Empty;
        private bool openingPrefab;
        private readonly SingleInteractionView singles = new SingleInteractionView();
        private readonly ExtendedInteractionView extended = new ExtendedInteractionView();

        #endregion

        #region Labels

        private static readonly GUIContent addHoverLabel = new GUIContent("+ Add Hover",
            "Add a First Person Hover and its UI to the selected object in the open prefab workspace.");
        private static readonly GUIContent removeHoverLabel = new GUIContent("Remove",
            "Remove this hover and its unshared UI. Undo restores both.");
        private static readonly GUIContent openPrefabLabel = new GUIContent("Open",
            "Open Prefab Workspace: edit this prefab with native preview and hierarchy navigation.");

        #endregion

        #region Properties

        /// <summary>Selected component from the hierarchy cache, refreshed only when the target changes.</summary>
        private ObjectHover CurrentInteraction => state.Target.InteractionIndex >= 0 && state.Target.InteractionIndex < interactions.Length
            ? interactions[state.Target.InteractionIndex] : null;

        /// <summary>Controls belong only to the retained branch inside the matching open prefab.</summary>
        private bool CanEdit => state.Target.IsOpen && currentObject != null && !EditorUtility.IsPersistent(currentObject);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens the previous workspace without replacing its retained draft.</summary>
        [MenuItem("Tools/Objects Logic Studio")]
        private static void OpenWindow()
        {
            // Window lifetime is independent of the saved workspace and its pending values.
            GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
        }

        /// <summary>Opens the component requested by its inspector unless another proposal is pending.</summary>
        /// <param name="hover">Interaction to select.</param>
        internal static void Open(ObjectHover hover)
        {
            // Explicit navigation uses the same unfinished-session guard as the object picker.
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            if (!ObjectAuthoringSave.TryValidate(hover.gameObject, out window.status))
                return;
            if (ObjectWorkspaceSession.Resolve(window.state) != hover
                && !ObjectWorkspaceSession.Select(window.state, hover.gameObject,
                    Array.IndexOf(hover.GetComponents<ObjectHover>(), hover), out window.status))
                return;
            window.state.Category = ObjectInteractionCategory.Hover;
            window.state.InteractionExpanded = true;
            window.Refresh();
        }

        /// <summary>Opens the requested Grab, Drop or Throw without discarding an unfinished proposal.</summary>
        /// <param name="feature">Component requested by its inspector.</param>
        internal static void Open(ObjectSingleInteraction feature)
        {
            // Selecting the same retained card also works while its proposal is pending.
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            if (!ObjectAuthoringSave.TryValidate(feature.gameObject, out window.status))
                return;
            if (window.state.HasChanges && (window.currentObject != feature.gameObject || window.state.Single.Kind != feature.Kind))
            {
                window.status = "Apply or Discard before changing objects or interactions.";
                return;
            }
            if (!window.state.HasChanges)
            {
                ObjectWorkspaceSession.Select(window.state, feature.gameObject, 0, out window.status);
                window.state.Single.Kind = feature.Kind;
                window.state.Single.Read(feature.gameObject);
            }
            window.state.Category = ObjectInteractionCategory.SingleInteraction;
            window.state.Single.Expanded = true;
            window.state.Persist();
            window.Refresh();
        }

        /// <summary>Opens a specific contact or dialogue component without replacing another pending proposal.</summary>
        /// <param name="feature">Prefab component requested by its inspector.</param>
        internal static void Open(ObjectExtendedInteraction feature)
        {
            // Native component IDs distinguish multiple dialogues on the same branch.
            ObjectsLogicStudioWindow window = GetWindow<ObjectsLogicStudioWindow>("Objects Logic Studio");
            if (!ObjectAuthoringSave.TryValidate(feature.gameObject, out window.status))
                return;
            if (window.state.HasChanges && window.state.Extended.Resolve(window.currentObject) != feature)
            {
                window.status = "Apply or Discard before changing objects or interactions.";
                return;
            }
            if (!window.state.HasChanges)
            {
                ObjectWorkspaceSession.Select(window.state, feature.gameObject, 0, out window.status);
                window.state.Extended.Select(feature);
            }
            window.state.Category = feature.Kind switch
            {
                ExtendedInteractionKind.Dialogue or ExtendedInteractionKind.Slice => ObjectInteractionCategory.MultipleInteraction,
                ExtendedInteractionKind.SpawnManagement => ObjectInteractionCategory.SpawnManagement,
                ExtendedInteractionKind.AssemblyStation or ExtendedInteractionKind.AssemblyProduct => ObjectInteractionCategory.ObjectAssemble,
                ExtendedInteractionKind.Unlock => ObjectInteractionCategory.UnlockInteractions,
                _ => ObjectInteractionCategory.PassiveInteraction
            };
            window.state.Extended.Expanded = true;
            window.state.Persist();
            window.Refresh();
        }

        /// <summary>Restores persistent data and reconnects native hierarchy and Undo notifications.</summary>
        private void OnEnable()
        {
            // Native serialization restores preset/font/sprite references without transient JSON IDs.
            state = ObjectWorkspace.instance;
            state.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            data = new SerializedObject(state);
            minSize = new Vector2(360f, 460f);
            if (state.Position.width >= minSize.x && state.Position.height >= minSize.y)
                position = state.Position;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                state.Observer.Refresh();
            Undo.undoRedoPerformed += HandleUndo;
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.projectChanged += Refresh;
            EditorApplication.quitting += Persist;
            EditorApplication.playModeStateChanged += HandlePlayMode;
            Selection.selectionChanged += FollowSelection;
            PrefabStage.prefabStageOpened += HandleStageChanged;
            PrefabStage.prefabStageClosing += HandleStageChanged;
            Refresh();
        }

        /// <summary>Saves the proposal and navigation without forcing Apply or Discard when the window closes.</summary>
        private void OnDisable()
        {
            // Closing a workspace preserves pending work exactly as reopening expects.
            Persist();
            Undo.undoRedoPerformed -= HandleUndo;
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.quitting -= Persist;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            Selection.selectionChanged -= FollowSelection;
            PrefabStage.prefabStageOpened -= HandleStageChanged;
            PrefabStage.prefabStageClosing -= HandleStageChanged;
            EditorApplication.delayCall -= Refresh;
            data?.Dispose();
            data = null;
        }

        /// <summary>Captures layout at close, Play entry and Editor shutdown.</summary>
        private void Persist()
        {
            // Runtime copies must not replace the last Edit-mode proposal.
            if (state == null || EditorApplication.isPlaying)
                return;
            state.Position = position;
            state.Persist();
        }

        /// <summary>Preserves the edit session at Play boundaries and resolves returned scene objects afterward.</summary>
        /// <param name="change">Native Play mode transition.</param>
        private void HandlePlayMode(PlayModeStateChange change)
        {
            // Saving before reload retains incomplete values without applying them to the running game.
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Persist();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Refresh();
                    state.Observer.Refresh();
                    break;
            }
        }

        /// <summary>Updates editor wrappers after Undo restores a proposal or an applied source.</summary>
        private void HandleUndo()
        {
            // Undo changes the serialized singleton, so its saved copy must follow the restored values.
            state.Observer.Refresh();
            GameObject target = state.Target.Resolve();
            if (target != null && ObjectAuthoringSave.TryValidate(target, out _))
                try
                {
                    // Applied prefab Undo must reach disk just like the original Apply operation.
                    ObjectAuthoringSave.Save(target);
                }
                catch (Exception exception)
                {
                    status = "Could not save prefab Undo: " + exception.Message;
                }
            Refresh();
            state.Persist();
        }

        #endregion

        #region Selection

        /// <summary>Updates cached component names after structural changes instead of searching on each repaint.</summary>
        private void Refresh()
        {
            // Hidden source assets retain their draft but never populate controls outside their native stage.
            if (state == null)
                return;
            currentObject = state.Target.IsOpen ? state.Target.Resolve() : null;
            if (currentObject != null && !state.HasChanges && !EditorApplication.isPlayingOrWillChangePlaymode)
                ObjectWorkspaceSession.Discard(state);
            UnlockInteractionControls.Refresh(currentObject);
            singles.Refresh(currentObject);
            extended.Refresh(currentObject);
            interactions = currentObject != null ? currentObject.GetComponents<ObjectHover>() : Array.Empty<ObjectHover>();
            names = new GUIContent[interactions.Length];
            for (int index = 0; index < interactions.Length; index++)
                names[index] = new GUIContent((index + 1) + ". " + interactions[index].InteractionName,
                    "Expand this hover to edit its preset, binding and appearance. Apply or Discard before opening another hover.");
            Repaint();
        }

        /// <summary>Refreshes once native stage navigation has finished replacing its contents.</summary>
        /// <param name="stage">Prefab workspace being opened or closed.</param>
        private void HandleStageChanged(PrefabStage stage)
        {
            // Closing fires before Unity leaves the stage; defer resolution until the new context is active.
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
            Repaint();
        }

        /// <summary>Follows explicit hierarchy selections only when no proposal would be lost.</summary>
        private void FollowSelection()
        {
            // Merely selecting a font or preset while editing must not redirect an unfinished session.
            if (state == null || openingPrefab || state.HasChanges || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            GameObject selected = Selection.activeGameObject;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (state.Category == ObjectInteractionCategory.SceneObserver || selected == null || selected == currentObject || stage == null || !stage.IsPartOfPrefabContents(selected)
                || !ObjectAuthoringSave.TryValidate(selected, out _))
                return;
            ObjectHover owner = selected.GetComponentInParent<ObjectHover>();
            ObjectWorkspaceSession.Select(state, owner != null ? owner.gameObject : selected, 0, out status);
            state.Persist();
            Refresh();
        }

        /// <summary>Changes the prefab only after the previous proposal has been explicitly resolved.</summary>
        /// <param name="requested">Prefab asset to select, or null to clear selection.</param>
        private void SelectPrefab(GameObject requested)
        {
            // Validate before offering to discard anything; rejected scene objects leave the session intact.
            if (requested != null && !ObjectAuthoringSave.TryValidate(requested, out status))
                return;
            if (state.HasChanges)
            {
                // Closed or missing targets still allow recovery without opening or recreating their assets.
                if (state.PrefabChanged && !CanEdit)
                {
                    if (!EditorUtility.DisplayDialog("Change prefab?",
                        "The previous session has pending changes. Discard them to change prefab.",
                        "Discard and Switch", "Cancel"))
                        return;
                    ObjectWorkspaceSession.Discard(state);
                }
                else
                    switch (EditorUtility.DisplayDialogComplex("Change prefab?",
                        "Apply or discard the pending interaction and Scene Observer changes before changing prefab. Cancel keeps the current session.",
                        "Apply and Switch", "Cancel", "Discard and Switch"))
                    {
                        case 0:
                            if (!ObjectWorkspaceSession.Apply(state, out status))
                                return;
                            break;
                        case 2:
                            ObjectWorkspaceSession.Discard(state);
                            break;
                        default:
                            return;
                    }
            }
            // Select commits both the asset field and its durable route together after the guard succeeds.
            if (ObjectWorkspaceSession.Select(state, requested, 0, out status))
                Refresh();
        }

        #endregion

        #region Drawing

        /// <summary>Draws scrollable dropdown menus above a fixed Apply/Discard footer.</summary>
        private void OnGUI()
        {
            // Navigation and pending values are separate; only data changes enable Apply.
            if (state == null || data == null)
                return;
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            state.Scroll = EditorGUILayout.BeginScrollView(state.Scroll);
            using (new EditorGUI.DisabledScope(locked))
                DrawSelection();
            ObjectInteractionTabs.Draw(state);
            if (!CanEdit && state.Category != ObjectInteractionCategory.SceneObserver)
            {
                // A retained draft stays untouched while its prefab is closed or another asset is being previewed.
                DrawClosedWorkspace();
                EditorGUILayout.EndScrollView();
                DrawFooter(locked);
                return;
            }
            using (new EditorGUI.DisabledScope(locked))
                switch (state.Category)
                {
                    case ObjectInteractionCategory.SceneObserver:
                        state.Observer.Draw(state);
                        break;
                    case ObjectInteractionCategory.Hover:
                        DrawHovers();
                        break;
                    case ObjectInteractionCategory.SingleInteraction:
                        singles.Draw(state, data);
                        break;
                    case ObjectInteractionCategory.MultipleInteraction:
                        extended.Draw(state, data, ExtendedInteractionKind.Dialogue);
                        break;
                    case ObjectInteractionCategory.UnlockInteractions:
                        extended.Draw(state, data, ExtendedInteractionKind.Unlock);
                        break;
                    case ObjectInteractionCategory.SpawnManagement:
                        extended.Draw(state, data, ExtendedInteractionKind.SpawnManagement);
                        break;
                    case ObjectInteractionCategory.ObjectAssemble:
                        extended.Draw(state, data, ExtendedInteractionKind.AssemblyStation);
                        break;
                    case ObjectInteractionCategory.PassiveInteraction:
                        extended.Draw(state, data, ExtendedInteractionKind.ModifyByContact);
                        break;
                }
            EditorGUILayout.EndScrollView();
            DrawFooter(locked);
        }

        /// <summary>Keeps hidden drafts recoverable when their prefab closes or their branch is removed.</summary>
        private void DrawClosedWorkspace()
        {
            // The common footer remains available even if an old prefab or selected branch no longer exists.
            EditorGUILayout.LabelField(state.Target.IsOpen ? "The selected prefab object is no longer available."
                : state.PrefabChanged ? "Open the selected prefab to resume pending changes, or Discard to clear the retained draft."
                : "Open a prefab to edit its interactions.", EditorStyles.wordWrappedLabel);
        }

        /// <summary>Shows prefab, object and interaction navigation without editing their applied contents.</summary>
        private void DrawSelection()
        {
            // The picker stays usable; selecting another prefab explicitly resolves any retained proposal.
            if (state.Sections.Draw("Prefab", "Choose a prefab asset; use its workspace hierarchy to select a child."))
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GameObject requested = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "Source prefab edited by this tool. Changing prefab asks how to handle pending changes."), state.Prefab, typeof(GameObject), false);
                        if (requested != state.Prefab)
                            SelectPrefab(requested);
                        if (state.Prefab != null && GUILayout.Button(openPrefabLabel, EditorStyles.miniButton, GUILayout.Width(44f)))
                            OpenPrefab();
                    }
                    if (CanEdit)
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(new GUIContent("Prefab Object", "Selected root or child inside the prefab workspace."), currentObject, typeof(GameObject), true);
                        using (new EditorGUI.DisabledScope(state.HasChanges))
                            if (currentObject.GetComponent<ObjectItem>() == null && GUILayout.Button(new GUIContent("Prepare Contact Item",
                                "Add shared item state so this prefab can be a tagged contact participant and retain consumption receipts.")))
                            {
                                ExtendedInteractionAuthoring.Prepare(currentObject);
                                Refresh();
                            }
                    }
                }
        }

        /// <summary>Lists independently configured hover components as expandable feature cards.</summary>
        private void DrawHovers()
        {
            // Structural changes use the native prefab stage and are saved immediately to its asset.
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(state.HasChanges || currentObject == null || EditorUtility.IsPersistent(currentObject)))
                    if (GUILayout.Button(addHoverLabel, EditorStyles.miniButton, GUILayout.Width(105f)))
                        AddHover();
                GUILayout.FlexibleSpace();
            }

            // Only the expanded component owns the draft; other cards cannot replace pending edits.
            for (int index = 0; index < interactions.Length; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool expanded = index == state.Target.InteractionIndex && state.InteractionExpanded;
                        using (new EditorGUI.DisabledScope(state.HasChanges && index != state.Target.InteractionIndex))
                        {
                            bool requested = EditorGUILayout.Foldout(expanded, names[index], true, EditorStyles.foldoutHeader);
                            if (requested != expanded)
                            {
                                if (index == state.Target.InteractionIndex || ObjectWorkspaceSession.Select(state, currentObject, index, out status))
                                    state.InteractionExpanded = requested;
                                state.Persist();
                            }
                        }
                        using (new EditorGUI.DisabledScope(state.HasChanges || EditorUtility.IsPersistent(currentObject)))
                            if (GUILayout.Button(removeHoverLabel, EditorStyles.miniButton, GUILayout.Width(60f)))
                            {
                                RemoveHover(index);
                                return;
                            }
                    }
                    if (index == state.Target.InteractionIndex && state.InteractionExpanded)
                        using (new EditorGUI.IndentLevelScope())
                            DrawHoverSettings();
                }

            // Presets remain editable without a component; retained drafts also survive missing objects.
            if (interactions.Length == 0 || state.HasBinding && CurrentInteraction == null)
                DrawHoverSettings();
        }

        /// <summary>Draws the complete selected feature while keeping its existing Apply/Discard transaction.</summary>
        private void DrawHoverSettings()
        {
            // A collapsed feature does not update or discard its serialized proposal.
            DrawPreset();
            data.Update();
            if (state.HasBinding && state.Sections.Draw("Binding", "Edit this object's interaction name, enabled state and optional anchor."))
                using (new EditorGUI.IndentLevelScope())
                    DrawBinding();
            if (state.Source != null)
                HoverControls.Draw(data.FindProperty("Draft"), state.Sections);
            if (state.HasBinding && state.Sections.Draw("Debug", "Control selected-object debug geometry."))
                using (new EditorGUI.IndentLevelScope())
                    HoverControls.Field(data.FindProperty("Binding"), "DrawGizmos");
            if (data.ApplyModifiedProperties())
                state.Persist();
        }

        /// <summary>Adds one feature and opens its settings, retaining the previously selected preset.</summary>
        private void AddHover()
        {
            // Authoring also creates the label in Edit mode, before any gameplay starts.
            HoverPreset preset = state.Source;
            ObjectHover added = HoverAuthoring.AddHover(currentObject);
            Refresh();
            if (ObjectWorkspaceSession.Select(state, currentObject, Array.IndexOf(interactions, added), out status))
            {
                if (preset != null)
                    ObjectWorkspaceSession.LoadPreset(state, preset);
                state.InteractionExpanded = true;
                state.Persist();
            }
        }

        /// <summary>Removes one card and keeps the same selected component when it still exists.</summary>
        /// <param name="index">Component represented by the clicked feature card.</param>
        private void RemoveHover(int index)
        {
            // Removing an earlier card shifts the selected index without changing its component.
            int selected = state.Target.InteractionIndex;
            HoverAuthoring.RemoveHover(interactions[index]);
            Refresh();
            selected = selected > index ? selected - 1 : selected == index ? 0 : selected;
            ObjectWorkspaceSession.Select(state, currentObject, selected, out status);
        }

        /// <summary>Provides saved configuration selection with explicit New and Duplicate actions.</summary>
        private void DrawPreset()
        {
            // Asset creation is explicit; assigning the result remains a pending component binding.
            if (!state.Sections.Draw("Preset", "Select the reusable detection and text configuration edited by this session."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            using (new EditorGUI.DisabledScope(state.HasChanges))
                using (new EditorGUILayout.HorizontalScope())
                {
                    HoverPreset selected = (HoverPreset)EditorGUILayout.ObjectField(new GUIContent("Hover Preset", "Apply saves changes to this shared asset and assigns it to the selected interaction."), state.Source, typeof(HoverPreset), false);
                    if (selected != state.Source)
                    {
                        Undo.RecordObject(state, "Select hover preset");
                        ObjectWorkspaceSession.LoadPreset(state, selected);
                        state.Persist();
                    }
                    if (GUILayout.Button(new GUIContent("New", "Create a preset with default detection and style settings."), GUILayout.Width(42f)))
                        CreatePreset(false);
                    using (new EditorGUI.DisabledScope(state.Source == null))
                        if (GUILayout.Button(new GUIContent("Duplicate", "Copy the selected saved configuration to a new asset."), GUILayout.Width(70f)))
                            CreatePreset(true);
                }
        }

        /// <summary>Edits per-object data independently of the preset shared by other objects.</summary>
        private void DrawBinding()
        {
            // Anchor routes remain prefab-local and survive a closed native editing stage.
            SerializedProperty binding = data.FindProperty("Binding");
            HoverControls.Field(binding, "Name");
            HoverControls.Field(binding, "Enabled");
            InteractionTagControls.Draw(binding.FindPropertyRelative("TagChange"), state.Sections);
            InteractionVfxControls.Draw(binding.FindPropertyRelative("VisualEffect"), state.Sections, 0f);
            ObjectHover hover = CurrentInteraction;
            if (hover == null)
                return;
            SerializedProperty path = binding.FindPropertyRelative("AnchorPath");
            Transform anchor = HoverHierarchy.Resolve(hover.transform, path.stringValue);
            Transform selected = (Transform)EditorGUILayout.ObjectField(new GUIContent("Anchor", "Optional transform inside this object's hierarchy; empty uses its root."), anchor, typeof(Transform), true);
            if (selected != anchor)
            {
                if (selected == null || selected.IsChildOf(hover.transform))
                    path.stringValue = HoverHierarchy.Path(hover.transform, selected);
                else
                    status = "Choose an anchor inside the selected object's hierarchy.";
            }
            using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(hover)))
                if (hover.Label == null && GUILayout.Button(new GUIContent("Create Hover UI", "Author missing label objects in the prefab workspace or a loaded scene.")))
                {
                    HoverAuthoring.CreateLabel(hover);
                    ObjectAuthoringSave.Save(hover.gameObject);
                    state.Hierarchy = HoverHierarchy.Signature(hover.transform);
                    state.Persist();
                }
        }

        /// <summary>Provides the common transaction actions outside the scrolling form.</summary>
        /// <param name="locked">Whether Play suspends editing.</param>
        private void DrawFooter(bool locked)
        {
            // Apply is one operation across configuration, binding and scene observer setup.
            if (status.Length > 0)
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(state.HasChanges ? "Pending changes · retained when the tool closes" : "Applied configuration", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(locked || !state.HasChanges))
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(state.PrefabChanged && !CanEdit))
                        if (GUILayout.Button(new GUIContent("Apply", "Validate and save interaction settings directly to the open prefab; observer setup follows scene Save.")))
                        {
                            if (ObjectWorkspaceSession.Apply(state, out status))
                                status = "Applied to prefab. Save the gameplay scene if its Observer setup changed.";
                            Refresh();
                        }
                    if (GUILayout.Button(new GUIContent("Discard", "Reload applied data without changing a preset, prefab or scene.")))
                    {
                        ObjectWorkspaceSession.Discard(state);
                        if (state.Target.IsOpen && state.Target.Resolve() == null)
                            ObjectWorkspaceSession.Select(state, PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot, 0, out status);
                        status = string.Empty;
                        Refresh();
                    }
                }
        }

        #endregion

        #region Assets

        /// <summary>Opens a writable prefab with Unity's native preview and editing controls.</summary>
        private void OpenPrefab()
        {
            // Imported models need a prefab variant before adding components.
            string path = AssetDatabase.GetAssetPath(state.Prefab);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsOpenForEdit(state.Prefab))
            {
                status = "Choose a writable .prefab asset; create a variant for imported models first.";
                return;
            }
            openingPrefab = true;
            try
            {
                // Opening the native stage must not replace a retained child or pending interaction with the root.
                PrefabStage stage = PrefabStageUtility.OpenPrefab(path);
                Selection.activeGameObject = state.Target.Resolve();
                if (Selection.activeGameObject == null && !state.HasChanges)
                    ObjectWorkspaceSession.Select(state, stage.prefabContentsRoot, 0, out status);
            }
            finally
            {
                openingPrefab = false;
            }
            Refresh();
        }

        /// <summary>Creates a preset at the requested path without modifying any existing saved source.</summary>
        /// <param name="duplicate">Copy the selected saved preset instead of using defaults.</param>
        private void CreatePreset(bool duplicate)
        {
            // Cancelling a file chooser leaves the current draft and selection untouched.
            string path = EditorUtility.SaveFilePanelInProject(duplicate ? "Duplicate hover preset" : "New hover preset",
                duplicate ? state.Source.name + " Copy" : "Hover Preset", "asset", "Choose where to save the configuration.");
            if (string.IsNullOrEmpty(path))
                return;
            HoverPreset created = ObjectPresetAssets.Create(path, duplicate ? state.Source : null);
            Undo.RecordObject(state, "Select new hover preset");
            ObjectWorkspaceSession.LoadPreset(state, created);
            state.Persist();
        }

        #endregion

        #endregion
    }
}
