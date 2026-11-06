using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Configures one optional pooled burst attached to the arm or firing point.</summary>
    [Serializable]
    public sealed class PlayerToolEffectSettings
    {
        #region Fields

        [Header("Visual")]
        [Tooltip("Effect prefab. Leave empty to skip this effect.")]
        public GameObject Prefab;
        [Tooltip("Muzzle Effect attachment. Leave empty to use Tools Hierarchy Root.")]
        public string Path = string.Empty;
        [Tooltip("Position offset from the arm or firing point.")]
        public Vector3 Position;
        [Tooltip("Extra rotation, in degrees.")]
        public Vector3 Rotation;
        [Tooltip("Size of the effect.")]
        [Range(0.01f, 5f)]
        public float Scale = 1f;
        [Tooltip("Game seconds before the effect is cleared. Enable Fit Particle Playback to stretch or compress its particle timing.")]
        [Range(0.01f, 30f)]
        public float Lifetime = 0.3f;
        [Tooltip("Optional particle and trail timing for this effect.")]
        public PlayerToolEffectPlayback Playback = new PlayerToolEffectPlayback();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks enabled bursts without changing unused values or source prefabs.</summary>
        /// <param name="attachment">Whether this effect selects an independent arm attachment.</param>
        /// <param name="warning">Receives the first invalid effect setting.</param>
        /// <returns>True when disabled or ready for a bounded visual pool.</returns>
        public bool TryValidate(bool attachment, out string warning)
        {
            // Unassigned effects retain their settings without preventing weapon use.
            warning = "Enabled shot effects need a hierarchy path, finite offsets and positive finite scale and lifetime.";
            if (Prefab != null && (attachment && Path == null || !PlayerToolWeaponSettings.Finite(Position)
                || !PlayerToolWeaponSettings.Finite(Rotation) || !float.IsFinite(Scale) || Scale <= 0f
                || !float.IsFinite(Lifetime) || Lifetime <= 0f))
                return false;
            if (Prefab != null && Playback != null && !Playback.TryValidate(Lifetime))
            {
                warning = "Fit Particle Playback requires a positive finite reference duration.";
                return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }

    /// <summary>Fits an effect's native particle and trail timing into a requested pool lifetime.</summary>
    [Serializable]
    public sealed class PlayerToolEffectPlayback
    {
        #region Fields

        [Header("Timing")]
        [Tooltip("Stretch or compress the particle systems and trails to match Lifetime. Leave off to keep their prefab timing.")]
        public bool FitParticlePlayback;
        [Tooltip("Seconds from the start of the prefab preview to its last visible particle at native speed. For a loop, use the playback span to fit.")]
        [Range(0.01f, 30f)]
        public float ReferenceDuration = 1f;

        #endregion

        #region Methods

        #region Timing

        /// <summary>Derives the playback multiplier once before a visual pool is prepared.</summary>
        /// <param name="lifetime">Requested lifetime in game seconds.</param>
        /// <returns>Positive playback multiplier, or zero to retain the prefab timing.</returns>
        internal float Speed(float lifetime)
        {
            // One ratio keeps child delays, motion and fades in sync.
            return FitParticlePlayback ? ReferenceDuration / lifetime : 0f;
        }

        /// <summary>Checks the effective multiplier without changing stored times.</summary>
        /// <param name="lifetime">Positive pool lifetime already checked by the caller.</param>
        /// <returns>True when fitting is off or the multiplier is positive and finite.</returns>
        internal bool TryValidate(float lifetime)
        {
            // An unused reference never blocks the preset.
            if (!FitParticlePlayback)
                return true;
            float speed = Speed(lifetime);
            return float.IsFinite(speed) && speed > 0f;
        }

        #endregion

        #endregion
    }
}
