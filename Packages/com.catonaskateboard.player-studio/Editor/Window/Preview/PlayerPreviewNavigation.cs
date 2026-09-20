using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Keeps native SceneView navigation repainting while the pointer is over its viewport.</summary>
    internal sealed class PlayerPreviewNavigation : IDisposable
    {
        #region State

        private readonly SceneView window;
        private readonly VisualElement viewport;
        private bool hovering;
        private double nextFrame;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Attaches a bounded repaint clock without replacing Unity's navigation controls.</summary>
        /// <param name="window">Owning native SceneView.</param>
        /// <param name="viewport">Actual input surface inside the split workspace.</param>
        internal PlayerPreviewNavigation(SceneView window, VisualElement viewport)
        {
            // Native navigation remains responsible for orbit, pan, fly and their input deltas.
            this.window = window;
            this.viewport = viewport;
            viewport.RegisterCallback<MouseEnterEvent>(Enter);
            viewport.RegisterCallback<MouseLeaveEvent>(Leave);
            EditorApplication.update += Update;
        }

        /// <summary>Detaches the clock and pointer callbacks when the window closes or rebuilds.</summary>
        public void Dispose()
        {
            // No scheduled work may keep a discarded SceneView alive.
            EditorApplication.update -= Update;
            viewport.UnregisterCallback<MouseEnterEvent>(Enter);
            viewport.UnregisterCallback<MouseLeaveEvent>(Leave);
        }

        #endregion

        #region Navigation

        /// <summary>Starts continuous presentation when native navigation becomes reachable.</summary>
        /// <param name="eventData">Pointer entry notification from the viewport.</param>
        private void Enter(MouseEnterEvent eventData)
        {
            // The sidebar must not request native camera repaints by itself.
            hovering = true;
        }

        /// <summary>Releases continuous presentation when the pointer leaves the native viewport.</summary>
        /// <param name="eventData">Pointer exit notification from the viewport.</param>
        private void Leave(MouseLeaveEvent eventData)
        {
            // Unity still repaints its own camera easing after pointer exit.
            hovering = false;
        }

        /// <summary>Requests at most sixty frames per second only during active edit-mode navigation.</summary>
        private void Update()
        {
            // No asset serialization, validation or geometry rebuild belongs on this clock.
            if (!hovering || EditorApplication.isPlayingOrWillChangePlaymode || EditorWindow.focusedWindow != window
                || EditorApplication.timeSinceStartup < nextFrame)
                return;
            nextFrame = EditorApplication.timeSinceStartup + 1d / 60d;
            window.Repaint();
        }

        #endregion

        #endregion
    }
}
