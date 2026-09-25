using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Controls the glow on visible silhouettes and geometric creases.</summary>
    [Serializable]
    public sealed class OutlineSettings
    {
        #region Fields

        [Header("Edge Glow")]
        [Tooltip("Glow width in render pixels, from zero to sixteen. Zero hides the effect.")]
        public float Thickness = 3f;
        [Tooltip("Glow tint and opacity. Black emits no light; the original surface material is preserved.")]
        public Color Color = Color.white;
        [Tooltip("Light added at the edge. Values above one can feed the camera's configured Bloom effect.")]
        public float Intensity = 2f;
        [Tooltip("Minimum normal change, in degrees, treated as a geometric crease. Silhouettes remain visible independently.")]
        public float EdgeAngle = 30f;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Reports invalid shader parameters without rewriting authored values.</summary>
        /// <param name="warning">Receives the first invalid quantity.</param>
        /// <returns>True when all enabled glow settings can be rendered.</returns>
        public bool TryValidate(out string warning)
        {
            // The width bound limits shader sampling while keeping invalid input visible for correction.
            warning = string.Empty;
            if (!InteractionValues.Finite(Thickness) || Thickness < 0f || Thickness > 16f
                || !InteractionValues.Finite(Intensity) || Intensity < 0f
                || !InteractionValues.Finite(EdgeAngle) || EdgeAngle <= 0f || EdgeAngle > 180f
                || !InteractionValues.Finite(new Vector3(Color.r, Color.g, Color.b))
                || !InteractionValues.Finite(Color.a) || Color.a < 0f || Color.a > 1f)
                warning = "Use width from 0 to 16 pixels, non-negative intensity, an edge angle above 0 up to 180, and finite color/opacity.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
