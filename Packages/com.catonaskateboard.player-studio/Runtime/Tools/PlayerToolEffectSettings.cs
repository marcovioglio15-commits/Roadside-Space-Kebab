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
        [Tooltip("Part of the arm to attach to. Leave empty to use Tools Hierarchy Root.")]
        public string Path = string.Empty;
        [Tooltip("Position offset from the arm or firing point.")]
        public Vector3 Position;
        [Tooltip("Extra rotation, in degrees.")]
        public Vector3 Rotation;
        [Tooltip("Size of the effect.")]
        [Range(0.01f, 5f)]
        public float Scale = 1f;
        [Tooltip("Seconds before the effect disappears. Also stops looping particles.")]
        [Range(0.01f, 30f)]
        public float Lifetime = 0.3f;

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
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
