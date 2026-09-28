using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Draws original visible surfaces once and adds a soft edge glow without expanded shell geometry.</summary>
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
            internal TextureHandle Normals;
            internal Material Material;
        }

        #endregion

        #region State

        private static readonly int colorsId = Shader.PropertyToID("_ObjectGlowColors");
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

        /// <summary>Requests scene depth and schedules glow before camera post-processing.</summary>
        /// <param name="sharedMaterial">Authored two-pass mask and glow shader.</param>
        internal OutlineRenderPass(Material sharedMaterial)
        {
            // HDR glow can feed an existing Bloom volume, while occlusion follows the actual camera depth.
            material = sharedMaterial;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        #endregion

        #region Graph

        /// <summary>Registers transient surface masks and the final additive composite in URP's render graph.</summary>
        /// <param name="graph">Current camera render graph.</param>
        /// <param name="frameData">Culling results, camera descriptors and active render targets.</param>
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            // The mask uses the camera's depth attachment read-only; hidden edges never enter the effect.
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData camera = frameData.Get<UniversalCameraData>();
            UniversalRenderingData rendering = frameData.Get<UniversalRenderingData>();
            UniversalLightData lights = frameData.Get<UniversalLightData>();
            RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle normals = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Object Glow Normals", true);
            TextureHandle colors = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Object Glow Colors", true);
            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(shaderTags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
            drawing.overrideMaterial = material;
            drawing.overrideMaterialPassIndex = 0;
            FilteringSettings filter = new FilteringSettings(RenderQueueRange.opaque, camera.camera.cullingMask, OutlineRendererState.RenderingLayer);
            RendererListParams parameters = new RendererListParams(rendering.cullResults, drawing, filter);
            using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass("Object Glow Surfaces", out MaskData data, maskSampler))
            {
                data.Renderers = graph.CreateRendererList(parameters);
                builder.UseRendererList(data.Renderers);
                builder.SetRenderAttachment(normals, 0, AccessFlags.Write);
                builder.SetRenderAttachment(colors, 1, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetGlobalTextureAfterPass(colors, colorsId);
                builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => DrawMask(data, context));
            }
            using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass("Object Edge Glow", out CompositeData data, compositeSampler))
            {
                data.Normals = normals;
                data.Material = material;
                builder.UseTexture(normals, AccessFlags.Read);
                builder.UseTexture(colors, AccessFlags.Read);
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
            Blitter.BlitTexture(context.cmd, data.Normals, new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
        }

        #endregion

        #endregion
    }
}
