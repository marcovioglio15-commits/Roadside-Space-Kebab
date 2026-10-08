using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Configures temporary body-like motion resolved by the player's existing CharacterController.</summary>
    [Serializable]
    public sealed class PlayerFloatSettings
    {
        #region Fields

        [Header("Virtual Body")]
        [Tooltip("Virtual mass in kilograms used to convert instantaneous impulses into velocity changes.")]
        public float Mass = 75f;
        [Tooltip("Multiplier applied to the generator's sampled impulse or acceleration for this player.")]
        public float ForceMultiplier = 1f;
        [Tooltip("Fraction of the player's current motion carried into the first overlapping suspension. Zero starts from rest.")]
        public float InitialMomentum = 1f;
        [Tooltip("Exponential velocity damping per second while suspended. Zero preserves free-flight momentum.")]
        public float Damping = 0.15f;
        [Tooltip("Maximum floating speed in metres per second, including forces and player input.")]
        public float MaximumSpeed = 12f;
        [Tooltip("Additional constant world-space acceleration while suspended. Zero produces weightless free flight.")]
        public Vector3 DriftAcceleration;
        [Header("Air Control")]
        [Tooltip("Acceleration from movement input in metres per second squared. Zero leaves motion entirely to external forces.")]
        public float ControlAcceleration = 3f;
        [Tooltip("Include the gameplay camera's pitch in forward/backward movement, allowing ascent and descent by looking up or down.")]
        public bool FollowCameraPitch;
        [Tooltip("Use the existing Jump button as an upward thruster while held, instead of initiating ordinary jumps.")]
        public bool JumpThrust;
        [Tooltip("Upward acceleration from the held Jump button in metres per second squared.")]
        public float JumpAcceleration = 3f;
        [Header("Contact and Restoration")]
        [Tooltip("Fraction of impact speed reflected away from walls, floors and ceilings. Zero slides; one is fully elastic.")]
        public float Bounce;
        [Tooltip("Fraction of tangential speed removed at each contact. Zero preserves sliding; one stops it.")]
        public float ContactFriction;
        [Tooltip("Push dynamically suspended bodies aside with reduced resistance. Static geometry and unsuspended bodies retain ordinary collision response.")]
        public bool PushSuspendedBodies;
        [Tooltip("Multiplier on the contacted body's mass for collision response against the virtual player mass. Zero gives the player no impact slowdown; small values make it dominate heavier objects without changing their real mass.")]
        public float SuspendedMassScale = 0.01f;
        [Tooltip("Multiplier on the velocity transferred to suspended objects. One matches the resolved contact speed; slightly higher values separate them from the player sooner.")]
        public float SuspendedPushSpeed = 1.1f;
        [Tooltip("Fraction of floating velocity retained when the last suspension ends. Normal locomotion then brakes horizontal motion and gravity resumes vertically.")]
        public float RestoredMomentum = 1f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks the virtual body's active options without rewriting authored values.</summary>
        /// <param name="warning">Receives an unsupported mass, response rate or fraction.</param>
        /// <returns>True when all enabled simulation settings are finite and usable.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Player suspension needs positive mass and maximum speed, non-negative acceleration/damping, and momentum/contact fractions from zero to one.";
            if (!Nonnegative(Mass) || Mass <= 0f || !Nonnegative(MaximumSpeed) || MaximumSpeed <= 0f
                || !Nonnegative(ForceMultiplier) || !Nonnegative(Damping) || !Nonnegative(ControlAcceleration)
                || JumpThrust && !Nonnegative(JumpAcceleration) || !Fraction(InitialMomentum) || !Fraction(RestoredMomentum)
                || !Fraction(Bounce) || !Fraction(ContactFriction) || !float.IsFinite(DriftAcceleration.x)
                || !float.IsFinite(DriftAcceleration.y) || !float.IsFinite(DriftAcceleration.z))
                return false;
            warning = string.Empty;
            if (PushSuspendedBodies && (!Nonnegative(SuspendedMassScale) || !float.IsFinite(SuspendedPushSpeed) || SuspendedPushSpeed < 1f))
                warning = "Suspended-body pushing needs a finite non-negative mass scale and a push-speed multiplier of at least one.";
            return warning.Length == 0;
        }

        /// <summary>Checks rates which may explicitly disable an effect at zero.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for finite, non-negative values.</returns>
        private static bool Nonnegative(float value)
        {
            return float.IsFinite(value) && value >= 0f;
        }

        /// <summary>Checks a normalized physical response without silently clamping it.</summary>
        /// <param name="value">Authored fraction.</param>
        /// <returns>True for values in the inclusive zero-to-one range.</returns>
        private static bool Fraction(float value)
        {
            return Nonnegative(value) && value <= 1f;
        }

        #endregion
        #endregion
    }
}
