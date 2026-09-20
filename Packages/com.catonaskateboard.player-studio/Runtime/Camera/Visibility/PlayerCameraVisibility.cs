using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Temporarily hides configured renderers for one URP camera and restores their prior state afterward.</summary>
    internal sealed class PlayerCameraVisibility : IDisposable
    {
        #region State

        private Camera camera;
        private Renderer[] renderers;
        private bool[] previous;
        private bool applied;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches the assigned visual once and subscribes only when first-person hiding is enabled.</summary>
        /// <param name="view">Only camera for which these renderers are hidden.</param>
        /// <param name="model">Configured model subtree to hide, or null for no model.</param>
        public void Connect(Camera view, Transform model)
        {
            // Other cameras and scene views retain the original renderer state.
            Dispose();
            if (view == null || model == null)
                return;
            camera = view;
            renderers = model.GetComponentsInChildren<Renderer>(true);
            previous = new bool[renderers.Length];
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        /// <summary>Restores pending visibility before releasing subscriptions.</summary>
        public void Dispose()
        {
            // Disable and destruction cannot leave the model hidden after an interrupted render.
            Restore();
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            camera = null;
            renderers = null;
            previous = null;
        }

        #endregion

        #region Rendering

        /// <summary>Hides the cached model only for the configured view.</summary>
        /// <param name="context">URP rendering context.</param>
        /// <param name="view">Camera about to render.</param>
        private void Begin(ScriptableRenderContext context, Camera view)
        {
            // A nested camera first restores the original state rather than inheriting another view's hiding.
            Restore();
            if (view != camera || renderers == null)
                return;
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                {
                    previous[index] = renderers[index].forceRenderingOff;
                    renderers[index].forceRenderingOff = true;
                }
            applied = true;
        }

        /// <summary>Returns every cached renderer to its exact previous flag after the camera finishes.</summary>
        /// <param name="context">URP rendering context.</param>
        /// <param name="view">Camera that finished rendering.</param>
        private void End(ScriptableRenderContext context, Camera view)
        {
            // No component activation or material mutation is used for visibility.
            if (view == camera)
                Restore();
        }

        /// <summary>Restores the cached flags without searching the scene or allocating.</summary>
        private void Restore()
        {
            // This branch is inactive for every camera that never hid the model.
            if (!applied)
                return;
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].forceRenderingOff = previous[index];
            applied = false;
        }

        #endregion

        #endregion
    }
}
