using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Defines optional first-person footsteps from achieved movement, with directional lean and smooth variation.</summary>
    [Serializable]
    public struct PlayerHeadTiltSettings
    {
        #region Fields

        [Header("Head Tilt")]
        [Tooltip("Animate the first-person camera while the controller actually moves.")]
        public bool Enabled;
        [Tooltip("Fade out footsteps while jumping or falling. Disable for movement without ground contact.")]
        public bool GroundedOnly;
        [Tooltip("Metres travelled during one complete walking cycle. Shorter strides produce faster steps.")]
        public float StrideLength;
        [Tooltip("Speed in metres per second at which the full motion amplitude is reached.")]
        public float ReferenceSpeed;
        [Tooltip("Vertical camera amplitude in metres. Forward and backward movement use only this axis.")]
        public float Height;
        [Tooltip("Sideways camera amplitude in metres, scaled by the lateral movement direction.")]
        public float SideAmplitude;
        [Tooltip("Camera roll towards lateral movement in degrees; backward movement does not invert it.")]
        public float Lean;
        [Tooltip("Additional roll amplitude in degrees during lateral footsteps.")]
        public float RollAmplitude;
        [Tooltip("Response rate per second when starting, changing direction or returning to the resting pose.")]
        public float Response;
        [Header("Irregularity")]
        [Tooltip("Add smooth, repeatable variation to step timing and amplitude.")]
        public bool Irregular;
        [Tooltip("Fractional variation of vertical and lateral amplitudes, from zero to one.")]
        public float AmplitudeVariation;
        [Tooltip("Fractional variation of cadence, from zero to one.")]
        public float CadenceVariation;
        [Tooltip("Noise sampling distance per walking cycle; larger values change the pattern faster.")]
        public float VariationRate;
        [Tooltip("Seed for a repeatable pattern without changing Unity's shared random state.")]
        public int Seed;

        #endregion

        #region Properties

        /// <summary>Gentle regular footsteps, disabled until explicitly enabled in the Camera preset.</summary>
        public static PlayerHeadTiltSettings Default => new PlayerHeadTiltSettings
        {
            GroundedOnly = true, StrideLength = 1.6f, ReferenceSpeed = 5f, Height = 0.025f,
            SideAmplitude = 0.012f, Lean = 2f, RollAmplitude = 0.5f, Response = 12f,
            AmplitudeVariation = 0.2f, CadenceVariation = 0.15f, VariationRate = 0.7f, Seed = 17
        };

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active options without changing any authored value.</summary>
        /// <param name="warning">Receives the setting that needs correction.</param>
        /// <returns>True when disabled or all active values are usable.</returns>
        public readonly bool TryValidate(out string warning)
        {
            // Hidden options remain editable later without blocking an inactive effect.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (!Positive(StrideLength) || !Positive(ReferenceSpeed) || !Positive(Response)
                || !NonNegative(Height) || !NonNegative(SideAmplitude) || !NonNegative(Lean)
                || Lean > 0f && (!NonNegative(RollAmplitude) || RollAmplitude > Lean || Lean + RollAmplitude > 45f))
                warning = "Head Tilt needs positive stride, reference speed and response; non-negative amplitudes; roll amplitude no greater than lean; total roll at most 45 degrees.";
            else if (Irregular && (!NonNegative(AmplitudeVariation) || AmplitudeVariation > 1f
                || !NonNegative(CadenceVariation) || CadenceVariation >= 1f
                || (AmplitudeVariation > 0f || CadenceVariation > 0f) && !Positive(VariationRate)))
                warning = "Head Tilt variation needs amplitude in [0, 1], cadence in [0, 1) and a positive variation rate.";
            return warning.Length == 0;
        }

        /// <summary>Checks a rate that must advance even at its smallest value.</summary>
        /// <param name="value">Authored rate or distance.</param>
        /// <returns>True for positive finite values.</returns>
        private static bool Positive(float value)
        {
            // Infinity would break the phase or smoothing calculation.
            return float.IsFinite(value) && value > 0f;
        }

        /// <summary>Accepts zero to disable an individual motion axis.</summary>
        /// <param name="value">Authored amplitude.</param>
        /// <returns>True for finite values at least zero.</returns>
        private static bool NonNegative(float value)
        {
            // Zero amplitudes preserve the remaining independently configured axes.
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion

        #endregion
    }
}
