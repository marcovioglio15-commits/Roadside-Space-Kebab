using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits presets around Unity's native scene viewport, keeping drafts independent of panel visibility.</summary>
    [EditorToolOwner(typeof(PlayerStudioSceneToolContext))]
    internal sealed class PlayerStudioWindow : SceneView
    {
        #region Serialized State

        [Header("Workspace")]
        [Tooltip("Persistent proposals, source baselines and workspace preferences, independent of the native scene viewport.")]
        [SerializeField]
        private PlayerStudioState state = new PlayerStudioState();

        #endregion

        #region Presentation

        private static readonly GUIContent playerLabel = new GUIContent("Scene Player", "Choose an existing player to open its master and frame it.");
        private static readonly Color draftColor = new Color(1f, 0.72f, 0.2f);
        private static readonly Color appliedColor = new Color(0.2f, 0.85f, 1f, 1f);
        private PlayerStudioWorkspace workspace;
        private PlayerWorkspaceRecovery recovery;
        private PlayerSynchronizationPrompt synchronization;
        private PlayerWorkspaceStore.Snapshot beforePlay;
        private bool shutdownSaved;
        private PlayerVisualPreview visualPreview;
        private PlayerQuickPlayView quickPlayView;
        private PlayerPreviewNavigation navigation;
        private string operationWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>The chosen scene instance whose proposed pose is edited in this workspace.</summary>
        internal PlayerHost PreviewHost => state.PreviewHost;

        /// <summary>All editable domains share the footer, source-switch guard and close prompt.</summary>
        private bool HasPendingChanges => state.HasChanges;

        #endregion

        #region Methods

        #region Window Lifecycle

        /// <summary>Opens the single workspace without replacing its current draft.</summary>
        [MenuItem("Tools/Player Studio")]
        private static void Open()
        {
            // Reuse the window, including its native scene camera.
            PlayerMasterPreset defaults = PlayerDefaultAssets.Ensure();
            PlayerStudioWindow window = GetWindow<PlayerStudioWindow>("Player Studio");
            if (defaults != null && window.state.Selection.Master == null && window.state.Body.Source == null
                && !window.HasPendingChanges && !(window.recovery?.IsBlocked ?? false))
            {
                window.state.Selection.TrySelect(PlayerStudioSourceMode.Master, defaults, null, window.state.Body, out window.operationWarning);
                window.RefreshSession();
            }
            window.synchronization?.Schedule();
        }

        /// <summary>Opens a scene player's Visual tab without abandoning another unfinished proposal.</summary>
        /// <param name="host">Scene player requested by its binding Inspector.</param>
        internal static void OpenVisual(PlayerHost host)
        {
            // The existing selection guard reports pending work instead of replacing it.
            PlayerStudioWindow window = GetWindow<PlayerStudioWindow>("Player Studio");
            window.TryUsePlayer(host);
            window.state.Modules.SetOpen(2, true);
            window.Repaint();
        }

        /// <summary>Initializes Unity's native viewport before restoring our session and subscriptions.</summary>
        public override void OnEnable()
        {
            // SceneView owns rendering, navigation, shortcuts and its Editor-only camera.
            base.OnEnable();
            shutdownSaved = false;
            titleContent = new GUIContent("Player Studio");
            minSize = new Vector2(360f, 400f);
            overlayCanvas.overlaysEnabled = false;
            cameraSettings.easingEnabled = true;
            cameraSettings.accelerationEnabled = false;
            saveChangesMessage = "Apply the pending preset and Transform changes before closing Player Studio?";
            recovery = new PlayerWorkspaceRecovery();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                recovery.Load(this, ref state);
            RefreshSession();
            synchronization = new PlayerSynchronizationPrompt(this, () => state, ApplyDraft, warning =>
            {
                operationWarning = warning;
                UpdateActions();
                Repaint();
            });
            synchronization.Schedule();

            // Events update the draft and view without a custom Update loop.
            EditorApplication.quitting += SaveBeforeShutdown;
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorApplication.projectChanged += RefreshSession;
            SceneView.duringSceneGui += DrawSceneDraft;
            SceneView.duringSceneGui += DrawVisualPreview;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            visualPreview = new PlayerVisualPreview();
            quickPlayView = new PlayerQuickPlayView(this);
        }

        /// <summary>Builds Editor controls around the viewport after Unity prepares the root.</summary>
        private void CreateGUI()
        {
            // UI construction occurs only in the Editor, once per window UI lifetime.
            workspace = new PlayerStudioWorkspace(rootVisualElement, DrawControls, DrawTransformControls, ApplyDraft, DiscardChanges,
                TogglePreview, FramePlayer, EditTransform, ToggleModules, TogglePlacement, LoadDefaults, UseSelectedPlayer,
                CreateDefaultPlayer, ToggleQuickPlay,
                state.ModulesOpen, state.PreviewOpen, () => quickPlayView?.Draw());
            recovery?.RestoreLayout(workspace);
            navigation?.Dispose();
            navigation = new PlayerPreviewNavigation(this, rootVisualElement.Q<UnityEngine.UIElements.IMGUIContainer>("player-scene-viewport"));
            UpdateActions();
        }

        /// <summary>Removes our callbacks before releasing native SceneView resources.</summary>
        public override void OnDisable()
        {
            // Script reload must not discard serialized workspace state.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                SaveWorkspace();
            synchronization?.Dispose();
            EditorApplication.quitting -= SaveBeforeShutdown;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.projectChanged -= RefreshSession;
            SceneView.duringSceneGui -= DrawSceneDraft;
            SceneView.duringSceneGui -= DrawVisualPreview;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            visualPreview?.Dispose();
            quickPlayView?.Dispose();
            navigation?.Dispose();
            quickPlayView = null;
            state.Input.Dispose();
            state.Camera.Dispose();
            visualPreview = null;
            workspace = null;
            base.OnDisable();
        }

        #endregion

        #region Controls

        /// <summary>Draws scrollable fields independently of the fixed action footer.</summary>
        private void DrawControls()
        {
            // A pending draft prevents switching its source or scene context.
            if (recovery != null && recovery.Draw(this, ref state))
                return;
            bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(isPlaying || HasPendingChanges))
            {
                PlayerHost requestedHost = (PlayerHost)EditorGUILayout.ObjectField(playerLabel, state.PreviewHost, typeof(PlayerHost), true);
                if (requestedHost != state.PreviewHost)
                    TryUsePlayer(requestedHost);

                if (PlayerStudioSourceView.Draw(state.Selection, out PlayerStudioSourceMode mode, out PlayerMasterPreset master, out PlayerBodyPreset body)
                    && state.Selection.TrySelect(mode, master, body, state.Body, out operationWarning))
                {
                    RefreshModules();
                    Undo.ClearUndo(this);
                    UpdateActions();
                }
            }

            // Resolve a slot assignment before editing the candidate Body's dimensions.
            if (state.Selection.Mode == PlayerStudioSourceMode.Master && (state.Selection.Master != null || state.Selection.HasChanges(state.Body)))
            {
                using (new EditorGUI.DisabledScope(isPlaying))
                    if (PlayerMasterDraftView.Draw(state.Selection.MasterSession, this))
                        HandleDraftChanged();

                if (!state.Selection.MasterSession.HasChanges)
                    PlayerStudioSourceView.DrawTarget(state.Body);
            }

            // Each module has its own closable tab; draft lifetime is independent of tab visibility.
            if (state.Modules.Draw(state, this,
                isPlaying))
                HandleDraftChanged();
        }

        /// <summary>Keeps pose values visible above the scrolling preset controls while handles are used.</summary>
        private void DrawTransformControls()
        {
            bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            if (state.PlacementOpen)
            {
                // Placement consumes confirmed data, never an unfinished dimension or slot draft.
                using (new EditorGUI.DisabledScope(isPlaying || HasPendingChanges))
                    if (state.Placement.Draw(state.Selection.Master, out PlayerHost createdHost, out string warning))
                    {
                        operationWarning = warning;
                        if (createdHost != null)
                            TryUsePlayer(createdHost);
                    }
            }

            // Closing this panel only changes presentation; pending pose values remain in the session.
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (state.TransformView.Draw(state.Transform, state.Visual, state.VisualScene, this, out string warning))
                    HandleDraftChanged();
                if (warning.Length > 0)
                    operationWarning = warning;
            }
        }

        /// <summary>Refreshes session actions after edits, Undo, selection or mode transitions.</summary>
        private void UpdateActions()
        {
            // Validation is event-driven and stays outside the navigation repaint loop.
            PlayerStudioStatus.Update(state, workspace, recovery?.IsBlocked ?? false,
                operationWarning, visualPreview != null ? visualPreview.Warning : string.Empty);
        }

        #endregion

        #region Scene Preview

        /// <summary>Opens a loaded player's master only when the current draft permits switching source.</summary>
        /// <param name="host">Existing scene player to edit.</param>
        private void TryUsePlayer(PlayerHost host)
        {
            // A new scene context must not abandon a pending pose or preset.
            if (HasPendingChanges)
                operationWarning = "Apply or discard the current session before choosing another player.";
            // A persistent prefab must be opened or placed before it identifies a scene instance.
            else if (host == null || EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid() || host.MasterPreset == null)
                operationWarning = "Choose a scene Player Host with an assigned master.";
            else if (state.Selection.TrySelect(PlayerStudioSourceMode.Master, host.MasterPreset, null, state.Body, out operationWarning))
            {
                // Choosing a context changes only the editing route and preview location.
                state.PreviewHost = host;
                RefreshModules();
                state.Transform.Open(host);
                state.Placement.UseScene(host.gameObject.scene.path);
                Undo.ClearUndo(this);
                FramePlayer();
                synchronization?.Schedule();
            }
            UpdateActions();
        }

        /// <summary>Stages the default slots for the current master without writing its prefab or scene.</summary>
        private void LoadDefaults()
        {
            // Menu actions enforce the same guard even when the module pane is hidden.
            if (PlayerStudioCommands.TryLoadDefaults(state, this, out operationWarning))
            {
                RefreshModules();
                HandleDraftChanged();
            }
            UpdateActions();
        }

        /// <summary>Opens the selected Hierarchy player through the normal draft guard.</summary>
        private void UseSelectedPlayer()
        {
            // No scene selection is changed by opening the context.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                TryUsePlayer(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<PlayerHost>() : null);
        }

        /// <summary>Creates a complete default player through the Player menu after choosing its destination.</summary>
        private void CreateDefaultPlayer()
        {
            // Creating another player must not replace the current unfinished editing context.
            if (EditorApplication.isPlayingOrWillChangePlaymode || HasPendingChanges)
                operationWarning = "Apply or discard the current session in Edit mode before creating another player.";
            else if (PlayerDefaultCreation.Prompt(state.PreviewHost != null ? state.PreviewHost.gameObject.scene
                : UnityEngine.SceneManagement.SceneManager.GetActiveScene(), out PlayerHost host, out operationWarning))
                TryUsePlayer(host);
            UpdateActions();
        }

        /// <summary>Toggles the module pane without changing its open tabs or drafts.</summary>
        private void ToggleModules()
        {
            // Views remains available when both panes are hidden.
            state.ModulesOpen = !state.ModulesOpen;
            workspace.SetModulesVisible(state.ModulesOpen);
        }

        /// <summary>Toggles creation controls above the preview independently of Transform editing.</summary>
        private void TogglePlacement()
        {
            // This toggle is workspace state, not a preset modification.
            state.PlacementOpen = !state.PlacementOpen;
            Repaint();
        }

        /// <summary>Changes only preview visibility, preserving camera navigation and pending edits.</summary>
        private void TogglePreview()
        {
            // Views reopens the same viewport after closing its pane.
            state.PreviewOpen = !state.PreviewOpen;
            workspace.SetPreviewVisible(state.PreviewOpen);
            Repaint();
        }

        /// <summary>Opens numeric Transform fields and draft handles without writing the scene.</summary>
        private void EditTransform()
        {
            // The retained draft is reused even after its panel or preview has been closed.
            if (EditorApplication.isPlayingOrWillChangePlaymode || state.PreviewHost == null)
            {
                operationWarning = "Choose a loaded Scene Player in Edit mode before editing its Transform.";
                UpdateActions();
                return;
            }

            if (!state.Transform.HasChanges)
                state.Transform.Open(state.PreviewHost);
            state.TransformView.Toggle();
            state.PreviewOpen = true;
            workspace.SetPreviewVisible(true);
            Focus();
            Repaint();
        }

        /// <summary>Receives native scene tool events and records only the serialized pose draft.</summary>
        internal void DrawTransformHandles()
        {
            // The context never passes the real root to an editing handle.
            if (!state.PreviewOpen)
                return;

            if (state.TransformView.Pick(state.PreviewHost, state.VisualScene, state.Visual.Draft, state.Transform, visualPreview))
                Repaint();
            if (state.TransformView.DrawHandles(state.Transform, state.Visual, state.VisualScene, this))
                HandleDraftChanged();
        }

        /// <summary>Frames the player using matching draft dimensions or its applied Body.</summary>
        private void FramePlayer()
        {
            // An asset alone cannot identify which scene instance should be framed.
            if (state.PreviewHost == null || state.PreviewHost.MasterPreset == null)
            {
                operationWarning = "Choose a Scene Player with an assigned master to frame.";
                UpdateActions();
                return;
            }

            PlayerBodySettings settings;
            bool hasSettings = MatchesPreviewHost()
                ? PlayerStudioStatus.TryGetPreviewSettings(state, out settings, out operationWarning)
                : state.PreviewHost.MasterPreset.TryGetBodySettings(out settings, out operationWarning);
            if (!hasSettings)
                return;

            // Native framing moves only the Editor camera and also works without a Renderer.
            if (!state.Transform.TryValidateNumbers(out operationWarning))
                return;

            // A bounding sphere also contains a rotated or scaled draft capsule.
            Vector3 worldScale = state.Transform.WorldMatrix.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
            Bounds bounds = new Bounds(state.Transform.WorldMatrix.MultiplyPoint3x4(settings.Center), Vector3.one * settings.Height * extent);
            if (visualPreview != null && visualPreview.TryGetBounds(state.VisualScene, state.Visual.Draft, state.Transform.WorldMatrix, out Bounds visualBounds))
                bounds.Encapsulate(visualBounds);
            Frame(bounds, true);
            Repaint();
        }

        /// <summary>Shows the chosen player's applied or proposed geometry without depending on the current Hierarchy selection.</summary>
        /// <param name="view">Native view currently drawing; only this workspace receives the draft outline.</param>
        private void DrawSceneDraft(SceneView view)
        {
            // Draw only the chosen player in this viewport; unrelated asset routes keep its applied Body.
            if (view != this || !state.PreviewOpen || Event.current.type != EventType.Repaint
                || state.PreviewHost == null || state.PreviewHost.MasterPreset == null)
                return;

            bool hasDraft = state.Transform.HasChanges || (MatchesPreviewHost() && state.Selection.HasChanges(state.Body));
            if (!hasDraft && !state.PreviewHost.DrawBodyGizmo)
                return;

            // A proposal is drawn at its own matrix; no temporary Transform assignment is needed.
            PlayerBodySettings settings;
            bool isValid = MatchesPreviewHost() ? PlayerStudioStatus.TryGetPreviewSettings(state, out settings, out _)
                : state.PreviewHost.MasterPreset.TryGetBodySettings(out settings, out _);
            if (isValid && state.Transform.TryValidateNumbers(out _))
                PlayerBodyWireGizmo.Draw(state.Transform.WorldMatrix, settings, hasDraft ? draftColor : appliedColor);
        }

        /// <summary>Draws cached proposed meshes in the embedded viewport after its real scene.</summary>
        /// <param name="view">Native scene view currently preparing to render.</param>
        private void DrawVisualPreview(SceneView view)
        {
            // The applied model stays visible; only this camera receives the amber proposal.
            if (view == this && state.PreviewOpen && !EditorApplication.isPlayingOrWillChangePlaymode
                && Event.current.type == EventType.Repaint && state.VisualScene.Host != null
                && (state.Visual.HasChanges || state.VisualScene.HasChanges || state.Transform.HasChanges)
                && state.Transform.TryValidateNumbers(out _))
                visualPreview?.Draw(state.VisualScene, state.Visual.Draft, state.Transform.WorldMatrix);
        }

        /// <summary>Checks that the chosen instance belongs to the current asset route.</summary>
        /// <returns>True when candidate geometry can be drawn at this player's position.</returns>
        private bool MatchesPreviewHost()
        {
            // Several instances can share a Body; this reference chooses just the preview location.
            return state.PreviewHost != null && state.PreviewHost.MasterPreset != null && (state.Selection.Mode == PlayerStudioSourceMode.Master
                ? state.PreviewHost.MasterPreset == state.Selection.Master : state.PreviewHost.MasterPreset.BodyPreset == state.Body.Source);
        }

        #endregion

        #region Session Actions

        /// <summary>Tests the current proposal without confirming it, or returns from the running test.</summary>
        private void ToggleQuickPlay()
        {
            if (PlayerQuickPlay.IsActive)
                PlayerQuickPlay.Stop();
            else if (!(recovery?.IsBlocked ?? false))
            {
                SaveWorkspace();
                PlayerQuickPlay.TryStart(state, out operationWarning);
            }
            UpdateActions();
        }

        /// <summary>Updates pending presentation without changing assets or scene components.</summary>
        private void HandleDraftChanged()
        {
            // Draft edits repaint the outline, not the CharacterController.
            operationWarning = string.Empty;
            hasUnsavedChanges = false;
            UpdateActions();
            Repaint();
        }

        /// <summary>Confirms the preset, affected controllers and proposed pose through their shared save path.</summary>
        private void ApplyDraft()
        {
            // The footer button and native close-save prompt use the same guarded operation.
            using PlayerPresetBatch changes = new PlayerPresetBatch();
            if ((recovery?.IsBlocked ?? false) || !changes.TryPrepare(state, out operationWarning)
                || state.Selection.Master != null && !changes.TryValidate(state.Selection.Master, out operationWarning))
            {
                UpdateActions();
                return;
            }

            // All prepared properties join the same validated save boundary.
            if (!PlayerPresetSaveUtility.TrySaveBatch(changes, state.Transform, state.VisualScene, state.CameraScene, out operationWarning))
            {
                UpdateActions();
                return;
            }

            ReloadAppliedState();
            base.SaveChanges();
            UpdateActions();
            Repaint();
        }

        /// <summary>Keeps the window open when native close-save cannot apply the retained draft.</summary>
        public override void SaveChanges()
        {
            // Unity uses an exception to cancel an unsuccessful close-save.
            ApplyDraft();
            if (HasPendingChanges)
                throw new InvalidOperationException(operationWarning);
        }

        /// <summary>Reloads applied presets without changing a scene object.</summary>
        public override void DiscardChanges()
        {
            // Discard reads the live state; it never restores old values over external edits.
            ReloadAppliedState();
            base.DiscardChanges();
            UpdateActions();
            Repaint();
        }

        /// <summary>Reopens confirmed values after a successful Apply or an explicit Discard.</summary>
        private void ReloadAppliedState()
        {
            // All module and scene sessions leave the shared confirmation boundary together.
            state.Selection.Discard(state.Body, out operationWarning);
            state.Locomotion.Discard();
            state.Visual.Discard();
            state.VisualScene.Discard();
            state.Input.Discard();
            state.Camera.Discard();
            state.CameraScene.Discard();
            RefreshModules();
            state.Transform.Discard();
            visualPreview?.Invalidate();
            Undo.ClearUndo(this);
        }

        #endregion

        #region Editor Events

        /// <summary>Retains unconfirmed proposals on disk without applying them to assets or scene objects.</summary>
        private void SaveWorkspace()
        {
            // Closing the window keeps drafts; Apply and Discard remain explicit footer actions.
            if (!shutdownSaved)
                recovery?.Save(state, this, workspace);
        }


        /// <summary>Saves while scene identities are still available, before Editor shutdown destroys them.</summary>
        private void SaveBeforeShutdown()
        {
            // OnDisable must not replace this snapshot after Unity releases scene objects.
            SaveWorkspace();
            shutdownSaved = true;
        }

        /// <summary>Refreshes clean sessions while retaining pending drafts and their target.</summary>
        private void RefreshSession()
        {
            // Play keeps the entire editing context.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !(recovery?.IsBlocked ?? false))
            {
                PlayerBodyPreset previousSource = state.Body.Source;
                state.Selection.Refresh(state.Body, out operationWarning);
                RefreshModules();
                if (previousSource != state.Body.Source && !state.HasChanges)
                    Undo.ClearUndo(this);

                if (!state.Transform.HasChanges)
                    state.Transform.Open(state.PreviewHost);
            }

            visualPreview?.Invalidate();
            hasUnsavedChanges = false;
            UpdateActions();
            Repaint();
        }

        /// <summary>Refreshes clean module sources while preserving any pending scene baseline.</summary>
        private void RefreshModules()
        {
            // Asset and scene proposals share a context but keep separate ownership and conflicts.
            state.Locomotion.Refresh(state.Selection.Master);
            state.Visual.Refresh(state.Selection.Master);
            state.Input.Refresh(state.Selection.Master != null ? state.Selection.Master.InputPreset : null);
            state.Camera.Refresh(state.Selection.Master != null ? state.Selection.Master.CameraPreset : null);
            if (!state.Camera.HasChanges && !state.Selection.MasterSession.HasChanges)
                state.CameraScene.Refresh(state.Selection.Mode == PlayerStudioSourceMode.Master && state.PreviewHost != null
                    && state.PreviewHost.MasterPreset == state.Selection.Master ? state.PreviewHost : null);
            if (!state.Visual.HasChanges && !state.Selection.MasterSession.HasChanges)
                state.VisualScene.Refresh(state.Selection.Mode == PlayerStudioSourceMode.Master && state.PreviewHost != null
                    && state.PreviewHost.MasterPreset == state.Selection.Master ? state.PreviewHost : null);
        }

        /// <summary>Invalidates cached geometry after outside hierarchy edits without accepting a dirty baseline.</summary>
        private void HandleHierarchyChanged()
        {
            // The ordinary refresh leaves pending asset and scene proposals untouched.
            RefreshSession();
        }

        /// <summary>Refreshes after Unity restores preset and controller snapshots together.</summary>
        private void HandleUndoRedo()
        {
            // This also covers draft-only Undo, which leaves scene components untouched.
            RefreshSession();
            SceneView.RepaintAll();
        }

        /// <summary>Updates suspended controls after a Play-mode transition.</summary>
        /// <param name="transition">Unity transition; the refresh guard decides when to reload a source.</param>
        private void HandlePlayModeChanged(PlayModeStateChange transition)
        {
            // A transition never confirms pending drafts or keeps a preview material alive in Play.
            if (transition == PlayModeStateChange.ExitingEditMode)
            {
                SaveWorkspace();
                beforePlay = PlayerWorkspaceStore.Capture(state, this);
                state.VisualScene.CaptureSceneReferences();
                state.Transform.CaptureSceneReferences();
                visualPreview?.Dispose();
            }
            else if (transition == PlayModeStateChange.EnteredEditMode)
            {
                if (beforePlay != null)
                    state = PlayerWorkspaceStore.Restore(beforePlay, out operationWarning);
                else
                    recovery?.Load(this, ref state);
                state.VisualScene.RestoreSceneReferences();
                state.Transform.RestoreSceneReferences();
                state.PreviewHost = state.Transform.Source;
            }
            if (transition == PlayModeStateChange.ExitingPlayMode)
            {
                quickPlayView?.Dispose();
                quickPlayView = null;
            }
            if (transition == PlayModeStateChange.EnteredEditMode)
                quickPlayView = new PlayerQuickPlayView(this);
            if (transition == PlayModeStateChange.EnteredPlayMode && PlayerQuickPlay.IsActive)
            {
                state.PreviewOpen = true;
                workspace?.SetPreviewVisible(true);
                Focus();
            }
            RefreshSession();
        }

        #endregion

        #endregion
    }
}
