using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves one motion parameter from a fixed value or an impact-dependent range.</summary>
    [Serializable]
    public struct KnockbackRange
    {
        #region Fields

        [Tooltip("Interpolate from Minimum to Maximum as impact increases.")]
        public bool UseRange;
        [Tooltip("Fixed value used for every accepted hit.")]
        public float Value;
        [Tooltip("Value used at the minimum impact threshold.")]
        public float Minimum;
        [Tooltip("Value used at or above the maximum impact threshold.")]
        public float Maximum;

        #endregion

        #region Methods

        #region Sampling

        /// <summary>Sets the starting range and fixed fallback for a motion parameter.</summary>
        /// <param name="maximum">Value for a full-strength hit and the fixed fallback.</param>
        /// <param name="minimum">Value for the softest accepted hit.</param>
        /// <param name="useRange">Whether new settings interpolate by default.</param>
        internal KnockbackRange(float maximum, float minimum, bool useRange = true)
        {
            UseRange = useRange;
            Value = Maximum = maximum;
            Minimum = minimum;
        }

        /// <summary>Reads the value for a particular collision without modifying the settings.</summary>
        /// <param name="impact">Strength normalized between the configured thresholds.</param>
        /// <returns>Motion parameter for this recoil.</returns>
        internal readonly float Sample(float impact)
        {
            return UseRange ? Mathf.Lerp(Minimum, Maximum, impact) : Value;
        }

        /// <summary>Validates only the active fixed value or range.</summary>
        /// <param name="floor">Smallest valid value in this parameter's units.</param>
        /// <returns>True for a finite fixed value or an ordered range.</returns>
        internal readonly bool Valid(float floor)
        {
            return UseRange ? float.IsFinite(Minimum) && float.IsFinite(Maximum) && Minimum >= floor && Maximum >= Minimum
                : float.IsFinite(Value) && Value >= floor;
        }

        #endregion

        #endregion
    }
}
