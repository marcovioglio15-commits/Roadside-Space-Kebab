using System;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Filters collision sounds by closing speed and a minimum repeat interval.</summary>
    [Serializable]
    public sealed class ImpactAudioSettings
    {
        #region Fields

        [Header("Collision Audio")]
        [Tooltip("Play collision audio when the normal impact speed exceeds the threshold.")]
        public bool Enabled = true;
        [Tooltip("Minimum closing speed in metres per second, measured along a contact normal.")]
        public float MinimumSpeed = 1.5f;
        [Tooltip("Minimum scaled seconds between impact sounds from this object.")]
        public float Cooldown = 0.15f;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks enabled thresholds without changing authored values.</summary>
        /// <returns>True when disabled or finite nonnegative thresholds are configured.</returns>
        public bool IsValid()
        {
            // Negative or nonfinite thresholds remain visible for correction.
            return !Enabled || float.IsFinite(MinimumSpeed) && MinimumSpeed >= 0f
                && float.IsFinite(Cooldown) && Cooldown >= 0f;
        }

        #endregion
        #endregion
    }
}
