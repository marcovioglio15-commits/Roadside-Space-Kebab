using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses how suspended objects rotate during continuous floating.</summary>
    public enum GravityFloatRotation { None, Spin, Orbit }

    /// <summary>Places an orbit center and its local up axis in the generator's frame.</summary>
    [Serializable]
    public sealed class GravityOrbitCenter
    {
        #region Fields

        [Header("Orbit Center")]
        [Tooltip("Center position in the generator's local space.")]
        public Vector3 Position;
        [Tooltip("Local Euler angles in degrees. The rotated Y axis is the orbit axis.")]
        public Vector3 Rotation;

        #endregion
    }

    /// <summary>Configures continuous floating and optional rotation within an active gravity area.</summary>
    [Serializable]
    public sealed class GravityFloatSettings
    {
        #region Fields

        [Header("Continuous Floating")]
        [Tooltip("Vertical travel above and below the floating height, in metres.")]
        [Range(0f, 1f)]
        public float Height = 0.12f;
        [Tooltip("Vertical oscillations per second.")]
        [Range(0.1f, 3f)]
        public float Frequency = 0.6f;
        [Tooltip("Maximum acceleration toward the floating path. Lower values let contacts push bodies farther away.")]
        [Range(0.5f, 60f)]
        public float Acceleration = 14f;
        [Tooltip("Spin around each object's center or orbit the closest configured center.")]
        public GravityFloatRotation Rotation;
        [Tooltip("Spin speed in degrees per second around the generator's up axis. Negative reverses direction.")]
        [Range(-180f, 180f)]
        public float SpinSpeed = 45f;
        [Tooltip("Orbit speed in degrees per second around each center's up axis. Negative reverses direction.")]
        [Range(-120f, 120f)]
        public float OrbitSpeed = 24f;
        [Tooltip("Local centers available to entering bodies. Each body orbits the center nearest when it begins floating.")]
        public GravityOrbitCenter[] Centers = { new GravityOrbitCenter() };

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates only the enabled float settings and chosen rotation mode.</summary>
        /// <param name="warning">Receives the first invalid motion setting.</param>
        /// <returns>True when the configuration can drive continuous motion.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = "Use a non-negative height and positive frequency and acceleration for continuous floating.";
            if (!float.IsFinite(Height) || Height < 0f || Height > 0f && !InteractionValues.Positive(Frequency)
                || !InteractionValues.Positive(Acceleration))
                return false;

            // Only Orbit needs center poses.
            warning = "Choose a valid rotation and a finite speed other than zero.";
            if (Rotation is not (GravityFloatRotation.None or GravityFloatRotation.Spin or GravityFloatRotation.Orbit)
                || Rotation == GravityFloatRotation.Spin && !ValidSpeed(SpinSpeed)
                || Rotation == GravityFloatRotation.Orbit && !ValidSpeed(OrbitSpeed))
                return false;
            if (Rotation == GravityFloatRotation.Orbit)
            {
                warning = "Add at least one orbit center with finite position and rotation.";
                if (Centers == null || Centers.Length == 0)
                    return false;
                foreach (GravityOrbitCenter center in Centers)
                    if (center == null || !InteractionValues.Finite(center.Position) || !InteractionValues.Finite(center.Rotation))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Allows motion in either direction at a finite nonzero speed.</summary>
        /// <param name="speed">Configured degrees per second.</param>
        /// <returns>True when the chosen rotation will move.</returns>
        private static bool ValidSpeed(float speed)
        {
            return float.IsFinite(speed) && speed != 0f;
        }

        #endregion

        #endregion
    }
}

