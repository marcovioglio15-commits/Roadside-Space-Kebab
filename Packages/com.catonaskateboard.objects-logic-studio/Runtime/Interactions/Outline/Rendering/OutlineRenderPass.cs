using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Masks visible geometry once and adds glow only along its silhouette.</summary>
    internal sealed class OutlineRenderPass : ScriptableRenderPass
    {
        #region Pass Data

        /// <summary>Retains the renderer list needed by the mask pass.</summary>
        private sealed class MaskData
        {
            internal RendererListHandle Renderers;
        }

        /// <summary>Retains the mask and shared material for one composite invocation.</summary>
        private sealed class CompositeData
        {
            internal TextureHandle Mask;
            internal Material Material;
        }

        #endregion

        #region State

        private static readonly int depthId = Shader.PropertyToID("_ObjectGlowDepth");
        private static readonly List<ShaderTagId> shaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalGBuffer")
        };
        private readonly Material material;
        private readonly ProfilingSampler maskSampler = new ProfilingSampler("Object Glow Surfaces");
        private readonly ProfilingSampler compositeSampler = new ProfilingSampler("Object Edge Glow");

        #endregion

        #region Methods

        #region Setup

        /// <summary>Requests scene depth and composites outlines after bloom has processed the scene.</summary>
        /// <param name="sharedMaterial">Shared two-pass silhouette material.</param>
        internal OutlineRenderPass(Material sharedMaterial)
        {
            // Scene bloom stays active; the later outline composite cannot enter its source image.
            material = sharedMaterial;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        #endregion

        #region Graph

        /// <summary>Registers transient surface masks and the final additive composite in URP's render graph.</summary>
        /// <param name="graph">Current camera render graph.</param>
        /// <param name="frameData">Culling results, camera descriptors and active render targets.</param>
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            // Matching final scene depth preserves source culling and alpha cuts, including transparent cutouts.
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData camera = frameData.Get<UniversalCameraData>();
            UniversalRenderingData rendering = frameData.Get<UniversalRenderingData>();
            UniversalLightData lights = frameData.Get<UniversalLightData>();
            RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Object Glow Silhouette", true);
            descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
            TextureHandle depth = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Object Glow Depth", true);
            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(shaderTags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
            drawing.overrideMaterial = material;
            drawing.overrideMaterialPassIndex = 0;
            FilteringSettings filter = new FilteringSettings(RenderQueueRange.all, camera.camera.cullingMask, OutlineRendererState.RenderingLayer);
            RendererListParams parameters = new RendererListParams(rendering.cullResults, drawing, filter);
            using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass("Object Glow Surfaces", out MaskData data, maskSampler))
            {
                data.Renderers = graph.CreateRendererList(parameters);
                builder.UseRendererList(data.Renderers);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachment(depth, 1, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetGlobalTextureAfterPass(depth, depthId);
                builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
            }
            using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass("Object Edge Glow", out CompositeData data, compositeSampler))
            {
                data.Mask = mask;
                data.Material = material;
                builder.UseTexture(mask, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) => DrawGlow(data, context));
            }
        }

        /// <summary>Renders the culled original surfaces with per-object glow data.</summary>
        /// <param name="data">Renderer list belonging to this camera.</param>
        /// <param name="context">Validated raster commands for the mask targets.</param>
        private static void DrawMask(MaskData data, RasterGraphContext context)
        {
            // Native renderer lists preserve skinning, submeshes and LOD culling.
            context.cmd.DrawRendererList(data.Renderers);
        }

        /// <summary>Adds edge light to existing camera color while preserving its alpha.</summary>
        /// <param name="data">Surface mask and shared composite material.</param>
        /// <param name="context">Validated raster commands for the camera target.</param>
        private static void DrawGlow(CompositeData data, RasterGraphContext context)
        {
            // Additive blending loads existing camera color without sampling and writing the same texture.
            Blitter.BlitTexture(context.cmd, data.Mask, new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
        }

        #endregion

        #endregion
    }
}
