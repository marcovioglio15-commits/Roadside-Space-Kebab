using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Controls the glow confined to visible silhouette borders.</summary>
    [Serializable]
    public sealed class OutlineSettings
    {
        #region Fields

        [Header("Edge Glow")]
        [Tooltip("Inner silhouette width in render pixels. Zero hides the effect.")]
        [Range(0f, 16f)]
        public float Thickness = 3f;
        [Tooltip("Glow tint and opacity. Black emits no light; the original surface material is preserved.")]
        public Color Color = Color.white;
        [Tooltip("Edge brightness added after the camera's bloom. Other scene objects keep their normal bloom.")]
        [Range(0f, 10f)]
        public float Intensity = 2f;
        [Tooltip("When this object has Hover, show its outline only while at least one Hover interaction is active.")]
        public bool HoverOnly;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Reports invalid shader parameters without changing saved values.</summary>
        /// <param name="warning">Receives the first invalid quantity.</param>
        /// <returns>True when all enabled glow settings can be rendered.</returns>
        public bool TryValidate(out string warning)
        {
            // The width bound limits shader sampling while keeping invalid input visible for correction.
            warning = string.Empty;
            if (!InteractionValues.Finite(Thickness) || Thickness < 0f || Thickness > 16f
                || !InteractionValues.Finite(Intensity) || Intensity < 0f
                || !InteractionValues.Finite(new Vector3(Color.r, Color.g, Color.b))
                || !InteractionValues.Finite(Color.a) || Color.a < 0f || Color.a > 1f)
                warning = "Use width from 0 to 16 pixels, non-negative intensity, and finite color/opacity.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
