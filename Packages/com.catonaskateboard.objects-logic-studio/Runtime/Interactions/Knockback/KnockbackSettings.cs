using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Configures a damped physical recoil driven by the strength of a player throw.</summary>
    [Serializable]
    public sealed class KnockbackSettings
    {
        #region Fields

        [Header("Incoming Throw")]
        [Tooltip("Maximum game seconds between Throw and collision.")]
        [Range(0.1f, 10f)]
        public float ThrowWindow = 2.5f;
        [Tooltip("Effective impact in newton-seconds below which the body stays still.")]
        [Range(0f, 25f)]
        public float MinimumImpact = 0.1f;
        [Tooltip("Effective impact in newton-seconds that selects the maximum response.")]
        [Range(0.1f, 50f)]
        public float MaximumImpact = 4f;
        [Tooltip("How strongly the size of the thrown object's solid colliders contributes to impact.")]
        [Range(0f, 1f)]
        public float VolumeInfluence = 0.35f;
        [Tooltip("Collider volume in cubic metres with a neutral impact multiplier.")]
        [Range(0.0001f, 1f)]
        public float ReferenceVolume = 0.008f;
        [Tooltip("Limit which thrown objects can trigger the recoil.")]
        public bool FilterThrows;
        [Tooltip("Combine layer, identity and interaction filters on the thrown object.")]
        public GravityFilter Filter = new GravityFilter();

        [Header("Wobble")]
        [Tooltip("Peak horizontal movement in metres.")]
        public KnockbackRange Distance = new KnockbackRange(0.075f, 0.012f);
        [Tooltip("Peak tilt in degrees.")]
        public KnockbackRange Tilt = new KnockbackRange(16f, 2f);
        [Tooltip("Seconds needed to settle back to the resting pose.")]
        public KnockbackRange Duration = new KnockbackRange(0.65f, 0.35f);
        [Tooltip("Oscillations during recovery.")]
        public KnockbackRange Cycles = new KnockbackRange(1.35f, 1.1f);
        [Tooltip("Exponential damping. Higher values settle more of the movement early.")]
        public KnockbackRange Damping = new KnockbackRange(2.1f, 1.5f, false);

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates impact thresholds, motion controls and the receiving body before binding.</summary>
        /// <param name="body">Rigidbody on the receiver.</param>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when the recoil can run.</returns>
        internal bool TryValidate(Rigidbody body, out string warning)
        {
            warning = "Knockback needs a kinematic Rigidbody on the same object.";
            if (body == null || !body.isKinematic)
                return false;
            warning = "Use an increasing impact range, positive throw window and reference volume.";
            if (!InteractionValues.Positive(ThrowWindow) || !float.IsFinite(MinimumImpact) || MinimumImpact < 0f
                || !float.IsFinite(MaximumImpact) || MaximumImpact <= MinimumImpact || VolumeInfluence is not (>= 0f and <= 1f)
                || VolumeInfluence > 0f && !InteractionValues.Positive(ReferenceVolume))
                return false;
            if (FilterThrows && (Filter == null || !Filter.TryValidate(out warning)))
                return false;
            warning = "Set finite wobble values. Duration and cycles must be positive and range minimums must not exceed maximums.";
            if (!Distance.Valid(0f) || !Tilt.Valid(0f) || !Duration.Valid(0.1f) || !Cycles.Valid(0.5f) || !Damping.Valid(0f))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
