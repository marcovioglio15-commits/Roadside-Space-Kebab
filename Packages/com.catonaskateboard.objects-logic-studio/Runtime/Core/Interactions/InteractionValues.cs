using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares finite-value checks without silently repairing interaction configuration.</summary>
    internal static class InteractionValues
    {
        #region Methods

        #region Validation

        /// <summary>Checks a scalar before it reaches a physics operation.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for a finite number.</returns>
        internal static bool Finite(float value)
        {
            // Infinity and NaN cannot describe an authored physical quantity.
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>Checks all axes of an offset, angle or velocity.</summary>
        /// <param name="value">Authored vector.</param>
        /// <returns>True when every component is finite.</returns>
        internal static bool Finite(Vector3 value)
        {
            // Keep all vector checks consistent with scalar checks.
            return Finite(value.x) && Finite(value.y) && Finite(value.z);
        }

        /// <summary>Checks a quantity that cannot be zero or negative.</summary>
        /// <param name="value">Authored distance, speed, mass or duration.</param>
        /// <returns>True for a positive finite number.</returns>
        internal static bool Positive(float value)
        {
            // Check finiteness even though comparisons already reject NaN.
            return Finite(value) && value > 0f;
        }

        #endregion

        #endregion
    }
}
