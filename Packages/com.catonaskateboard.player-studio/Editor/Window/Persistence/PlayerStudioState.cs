using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Owns serializable workspace data independently of SceneView's native rendering objects.</summary>
    [Serializable]
    internal sealed class PlayerStudioState
    {
        #region Serialized Fields

        [Header("Player Studio Session")]
        [Tooltip("Body dimensions and baseline.")]
        public PlayerBodyEditSession Body = new PlayerBodyEditSession();

        [Tooltip("Master route and slot proposal.")]
        public PlayerStudioSelection Selection = new PlayerStudioSelection();

        [Tooltip("Movement, gravity and jump proposal.")]
        public PlayerLocomotionEditSession Locomotion = new PlayerLocomotionEditSession();

        [Tooltip("Visual source and offset proposal.")]
        public PlayerVisualEditSession Visual = new PlayerVisualEditSession();

        [Tooltip("Selected visual hierarchy proposal and conflict baseline.")]
        public PlayerVisualSceneSession VisualScene = new PlayerVisualSceneSession();

        [Tooltip("Input role references edited in an isolated draft.")]
        public PlayerModuleEditSession Input = new PlayerModuleEditSession();

        [Tooltip("Camera settings edited in an isolated draft.")]
        public PlayerModuleEditSession Camera = new PlayerModuleEditSession();

        [Tooltip("Proposed camera and target references.")]
        public PlayerCameraSceneSession CameraScene = new PlayerCameraSceneSession();

        [Tooltip("Open tabs and active module.")]
        public PlayerStudioModuleTabs Modules = new PlayerStudioModuleTabs();

        [Tooltip("Whether the scene preview pane is visible.")]
        public bool PreviewOpen = true;

        [Tooltip("Whether the module editing pane is visible.")]
        public bool ModulesOpen = true;

        [Tooltip("Whether placement controls are visible above the scene preview.")]
        public bool PlacementOpen;

        [Tooltip("Scene player selected for editing.")]
        public PlayerHost PreviewHost = null;

        [Tooltip("Scene creation preferences.")]
        public PlayerStudioPlacement Placement = new PlayerStudioPlacement();

        [Tooltip("Player pose proposal and outside-edit baseline.")]
        public PlayerTransformEditSession Transform = new PlayerTransformEditSession();

        [Tooltip("Preview transform panel and handle preferences.")]
        public PlayerPreviewTransformEditor TransformView = new PlayerPreviewTransformEditor();

        #endregion

        #region Properties

        /// <summary>All editable domains share selection guards, Apply and Discard.</summary>
        public bool HasChanges => Selection.HasChanges(Body) || Locomotion.HasChanges || Visual.HasChanges
            || VisualScene.HasChanges || Transform.HasChanges || Input.HasChanges || Camera.HasChanges || CameraScene.HasChanges;

        #endregion
    }
}
