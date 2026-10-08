using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Samples material colour composition at emission boundaries with one small readback per source texture.</summary>
    internal sealed class SurfacePalette
    {
        #region State

        private static readonly Dictionary<Texture, Color[]> textures = new Dictionary<Texture, Color[]>();
        private static readonly Color[] fallback = { Color.white };
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Color> colors = new List<Color>();
        private readonly ObjectItem owner;
        private Renderer[] renderers;
        private static readonly int baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int tintColor = Shader.PropertyToID("_Color");
        private static readonly int baseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int mainTex = Shader.PropertyToID("_MainTex");

        #endregion
        #region Methods
        #region Sampling

        /// <summary>Retains the source item and its owned renderers without creating material instances.</summary>
        /// <param name="item">Object providing food or liquid colours.</param>
        internal SurfacePalette(ObjectItem item)
        {
            owner = item;
            RefreshGeometry();
        }

        /// <summary>Recaches visual branches after assembly adds or removes geometry.</summary>
        internal void RefreshGeometry()
        {
            renderers = owner != null ? owner.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
        }

        /// <summary>Builds a small current palette while preserving material and texture assets.</summary>
        /// <param name="settings">Manual colours or automatic sampling policy.</param>
        internal void Refresh(SurfaceTrailSettings settings)
        {
            colors.Clear();
            if (settings.DetectColors && owner != null)
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !owner.Owns(renderer.transform))
                        continue;
                    renderer.GetSharedMaterials(materials);
                    foreach (Material material in materials)
                    {
                        if (material == null)
                            continue;
                        Color tint = material.HasProperty(baseColor) ? material.GetColor(baseColor)
                            : material.HasProperty(tintColor) ? material.GetColor(tintColor) : Color.white;
                        Texture texture = material.HasProperty(baseMap) ? material.GetTexture(baseMap)
                            : material.HasProperty(mainTex) ? material.GetTexture(mainTex) : null;
                        if (texture == null)
                            colors.Add(tint);
                        else
                            foreach (Color sample in Read(texture))
                                if (sample.a > 0.05f)
                                    colors.Add(sample * tint);
                    }
                }
            if (colors.Count == 0)
                colors.AddRange(settings.Colors);
        }

        /// <summary>Chooses one palette sample for a patch or physical droplet.</summary>
        /// <returns>A sampled colour, or white before the first refresh.</returns>
        internal Color Sample() => colors.Count > 0 ? colors[Random.Range(0, colors.Count)] : Color.white;

        /// <summary>Reads a tiny texture representation once, supporting non-readable imported source textures.</summary>
        /// <param name="texture">Shared material texture.</param>
        /// <returns>Cached colour samples without retaining the temporary GPU resources.</returns>
        private static Color[] Read(Texture texture)
        {
            if (textures.TryGetValue(texture, out Color[] cached))
                return cached;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || texture.dimension != TextureDimension.Tex2D)
                return fallback;
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D sample = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                sample.ReadPixels(new Rect(0, 0, 8, 8), 0, 0, false);
                cached = sample.GetPixels();
                // Linear readback is converted to the same authoring colour space as material tints.
                for (int index = 0; index < cached.Length; index++)
                    cached[index] = cached[index].gamma;
                textures.Add(texture, cached);
                return cached;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                Object.Destroy(sample);
            }
        }

        /// <summary>Drops cached texture samples at Play entry, including sessions without domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            textures.Clear();
        }

        #endregion
        #endregion
    }
}
