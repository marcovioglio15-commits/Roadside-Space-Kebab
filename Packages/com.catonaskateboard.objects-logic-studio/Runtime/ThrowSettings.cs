using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses whether throw strength describes impulse or a mass-independent launch speed.</summary>
    public enum ThrowStrengthMode { Impulse, Speed }

    /// <summary>Defines a ballistic launch in the Observer camera's reference frame.</summary>
    [Serializable]
    public sealed class ThrowSettings
    {
        #region Fields

        [Header("Launch")]
        [Tooltip("Impulse uses newton-seconds and respects mass. Speed adds metres per second independently of mass.")]
        public ThrowStrengthMode Mode = ThrowStrengthMode.Speed;
        [Tooltip("Launch impulse or speed, according to Strength Mode.")]
        public float Strength = 8f;
        [Tooltip("Yaw in degrees to the right of camera aim.")]
        public float Yaw;
        [Tooltip("Elevation in degrees above camera aim. Positive values create an upward arc.")]
        public float Elevation = 10f;
        [Tooltip("Initial angular velocity in radians per second around camera right, up and forward axes.")]
        public Vector3 Spin;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects unusable launch values before any held object is released.</summary>
        /// <param name="warning">Receives the first invalid launch setting.</param>
        /// <returns>True when the configured trajectory is finite.</returns>
        public bool TryValidate(out string warning)
        {
            // Angular limits describe an aim-relative cone without rewriting authored values.
            warning = string.Empty;
            if (Mode is not (ThrowStrengthMode.Impulse or ThrowStrengthMode.Speed) || !InteractionValues.Positive(Strength)
                || !InteractionValues.Finite(Yaw) || Mathf.Abs(Yaw) > 180f
                || !InteractionValues.Finite(Elevation) || Mathf.Abs(Elevation) > 90f || !InteractionValues.Finite(Spin))
                warning = "Throw needs positive finite strength, yaw within ±180°, elevation within ±90° and finite spin.";
            return warning.Length == 0;
        }

        #endregion

        #region Launch

        /// <summary>Calculates the aim-relative launch direction for runtime and debug drawing.</summary>
        /// <param name="rotation">Gameplay camera rotation.</param>
        /// <returns>A normalized launch direction in world space.</returns>
        public Vector3 Direction(Quaternion rotation)
        {
            // Unity's positive X rotation points down, so upward elevation uses its negative.
            return rotation * Quaternion.Euler(-Elevation, Yaw, 0f) * Vector3.forward;
        }

        #endregion

        #endregion
    }
}
