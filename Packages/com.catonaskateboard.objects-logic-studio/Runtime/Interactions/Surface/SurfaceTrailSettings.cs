using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the silhouette used by a conforming surface deposit.</summary>
    public enum SurfaceMarkShape { Rounded, Splatter, Streak, Fragments }

    /// <summary>Shares deposit appearance between contact dirt and liquid spray impacts.</summary>
    [Serializable]
    public sealed class SurfaceTrailSettings
    {
        #region Fields

        [Header("Appearance")]
        [Tooltip("Dry fragments at zero, a smooth wet deposit at one; intermediate values combine grain and fluid edges.")]
        [Range(0f, 1f)]
        public float Liquidity = 0.3f;
        [Tooltip("Overall deposit silhouette before per-mark variation.")]
        public SurfaceMarkShape Shape = SurfaceMarkShape.Splatter;
        [Tooltip("Minimum and maximum deposit radius in metres.")]
        public Vector2 Radius = new Vector2(0.025f, 0.07f);
        [Tooltip("Minimum and maximum length multiplier along the surface tangent.")]
        public Vector2 Stretch = new Vector2(1f, 1.7f);
        [Tooltip("Number of independent patches per accepted impact.")]
        public int Patches = 3;
        [Tooltip("Random spread of patch centres around the contact in metres.")]
        public float Scatter = 0.035f;
        [Tooltip("Random variation along deposit edges. Zero produces a smooth silhouette.")]
        [Range(0f, 1f)]
        public float Irregularity = 0.55f;
        [Tooltip("Wet highlight intensity, multiplied by liquidity.")]
        [Range(0f, 1f)]
        public float Gloss = 0.75f;
        [Header("Colour")]
        [Tooltip("Sample the object's current material tints and texture colours. Texture samples are cached, not read every frame.")]
        public bool DetectColors = true;
        [Tooltip("Colour alternatives used when automatic sampling is disabled or no material is available.")]
        public Color[] Colors = { new Color(0.35f, 0.5f, 0.16f, 1f), new Color(0.65f, 0.7f, 0.25f, 1f) };
        [Tooltip("Blend a second sampled colour into each patch instead of using a single flat colour.")]
        [Range(0f, 1f)]
        public float ColorBlend = 0.6f;
        [Tooltip("Overall opacity before the gradual fade.")]
        [Range(0f, 1f)]
        public float Opacity = 0.9f;
        [Header("Surface And Lifetime")]
        [Tooltip("Lifetime in scaled seconds. Marks survive the source object's despawn.")]
        public float Lifetime = 16f;
        [Tooltip("Final portion of the lifetime spent fading smoothly to transparent.")]
        public float FadeDuration = 6f;
        [Tooltip("Maximum projection distance on either side of the contact plane, in metres.")]
        public float ProjectionDepth = 0.08f;
        [Tooltip("Small separation from the receiver surface to prevent depth flicker.")]
        public float SurfaceOffset = 0.001f;
        [Tooltip("Reject surface samples whose normal differs from the contact by more than this angle.")]
        public float MaximumAngle = 70f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled colour and geometry choices before allocating any marks.</summary>
        /// <param name="warning">Receives an invalid range, fade or colour.</param>
        /// <param name="deposits">Whether surface geometry and fade settings participate in this effect.</param>
        /// <returns>True when all deposit settings are usable.</returns>
        public bool TryValidate(out string warning, bool deposits = true)
        {
            warning = "Choose positive ordered mark size ranges, 1-12 patches, finite scatter and normalized appearance controls.";
            if (!SurfaceValues.Unit(Liquidity) || !SurfaceValues.Unit(Gloss) || !SurfaceValues.Unit(Opacity)
                || deposits && (!SurfaceValues.Range(Radius) || !SurfaceValues.Range(Stretch) || Patches < 1 || Patches > 12
                    || !SurfaceValues.Nonnegative(Scatter) || !SurfaceValues.Unit(Irregularity) || !SurfaceValues.Unit(ColorBlend)
                    || Shape is not (SurfaceMarkShape.Rounded or SurfaceMarkShape.Splatter or SurfaceMarkShape.Streak or SurfaceMarkShape.Fragments)))
                return false;
            warning = "Use positive lifetime and fade duration, with Fade Duration no longer than Lifetime; projection distance must be positive.";
            if (deposits && (!InteractionValues.Positive(Lifetime) || !InteractionValues.Positive(FadeDuration) || FadeDuration > Lifetime
                || !InteractionValues.Positive(ProjectionDepth) || !SurfaceValues.Nonnegative(SurfaceOffset)
                || !InteractionValues.Positive(MaximumAngle) || MaximumAngle > 89f))
                return false;
            warning = "Add at least one finite fallback colour with opacity in [0, 1].";
            if (Colors == null || Colors.Length == 0)
                return false;
            foreach (Color color in Colors)
                if (!SurfaceValues.Color(color))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
