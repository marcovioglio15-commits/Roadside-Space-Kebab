using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Controls an inverted-hull outline independently of the object's surface materials.</summary>
    [Serializable]
    public sealed class OutlineSettings
    {
        #region Fields

        [Header("Outline")]
        [Tooltip("Outline thickness in units of 1/250 metre. Zero hides the outline; world-space thickness is independent of object scale.")]
        public float Thickness = 1f;
        [Tooltip("Outline color and opacity. The object's texture and original materials remain untouched.")]
        public Color Color = Color.black;
        [Tooltip("Display the outline through other geometry. Disable for normal depth-tested silhouettes.")]
        public bool ThroughWalls;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects invalid thickness or color without rewriting saved values.</summary>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when the shader parameters are usable.</returns>
        public bool TryValidate(out string warning)
        {
            // HDR RGB values are allowed; opacity remains a normalized blend factor.
            warning = string.Empty;
            if (!InteractionValues.Finite(Thickness) || Thickness < 0f
                || !InteractionValues.Finite(new Vector3(Color.r, Color.g, Color.b))
                || !InteractionValues.Finite(Color.a) || Color.a < 0f || Color.a > 1f)
                warning = "Use finite non-negative thickness, finite color and opacity between zero and one.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
