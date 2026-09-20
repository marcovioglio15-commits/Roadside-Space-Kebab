using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains camera and target references until the common session Apply confirms them.</summary>
    [Serializable]
    internal sealed class PlayerCameraSceneSession
    {
        #region Serialized Fields

        [Header("Context")]
        [Tooltip("Selected scene player whose camera references can be configured.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Camera rig present when the session opened.")]
        [SerializeField]
        private PlayerCameraRig original;

        [Tooltip("Rig and view values captured to detect outside changes.")]
        [SerializeField]
        private string baseline = string.Empty;

        [Header("Draft")]
        [Tooltip("Camera proposed for this player; empty creates a camera child during Apply.")]
        [SerializeField]
        private Camera view;

        [Tooltip("Optional focus anchor; empty uses the player root and configured offset.")]
        [SerializeField]
        private Transform target;

        [Tooltip("Explicit request to configure or resynchronize the scene camera.")]
        [SerializeField]
        private bool synchronize;

        #endregion

        #region Properties

        /// <summary>The selected player receiving this camera setup.</summary>
        public PlayerHost Host => host;
        /// <summary>Proposed existing camera, or null for creation.</summary>
        public Camera View => view;
        /// <summary>Proposed target, or null for the player root.</summary>
        public Transform Target => target;
        /// <summary>Whether camera setup must join Apply even without preset edits.</summary>
        public bool HasChanges => synchronize || (original != null && (original.View != view || original.Target != target));

        #endregion

        #region Methods

        #region Session

        /// <summary>Refreshes the applied camera only when there is no pending proposal.</summary>
        /// <param name="player">Scene player matching the current master route.</param>
        public void Refresh(PlayerHost player)
        {
            // Outside changes cannot redirect an unfinished camera setup.
            if (HasChanges)
                return;
            host = player;
            Discard();
        }

        /// <summary>Reloads scene references and forgets only unconfirmed camera choices.</summary>
        public void Discard()
        {
            // No camera is created or moved by opening or discarding its controls.
            original = host != null ? host.GetComponent<PlayerCameraRig>() : null;
            view = original != null ? original.View : null;
            target = original != null ? original.Target : null;
            baseline = Capture(original);
            synchronize = false;
        }

        /// <summary>Stages synchronization even when the selected preset already contains the requested values.</summary>
        public void RequestSynchronization()
        {
            // No scene object is created until the shared Apply succeeds.
            synchronize = true;
        }

        /// <summary>Checks that the scene rig has not changed since the proposal began.</summary>
        /// <param name="warning">Receives a missing host or outside-change warning.</param>
        /// <returns>True when the captured camera setup still matches.</returns>
        public bool TryValidate(out string warning)
        {
            // Validate both reference identity and lens/local pose before touching an existing view.
            warning = string.Empty;
            if (host == null || host.MasterPreset == null || !host.gameObject.scene.IsValid())
                warning = "Choose a loaded scene player before applying camera setup.";
            else if (host.GetComponent<PlayerCameraRig>() != original || Capture(original) != baseline)
                warning = "The scene camera changed outside this session. Discard to reload it before applying.";
            return warning.Length == 0;
        }

        /// <summary>Draws reference choices below the Camera preset's conditional settings.</summary>
        /// <param name="owner">Window recorded before changing the proposal.</param>
        /// <returns>True when a scene camera setup was proposed.</returns>
        public bool Draw(UnityEngine.Object owner)
        {
            // Scene references are irrelevant while editing only an asset.
            if (host == null)
                return false;
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            Camera camera = (Camera)EditorGUILayout.ObjectField(new GUIContent("Scene Camera", "Empty creates a camera child during Apply."), view, typeof(Camera), true);
            Transform anchor = (Transform)EditorGUILayout.ObjectField(new GUIContent("Focus Anchor", "Optional head or camera point; empty uses the player root."), target, typeof(Transform), true);
            bool changed = EditorGUI.EndChangeCheck();
            if (!changed)
                return false;
            Undo.RecordObject(owner, "Edit Camera Setup Draft");
            view = camera;
            target = anchor;
            synchronize = true;
            return true;
        }

        /// <summary>Captures only the camera state owned by this authoring operation.</summary>
        /// <param name="rig">Current applied rig or null.</param>
        /// <returns>A stable signature that detects outside changes.</returns>
        private static string Capture(PlayerCameraRig rig)
        {
            // Global identities survive recreation of saved scene objects across Play.
            if (rig == null)
                return string.Empty;
            string result = PlayerStudioSceneReference.Capture(rig) + PlayerStudioSceneReference.Capture(rig.View)
                + PlayerStudioSceneReference.Capture(rig.Target);
            return rig.View != null ? result + rig.View.transform.localPosition.ToString("R")
                + rig.View.transform.localRotation.ToString("R") + rig.View.fieldOfView.ToString("R")
                + rig.View.nearClipPlane.ToString("R") + rig.View.farClipPlane.ToString("R") : result;
        }

        #endregion

        #endregion
    }
}
