using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Schedules a shared visible-surface mask and edge-glow composite for active object outlines.</summary>
    public sealed class OutlineRendererFeature : ScriptableRendererFeature
    {
        #region Serialized Fields

        [Header("Edge Glow")]
        [Tooltip("Shared mask/composite material supplied by Objects Logic Studio. Per-object appearance comes from each Outline interaction.")]
        [SerializeField]
        private Material material;

        #endregion

        #region State

        private OutlineRenderPass pass;

        #endregion

        #region Methods

        #region Rendering

        /// <summary>Creates only the reusable pass object when the renderer configuration changes.</summary>
        public override void Create()
        {
            // The authored material is shared; no per-camera material clone is created.
            pass = material != null ? new OutlineRenderPass(material) : null;
        }

        /// <summary>Skips cameras and frames without any registered outline source.</summary>
        /// <param name="renderer">URP renderer receiving this camera's passes.</param>
        /// <param name="renderingData">Current camera and pipeline state.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Preview cameras show neutral guides; actual scene/game cameras use camera culling and LOD selection.
            if (pass != null && OutlineRendererState.ActiveCount > 0 && renderingData.cameraData.cameraType != CameraType.Preview)
                renderer.EnqueuePass(pass);
        }

        #endregion

        #endregion
    }
}
