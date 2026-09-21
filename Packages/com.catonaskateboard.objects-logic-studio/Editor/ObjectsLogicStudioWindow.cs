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
        private readonly SingleInteractionView singles = new SingleInteractionView();

        #endregion

        #region Labels

        private static readonly GUIContent addHoverLabel = new GUIContent("+ Add Hover",
            "Add a First Person Hover and its UI to the selected scene object or open prefab workspace.");
        private static readonly GUIContent removeHoverLabel = new GUIContent("Remove",
            "Remove this hover and its unshared UI. Undo restores both.");
        private static readonly GUIContent openPrefabLabel = new GUIContent("Open",
            "Open Prefab Workspace: edit this prefab with native preview and hierarchy navigation.");

        #endregion

        #region Properties

        /// <summary>Selected component from the hierarchy cache, refreshed only when the target changes.</summary>
        private ObjectHover CurrentInteraction => state.Target.InteractionIndex >= 0 && state.Target.InteractionIndex < interactions.Length
            ? interactions[state.Target.InteractionIndex] : null;

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
            if (ObjectWorkspaceSession.Resolve(window.state) != hover)
                ObjectWorkspaceSession.Select(window.state, hover.gameObject,
                    Array.IndexOf(hover.GetComponents<ObjectHover>(), hover), out window.status);
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
            {
                state.Target.Resolve(true);
                state.Observer.RestoreScenes();
                state.Observer.Refresh();
                if (!state.HasChanges && (!state.HasBinding || ObjectWorkspaceSession.Resolve(state) != null))
                    ObjectWorkspaceSession.Discard(state);
            }
            Undo.undoRedoPerformed += HandleUndo;
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.quitting += Persist;
            EditorApplication.playModeStateChanged += HandlePlayMode;
            Selection.selectionChanged += FollowSelection;
            Refresh();
        }

        /// <summary>Saves the proposal and navigation without forcing Apply or Discard when the window closes.</summary>
        private void OnDisable()
        {
            // Closing a workspace preserves pending work exactly as reopening expects.
            Persist();
            Undo.undoRedoPerformed -= HandleUndo;
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.quitting -= Persist;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            Selection.selectionChanged -= FollowSelection;
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
            Refresh();
            state.Persist();
        }

        #endregion

        #region Selection

        /// <summary>Updates cached component names after structural changes instead of searching on each repaint.</summary>
        private void Refresh()
        {
            // A closed prefab stage resolves to its source asset without losing the selected branch.
            if (state == null)
                return;
            currentObject = state.Target.Resolve();
            singles.Refresh(currentObject);
            interactions = currentObject != null ? currentObject.GetComponents<ObjectHover>() : Array.Empty<ObjectHover>();
            names = new GUIContent[interactions.Length];
            for (int index = 0; index < interactions.Length; index++)
                names[index] = new GUIContent((index + 1) + ". " + interactions[index].InteractionName,
                    "Expand this hover to edit its preset, binding and appearance. Apply or Discard before opening another hover.");
            Repaint();
        }

        /// <summary>Follows explicit hierarchy selections only when no proposal would be lost.</summary>
        private void FollowSelection()
        {
            // Merely selecting a font or preset while editing must not redirect an unfinished session.
            if (state == null || state.HasChanges || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            GameObject selected = Selection.activeGameObject;
            if (selected == null || selected == currentObject)
                return;
            if (EditorUtility.IsPersistent(selected))
            {
                if (PrefabUtility.GetPrefabAssetType(selected) != PrefabAssetType.NotAPrefab)
                    state.Prefab = selected.transform.root.gameObject;
            }
            else
            {
                ObjectHover owner = selected.GetComponentInParent<ObjectHover>();
                ObjectWorkspaceSession.Select(state, owner != null ? owner.gameObject : selected, 0, out status);
            }
            state.Persist();
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
            using (new EditorGUI.DisabledScope(locked))
                switch (state.Category)
                {
                    case ObjectInteractionCategory.Hover:
                        DrawHovers();
                        break;
                    case ObjectInteractionCategory.SingleInteraction:
                        singles.Draw(state, data);
                        break;
                    case ObjectInteractionCategory.MultipleInteraction:
                        EditorGUILayout.LabelField("No features available yet.", EditorStyles.centeredGreyMiniLabel);
                        break;
                }
            using (new EditorGUI.DisabledScope(locked))
                if (state.Category != ObjectInteractionCategory.MultipleInteraction
                    && state.Sections.Draw("Scene Observer", "One scene component shares the camera and tagged player with hover and single interactions. Single interactions use that player's PlayerInput."))
                    state.Observer.Draw(state);
            EditorGUILayout.EndScrollView();
            DrawFooter(locked);
        }

        /// <summary>Shows prefab, object and interaction navigation without editing their applied contents.</summary>
        private void DrawSelection()
        {
            // All navigation is guarded while any domain has pending changes.
            if (state.Sections.Draw("Prefab & Object", "Choose a source prefab or an existing scene object."))
                using (new EditorGUI.DisabledScope(state.HasChanges))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        state.Prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "Source prefab opened by the native prefab workspace."), state.Prefab, typeof(GameObject), false);
                        if (state.Prefab != null && GUILayout.Button(openPrefabLabel, EditorStyles.miniButton, GUILayout.Width(44f)))
                            OpenPrefab();
                    }
                    GameObject requested = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Object", "Root or child receiving independently configured interactions."), currentObject, typeof(GameObject), true);
                    if (requested != currentObject)
                    {
                        ObjectWorkspaceSession.Select(state, requested, 0, out status);
                        Refresh();
                    }
                }
        }

        /// <summary>Lists independently configured hover components as expandable feature cards.</summary>
        private void DrawHovers()
        {
            // Structural changes require a writable scene or prefab stage and a completed draft.
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
                DrawBinding();
            if (state.Source != null)
                HoverControls.Draw(data.FindProperty("Draft"), state.Sections);
            if (state.HasBinding && state.Sections.Draw("Debug", "Control selected-object debug geometry."))
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
                    if (GUILayout.Button(new GUIContent("Apply", "Validate and save the preset and prefab binding; scene changes remain ready for scene Save.")))
                    {
                        if (ObjectWorkspaceSession.Apply(state, out status))
                            status = "Applied. Save any modified gameplay scenes.";
                        Refresh();
                    }
                    if (GUILayout.Button(new GUIContent("Discard", "Reload applied data without changing a preset, prefab or scene.")))
                    {
                        ObjectWorkspaceSession.Discard(state);
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
            PrefabStage stage = PrefabStageUtility.OpenPrefab(path);
            ObjectWorkspaceSession.Select(state, stage.prefabContentsRoot, 0, out status);
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
