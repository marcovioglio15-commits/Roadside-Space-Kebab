using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares finite-value checks between visual deformation, surface deposits and liquid emission.</summary>
    internal static class SurfaceValues
    {
        #region Methods
        #region Validation

        /// <summary>Accepts a finite value that may be zero.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for finite non-negative values.</returns>
        internal static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0f;

        /// <summary>Checks a normalized authored control without clamping it.</summary>
        /// <param name="value">Authored fraction.</param>
        /// <returns>True within the inclusive unit interval.</returns>
        internal static bool Unit(float value) => Nonnegative(value) && value <= 1f;

        /// <summary>Checks a finite ordered positive range.</summary>
        /// <param name="value">Minimum and maximum.</param>
        /// <returns>True when both bounds are positive and ordered.</returns>
        internal static bool Range(Vector2 value) => InteractionValues.Positive(value.x) && float.IsFinite(value.y) && value.y >= value.x;

        /// <summary>Checks every component of an authored colour.</summary>
        /// <param name="value">Colour, including opacity.</param>
        /// <returns>True when its channels are finite and opacity is normalized.</returns>
        internal static bool Color(Color value) => InteractionValues.Finite(new Vector3(value.r, value.g, value.b)) && Unit(value.a);

        #endregion
        #endregion
    }
}
