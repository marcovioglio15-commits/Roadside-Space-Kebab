using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Places preset controls beside Unity's native scene viewport and keeps session actions outside scrolling content.</summary>
    internal sealed class PlayerStudioWorkspace
    {
        #region Elements

        private readonly TwoPaneSplitView split;
        private readonly Label warningLabel;
        private readonly Button applyButton;
        private readonly Button discardButton;
        private bool previewVisible;
        private bool modulesVisible;
        private readonly ScrollView controls;
        private readonly ScrollView transformScroll;
        private readonly ToolbarButton quickPlayButton;
        private readonly IMGUIContainer viewport;
        private readonly IMGUIContainer gameViewport;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Builds Editor-only panels once while retaining the SceneView's own rendering and navigation container.</summary>
        /// <param name="root">Window root initialized by SceneView.OnEnable.</param>
        /// <param name="drawControls">Draws the existing IMGUI preset fields inside a scrollable panel.</param>
        /// <param name="drawTransform">Draws pinned numeric pose fields while the scene handles are being used.</param>
        /// <param name="apply">Confirms the active preset draft.</param>
        /// <param name="discard">Abandons only pending preset edits.</param>
        /// <param name="togglePreview">Changes visibility without replacing the draft or camera.</param>
        /// <param name="framePlayer">Frames the explicitly chosen scene player.</param>
        /// <param name="editTransform">Opens the scene player's pose draft and manipulation controls.</param>
        /// <param name="toggleModules">Opens or closes the module pane.</param>
        /// <param name="placement">Toggles scene placement above the preview.</param>
        /// <param name="defaults">Stages default slots for the selected player.</param>
        /// <param name="useSelected">Selects the Hierarchy player without adding a full-width button.</param>
        /// <param name="createDefault">Creates a complete default player at a chosen prefab destination.</param>
        /// <param name="quickPlay">Tests current drafts in the dedicated test scene, or stops the active test.</param>
        /// <param name="showModules">Saved visibility of the module pane.</param>
        /// <param name="showPreview">Saved visibility of the scene pane.</param>
        /// <param name="drawGame">Draws the actual Quick Play camera in a separate input surface.</param>
        public PlayerStudioWorkspace(VisualElement root, Action drawControls, Action drawTransform, Action apply, Action discard,
            Action togglePreview, Action framePlayer, Action editTransform, Action toggleModules, Action placement,
            Action defaults, Action useSelected, Action createDefault, Action quickPlay, bool showModules, bool showPreview, Action drawGame)
        {
            // SceneView creates this native IMGUI viewport before our workspace is built.
            viewport = root.Q<IMGUIContainer>();
            if (viewport == null)
                throw new InvalidOperationException("Unity did not create the native Scene view viewport.");

            viewport.name = "player-scene-viewport";
            viewport.RemoveFromHierarchy();
            previewVisible = showPreview;
            modulesVisible = showModules;

            // The toolbar reopens a closed pane; no blank placeholder tabs are added.
            Toolbar toolbar = new Toolbar();
            ToolbarMenu viewsMenu = new ToolbarMenu { text = "Views", tooltip = "Open or close the scene preview without changing the draft." };
            viewsMenu.menu.AppendAction("Scene Preview", _ => togglePreview(),
                _ => previewVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            viewsMenu.menu.AppendAction("Modules", _ => toggleModules(),
                _ => modulesVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(viewsMenu);
            ToolbarMenu playerMenu = new ToolbarMenu { text = "Player", tooltip = "Select a player or stage default configuration slots." };
            playerMenu.menu.AppendAction("Create Default Player…", _ => createDefault());
            playerMenu.menu.AppendAction("Use Selected Player", _ => useSelected());
            playerMenu.menu.AppendAction("Load Default Configuration", _ => defaults());
            toolbar.Add(playerMenu);
            root.Add(toolbar);

            // The split owns the available height; the footer keeps its own space below it.
            split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            split.style.minHeight = 0f;
            split.viewDataKey = "player-studio-split";
            VisualElement controlsPane = new VisualElement();
            controlsPane.style.minHeight = 0f;
            controlsPane.style.minWidth = 0f;
            IMGUIContainer transformPanel = new IMGUIContainer(drawTransform) { name = "preview-transform-panel" };
            transformPanel.style.flexShrink = 0f;

            controls = new ScrollView(ScrollViewMode.Vertical);
            controls.style.flexGrow = 1f;
            controls.style.minWidth = 0f;
            controls.style.minHeight = 0f;
            controls.viewDataKey = "player-studio-controls";
            controls.Add(new IMGUIContainer(drawControls));
            controlsPane.Add(controls);
            split.Add(controlsPane);

            // Keep native camera input on the viewport; the surrounding UI never forwards synthetic events.
            VisualElement scenePane = new VisualElement();
            scenePane.style.minWidth = 0f;
            scenePane.style.minHeight = 0f;
            Toolbar sceneToolbar = new Toolbar();
            sceneToolbar.style.flexShrink = 0f;
            sceneToolbar.style.flexWrap = Wrap.Wrap;
            sceneToolbar.style.height = StyleKeyword.Auto;
            sceneToolbar.style.minHeight = 20f;
            sceneToolbar.Add(new Label("Scene Preview"));
            quickPlayButton = new ToolbarButton(quickPlay)
            {
                text = "Quick Play",
                tooltip = "Test current drafts in the editable Player Test scene without Apply. Stop returns to this workspace."
            };
            sceneToolbar.Add(quickPlayButton);
            ToolbarButton frameButton = new ToolbarButton(framePlayer)
            {
                text = "Frame Player",
                tooltip = "Frame the proposed player and visual model together, including their offsets."
            };
            frameButton.style.marginLeft = StyleKeyword.Auto;
            sceneToolbar.Add(frameButton);
            sceneToolbar.Add(new ToolbarButton(editTransform) { text = "Edit Transform", tooltip = "Show or hide player and visual pose fields. The scene changes only after Apply." });
            sceneToolbar.Add(new ToolbarButton(placement) { text = "Scene Placement", tooltip = "Show or hide creation settings above the preview." });
            sceneToolbar.Add(new ToolbarButton(togglePreview) { text = "Close", tooltip = "Close this pane while retaining the draft and view." });
            scenePane.Add(sceneToolbar);
            transformScroll = new ScrollView(ScrollViewMode.Vertical);
            transformScroll.style.maxHeight = 270f;
            transformScroll.style.flexShrink = 1f;
            transformScroll.style.minHeight = 0f;
            transformScroll.Add(transformPanel);
            scenePane.Add(transformScroll);
            // Override SceneView's absolute viewport style so it cannot cover this pane's toolbar.
            viewport.style.position = Position.Relative;
            viewport.style.top = StyleKeyword.Auto;
            viewport.style.bottom = StyleKeyword.Auto;
            viewport.style.left = StyleKeyword.Auto;
            viewport.style.right = StyleKeyword.Auto;
            viewport.style.flexGrow = 1f;
            viewport.style.flexBasis = 0f;
            viewport.style.minHeight = 80f;
            scenePane.Add(viewport);
            gameViewport = new IMGUIContainer(drawGame) { name = "player-game-viewport", focusable = true };
            gameViewport.style.flexGrow = 1f;
            gameViewport.style.flexBasis = 0f;
            gameViewport.style.minHeight = 80f;
            gameViewport.style.display = DisplayStyle.None;
            scenePane.Add(gameViewport);
            split.Add(scenePane);
            root.Add(split);

            // Warning text can wrap, but it cannot displace the always-visible action row.
            VisualElement footer = new VisualElement();
            footer.style.flexShrink = 0f;
            footer.style.paddingLeft = 6f;
            footer.style.paddingRight = 6f;
            footer.style.paddingTop = 4f;
            footer.style.paddingBottom = 4f;
            warningLabel = new Label();
            warningLabel.style.whiteSpace = WhiteSpace.Normal;
            warningLabel.style.maxHeight = 54f;
            warningLabel.style.overflow = Overflow.Hidden;
            warningLabel.style.color = new Color(1f, 0.72f, 0.32f);
            footer.Add(warningLabel);
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexShrink = 0f;
            applyButton = CreateAction("Apply", "Confirm the active preset and Transform drafts together, then update affected native controllers.", apply);
            discardButton = CreateAction("Discard", "Discard all pending session edits. Use Unity Undo (Ctrl+Z) to undo only the last edit.", discard);
            actions.Add(applyButton);
            actions.Add(discardButton);
            footer.Add(actions);
            root.Add(footer);

            // Native split persistence handles docking; only the arrangement changes in narrow windows.
            root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            SetPreviewVisible(showPreview);
        }

        /// <summary>Creates one fixed-height session button outside the scrolling controls.</summary>
        /// <param name="text">Visible action name.</param>
        /// <param name="tooltip">Explains the action's write or discard scope.</param>
        /// <param name="clicked">Action invoked by a deliberate click.</param>
        /// <returns>A button with equal width to the other session action.</returns>
        private static Button CreateAction(string text, string tooltip, Action clicked)
        {
            // UI Toolkit lays out both buttons even when no source has been selected yet.
            Button button = new Button(clicked) { text = text, tooltip = tooltip };
            button.style.height = 26f;
            button.style.flexGrow = 1f;
            button.style.flexBasis = 0f;
            return button;
        }

        #endregion

        #region State

        /// <summary>Updates the preview test action only when ordinary window state is refreshed.</summary>
        /// <param name="available">Whether a configuration can currently start a test.</param>
        internal void UpdateQuickPlay(bool available)
        {
            bool running = PlayerQuickPlay.IsActive && EditorApplication.isPlaying;
            quickPlayButton.text = running ? "Stop Test" : "Quick Play";
            quickPlayButton.SetEnabled(running || available && !EditorApplication.isPlayingOrWillChangePlaymode);
            viewport.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            gameViewport.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Copies current splitter and scrolling positions into a durable workspace snapshot.</summary>
        /// <param name="snapshot">Snapshot receiving the current layout.</param>
        internal void Capture(PlayerWorkspaceStore.Snapshot snapshot)
        {
            // Read resolved geometry after layout, not the splitter's initial default.
            if (previewVisible && modulesVisible && split.fixedPane != null)
                snapshot.Split = split.orientation == TwoPaneSplitViewOrientation.Horizontal
                ? split.fixedPane.resolvedStyle.width : split.fixedPane.resolvedStyle.height;
            snapshot.ControlsScroll = controls.scrollOffset;
            snapshot.TransformScroll = transformScroll.scrollOffset;
        }

        /// <summary>Restores scroll and splitter values after the new window finishes its first layout.</summary>
        /// <param name="snapshot">Previously captured layout.</param>
        internal void Restore(PlayerWorkspaceStore.Snapshot snapshot)
        {
            // Delay one UI layout pass so view-data restoration cannot overwrite the saved offsets.
            controls.schedule.Execute(() =>
            {
                if (float.IsFinite(snapshot.Split) && snapshot.Split > 0f)
                {
                    split.fixedPaneInitialDimension = snapshot.Split;
                    if (split.orientation == TwoPaneSplitViewOrientation.Horizontal)
                        split.fixedPane.style.width = snapshot.Split;
                    else
                        split.fixedPane.style.height = snapshot.Split;
                }
                controls.scrollOffset = snapshot.ControlsScroll;
                transformScroll.scrollOffset = snapshot.TransformScroll;
            });
        }


        /// <summary>Refreshes action availability without rebuilding the UI or changing the scene.</summary>
        /// <param name="canApply">Whether current mode, target and draft permit confirmation.</param>
        /// <param name="canDiscard">Whether Edit mode has an open or pending session.</param>
        /// <param name="warning">Only the highest-priority problem; the tooltip retains its complete text.</param>
        public void UpdateActions(bool canApply, bool canDiscard, string warning)
        {
            // A warning changes presentation only; validation remains in the Apply path.
            applyButton.SetEnabled(canApply);
            discardButton.SetEnabled(canDiscard);
            warningLabel.text = warning;
            warningLabel.tooltip = warning;
            warningLabel.style.display = warning.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Closes or reopens the embedded viewport without destroying its native camera or session.</summary>
        /// <param name="visible">Whether the scene pane should receive space and input.</param>
        public void SetPreviewVisible(bool visible)
        {
            // Keep the toolbar and footer available even if both content panes are hidden.
            previewVisible = visible;
            RefreshVisibility();
        }

        /// <summary>Hides module controls without discarding data or altering the scene camera.</summary>
        /// <param name="visible">Whether the module pane receives layout space.</param>
        public void SetModulesVisible(bool visible)
        {
            // Both panes can be reopened through the persistent Views toolbar.
            modulesVisible = visible;
            RefreshVisibility();
        }

        /// <summary>Applies the pair of visibility flags without destroying either pane.</summary>
        private void RefreshVisibility()
        {
            // Uncollapse first to clear the previous splitter state before switching sides.
            split.style.display = previewVisible || modulesVisible ? DisplayStyle.Flex : DisplayStyle.None;
            split.UnCollapse();
            if (!modulesVisible)
                split.CollapseChild(0);
            else if (!previewVisible)
                split.CollapseChild(1);
        }

        /// <summary>Stacks the scene below the controls when a dock cannot fit both panels side by side.</summary>
        /// <param name="change">New workspace geometry provided by UI Toolkit.</param>
        private void HandleGeometryChanged(GeometryChangedEvent change)
        {
            // Avoid rebuilding panels or resetting navigation when the window is resized.
            TwoPaneSplitViewOrientation orientation = change.newRect.width >= 720f
                ? TwoPaneSplitViewOrientation.Horizontal : TwoPaneSplitViewOrientation.Vertical;
            if (split.orientation == orientation)
                return;

            split.fixedPaneInitialDimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? 320f : 220f;
            split.orientation = orientation;
            if (orientation == TwoPaneSplitViewOrientation.Vertical)
                split.fixedPane.style.height = 220f;
            else
                split.fixedPane.style.width = 320f;
            SetPreviewVisible(previewVisible);
        }

        #endregion

        #endregion
    }
}
