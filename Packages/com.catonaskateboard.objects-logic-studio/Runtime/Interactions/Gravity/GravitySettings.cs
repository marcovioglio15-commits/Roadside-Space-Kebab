using System;
using UnityEngine;
using CatOnASkateboard.PlayerStudio;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses how an active pulse restores its affected bodies.</summary>
    public enum GravityRestoreMode { Timer, Interaction, TimerOrInteraction }

    /// <summary>Configures periodic gravity suspension and per-body physical acceleration.</summary>
    [Serializable]
    public sealed class GravitySettings
    {
        #region Fields

        [Header("Pulse")]
        [Tooltip("Suspend matching dynamic Rigidbody objects during each pulse.")]
        public bool AffectObjects = true;
        [Tooltip("Selection reevaluated at pulse start, object activation and identity changes. Qualifying assembled products leave their table after any active movement transaction finishes.")]
        public GravityFilter Filter = new GravityFilter();
        [Tooltip("Randomize the delay between restoration and the next pulse.")]
        public bool RandomInterval;
        [Tooltip("Scaled seconds between restoration and the next pulse.")]
        public float Interval = 10f;
        [Tooltip("Inclusive minimum and maximum interval in scaled seconds.")]
        public Vector2 IntervalRange = new Vector2(5f, 15f);
        [Tooltip("Restore after a timer, a player request, or whichever occurs first.")]
        public GravityRestoreMode Restore;
        [Tooltip("Scaled seconds without gravity when a restoration timer is enabled.")]
        public float Duration = 5f;
        [Tooltip("Reach, aim and obstruction checks for the restoration Button.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Physical Force")]
        [Tooltip("Apply an impulse or sustained acceleration while gravity is suspended.")]
        public bool Push = true;
        [Tooltip("Interpret force directions in the generator's local orientation instead of world axes.")]
        public bool LocalDirection;
        [Tooltip("Randomize each body's direction independently within the configured vector bounds.")]
        public bool RandomDirection;
        [Tooltip("Force direction, normalized before use.")]
        public Vector3 Direction = Vector3.up;
        [Tooltip("Minimum components of the random direction before normalization.")]
        public Vector3 DirectionMinimum = new Vector3(-1f, 0.2f, -1f);
        [Tooltip("Maximum components of the random direction before normalization.")]
        public Vector3 DirectionMaximum = Vector3.one;
        [Tooltip("Randomize each body's force magnitude.")]
        public bool RandomIntensity;
        [Tooltip("Impulse in newton-seconds for zero force duration; otherwise acceleration in metres per second squared.")]
        public float Intensity = 1f;
        [Tooltip("Minimum and maximum impulse or acceleration magnitude.")]
        public Vector2 IntensityRange = new Vector2(0.5f, 2f);
        [Tooltip("Randomize how long acceleration is applied to each body.")]
        public bool RandomForceDuration;
        [Tooltip("Scaled seconds of acceleration. Zero applies one instantaneous impulse instead.")]
        public float ForceDuration;
        [Tooltip("Minimum and maximum acceleration duration in scaled seconds.")]
        public Vector2 ForceDurationRange = new Vector2(0.1f, 1f);
        [Header("Player Suspension")]
        [Tooltip("Also suspend active Player Studio CharacterController motors without adding Rigidbody components.")]
        public bool AffectPlayer;
        [Tooltip("Require the player to pass the same layer, identity and interaction filters as physical objects.")]
        public bool FilterPlayer;
        [Tooltip("Virtual body and controls while floating. With overlapping generators, the most recently acquired source controls damping and steering; forces remain additive.")]
        public PlayerFloatSettings Player = new PlayerFloatSettings();

        #endregion
        #region Methods
        #region Validation

        /// <summary>Validates enabled pulse, restoration and force branches.</summary>
        /// <param name="warning">Receives an invalid range, direction or required setting.</param>
        /// <returns>True when the reusable generator configuration is complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Configure gravity selection.";
            if ((AffectObjects || AffectPlayer && FilterPlayer) && (Filter == null || !Filter.TryValidate(out warning)))
                return false;
            warning = "Enable affected objects, the player, or both.";
            if (!AffectObjects && !AffectPlayer)
                return false;
            warning = "Configure player suspension.";
            if (AffectPlayer && (Player == null || !Player.TryValidate(out warning)))
                return false;
            warning = "Choose a positive interval, supported restoration mode and positive enabled timer duration.";
            if ((RandomInterval ? !Range(IntervalRange, true) : !InteractionValues.Positive(Interval))
                || Restore is not (GravityRestoreMode.Timer or GravityRestoreMode.Interaction or GravityRestoreMode.TimerOrInteraction)
                || Restore != GravityRestoreMode.Interaction && !InteractionValues.Positive(Duration))
                return false;
            if (Restore != GravityRestoreMode.Timer && (Target == null || !Target.TryValidate(out warning)))
                return false;
            warning = "Force needs a nonzero finite direction, ordered finite ranges and non-negative intensity and duration.";
            if (Push && ((RandomIntensity ? !Range(IntensityRange, false) : !Nonnegative(Intensity))
                || (RandomForceDuration ? !Range(ForceDurationRange, false) : !Nonnegative(ForceDuration))
                || (RandomDirection ? !InteractionValues.Finite(DirectionMinimum) || !InteractionValues.Finite(DirectionMaximum)
                    || DirectionMinimum.x > DirectionMaximum.x || DirectionMinimum.y > DirectionMaximum.y || DirectionMinimum.z > DirectionMaximum.z
                    || DirectionMinimum == Vector3.zero && DirectionMaximum == Vector3.zero
                    : !InteractionValues.Finite(Direction) || Direction.sqrMagnitude < 0.000001f)))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks an authored random range without sorting or clamping it.</summary>
        /// <param name="range">Minimum and maximum values.</param>
        /// <param name="positive">Whether zero is excluded.</param>
        /// <returns>True for ordered finite values in the required domain.</returns>
        private static bool Range(Vector2 range, bool positive)
        {
            return Nonnegative(range.x) && Nonnegative(range.y) && range.y >= range.x && (!positive || range.x > 0f);
        }

        /// <summary>Checks a finite value that may be zero.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for finite non-negative values.</returns>
        private static bool Nonnegative(float value)
        {
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion
        #region Sampling

        /// <summary>Samples the shared force policy once per acquired body or player.</summary>
        /// <param name="owner">Generator orientation used for local directions.</param>
        /// <param name="duration">Receives zero for an impulse or the sampled acceleration duration.</param>
        /// <returns>World-space impulse or acceleration, before any player's virtual-body multiplier.</returns>
        internal Vector3 SampleForce(Transform owner, out float duration)
        {
            duration = 0f;
            if (!Push)
                return Vector3.zero;
            Vector3 direction = RandomDirection ? new Vector3(
                UnityEngine.Random.Range(DirectionMinimum.x, DirectionMaximum.x),
                UnityEngine.Random.Range(DirectionMinimum.y, DirectionMaximum.y),
                UnityEngine.Random.Range(DirectionMinimum.z, DirectionMaximum.z)) : Direction;
            if (direction.sqrMagnitude < 0.000001f)
                direction = DirectionMaximum.sqrMagnitude > 0f ? DirectionMaximum : DirectionMinimum;
            direction.Normalize();
            if (LocalDirection)
                direction = owner.TransformDirection(direction);
            duration = RandomForceDuration ? UnityEngine.Random.Range(ForceDurationRange.x, ForceDurationRange.y) : ForceDuration;
            return direction * (RandomIntensity ? UnityEngine.Random.Range(IntensityRange.x, IntensityRange.y) : Intensity);
        }

        #endregion
        #endregion
    }
}
