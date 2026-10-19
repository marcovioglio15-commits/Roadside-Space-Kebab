using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Controls optional spatial camera oscillation without changing player look input.</summary>
    [Serializable]
    public sealed class PlayerCameraRumbleSettings
    {
        #region Fields

        [Header("Camera Rumble")]
        [Tooltip("Apply camera oscillation to players within the configured distance.")]
        public bool Enabled;
        [Tooltip("Play a short pulse when the effect begins.")]
        public bool AtStart = true;
        [Tooltip("Keep oscillating throughout the active effect.")]
        public bool Sustained;
        [Tooltip("Play a short pulse when the effect ends normally.")]
        public bool AtEnd = true;
        [Tooltip("Maximum distance in metres, with smooth attenuation towards its edge.")]
        [Range(0.5f, 100f)]
        public float Range = 20f;
        [Tooltip("Duration in seconds of the start and end pulses.")]
        [Range(0.01f, 3f)]
        public float PulseDuration = 0.35f;
        [Tooltip("Oscillation cycles per second.")]
        [Range(0.1f, 50f)]
        public float Frequency = 12f;
        [Tooltip("Camera-local displacement amplitude in metres, independently for each axis.")]
        public Vector3 Position = new Vector3(0.02f, 0.02f, 0f);
        [Tooltip("Camera-local angular amplitude in degrees, independently for each axis.")]
        public Vector3 Rotation = new Vector3(0.7f, 0.5f, 0.8f);
        [Tooltip("Use the custom one-cycle waveform instead of a sine wave.")]
        public bool CustomWave;
        [Tooltip("One oscillation cycle: horizontal time from zero to one, vertical displacement typically from minus one to one.")]
        public AnimationCurve Wave = AnimationCurve.Linear(0f, -1f, 1f, 1f);

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled feedback without normalizing authored amplitudes.</summary>
        /// <param name="warning">Receives invalid timing, range or waveform.</param>
        /// <returns>True for disabled or usable camera feedback.</returns>
        public bool TryValidate(out string warning)
        {
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (!float.IsFinite(Range) || Range <= 0f || !float.IsFinite(PulseDuration) || PulseDuration <= 0f
                || !float.IsFinite(Frequency) || Frequency <= 0f || !Finite(Position) || !Finite(Rotation)
                || CustomWave && (Wave == null || Wave.length < 2))
                warning = "Camera rumble needs positive finite range, timing and frequency, finite amplitudes and a complete custom waveform.";
            return warning.Length == 0;
        }

        /// <summary>Checks all axes before they can affect camera rendering.</summary>
        /// <param name="value">Authored displacement or rotation.</param>
        /// <returns>True when every axis is finite.</returns>
        private static bool Finite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion
        #endregion
    }
}
