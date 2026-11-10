using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shows a scene object for one occupied order slot and tints its original material colors.</summary>
    internal sealed class OrderSlotObject
    {
        #region State

        private static readonly int baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int color = Shader.PropertyToID("_Color");
        private readonly GameObject target;
        private readonly bool wasActive;
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly MaterialPropertyBlock work = new MaterialPropertyBlock();
        private Color? appliedTint;

        /// <summary>Retains the original overrides for one renderer material slot.</summary>
        private readonly struct Surface
        {
            internal readonly Renderer Renderer;
            internal readonly int Index;
            internal readonly int ColorId;
            internal readonly Color Original;
            internal readonly MaterialPropertyBlock Baseline;

            #region Methods

            /// <summary>Captures a tintable material slot before the board shows a ticket.</summary>
            /// <param name="renderer">Renderer on the assigned scene object.</param>
            /// <param name="index">Index in the renderer's material list.</param>
            /// <param name="material">Shared material providing the default color.</param>
            /// <param name="colorId">Supported color property on that material.</param>
            internal Surface(Renderer renderer, int index, Material material, int colorId)
            {
                Renderer = renderer;
                Index = index;
                ColorId = colorId;
                Baseline = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(Baseline, index);
                // Renderer-wide overrides also apply when this slot has no individual block.
                MaterialPropertyBlock effective = Baseline;
                if (Baseline.isEmpty)
                {
                    effective = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(effective);
                }
                Original = effective.HasColor(colorId) ? effective.GetColor(colorId) : material.GetColor(colorId);
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches only tintable renderers and hides the object until its first ticket.</summary>
        /// <param name="target">Pre-existing scene object assigned to a board slot.</param>
        /// <param name="tint">Whether delivery states change the object's material colors.</param>
        internal OrderSlotObject(GameObject target, bool tint)
        {
            this.target = target;
            wasActive = target.activeSelf;
            if (tint)
                foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        Material material = materials[index];
                        if (material == null)
                            continue;
                        if (material.HasColor(baseColor))
                            surfaces.Add(new Surface(renderer, index, material, baseColor));
                        else if (material.HasColor(color))
                            surfaces.Add(new Surface(renderer, index, material, color));
                    }
                }
            Hide();
        }

        /// <summary>Restores original visibility and material overrides when the board is released.</summary>
        internal void Restore()
        {
            // Restore existing overrides without modifying or creating a material asset.
            foreach (Surface surface in surfaces)
                if (surface.Renderer != null)
                    surface.Renderer.SetPropertyBlock(surface.Baseline.isEmpty ? null : surface.Baseline, surface.Index);
            if (target != null && target.activeSelf != wasActive)
                target.SetActive(wasActive);
        }

        #endregion

        #region Presentation

        /// <summary>Shows the assigned object and applies the current delivery color.</summary>
        /// <param name="tint">Color multiplied by the original material color.</param>
        internal void Show(Color tint)
        {
            if (target == null)
                return;
            ApplyTint(tint);
            if (!target.activeSelf)
                target.SetActive(true);
        }

        /// <summary>Updates a visible post-it without rebuilding its order text or changing its visibility.</summary>
        /// <param name="tint">Current delivery color multiplier.</param>
        internal void ApplyTint(Color tint)
        {
            if (target == null || appliedTint == tint)
                return;
            appliedTint = tint;
            // Start with current overrides so unrelated properties remain present during the ticket.
            foreach (Surface surface in surfaces)
                if (surface.Renderer != null)
                {
                    surface.Renderer.GetPropertyBlock(work, surface.Index);
                    if (work.isEmpty)
                        surface.Renderer.GetPropertyBlock(work);
                    work.SetColor(surface.ColorId, surface.Original * tint);
                    surface.Renderer.SetPropertyBlock(work, surface.Index);
                }
        }

        /// <summary>Hides the assigned object when its text slot becomes vacant.</summary>
        internal void Hide()
        {
            appliedTint = null;
            if (target != null && target.activeSelf)
                target.SetActive(false);
        }

        #endregion

        #endregion
    }
}
