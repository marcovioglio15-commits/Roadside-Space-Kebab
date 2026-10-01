using System;
using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Matches the Surface labels in the project's footstep event.</summary>
    public enum FootstepSurface { Concrete, ConcreteWet, Dirt, Metal }

    /// <summary>Maps physics layers to a footstep parameter without changing object identity.</summary>
    [Serializable]
    public sealed class FootstepLayers
    {
        #region Fields

        [Tooltip("Ground collider layers using this surface. The first matching row wins.")]
        public LayerMask Layers;
        [Tooltip("FMOD Surface parameter label for these ground layers.")]
        public FootstepSurface Surface;

        #endregion
    }

    /// <summary>Defines player footsteps and meaningful body-impact sounds.</summary>
    [Serializable]
    public sealed class PlayerAudioSettings
    {
        #region Fields

        [Header("Footsteps")]
        [Tooltip("Play footsteps while grounded and moving. Active first-person Head Tilt supplies step timing.")]
        public bool Footsteps = true;
        [Tooltip("Seconds between steps when first-person Head Tilt is inactive.")]
        public float Interval = 0.45f;
        [Tooltip("Minimum achieved horizontal speed in metres per second for audible steps.")]
        public float MinimumSpeed = 0.15f;
        [Tooltip("Surface used when no layer mapping matches the supporting collider.")]
        public FootstepSurface DefaultSurface;
        [Tooltip("Ground layer mappings in priority order; these do not change collision masks.")]
        public FootstepLayers[] Surfaces = Array.Empty<FootstepLayers>();
        [Header("Player Impacts")]
        [Tooltip("Body-impact speed threshold and repetition limit.")]
        public ImpactAudioSettings Collision = new ImpactAudioSettings { MinimumSpeed = 3f, Cooldown = 0.25f };

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks enabled audio settings while preserving invalid values for correction.</summary>
        /// <param name="warning">Receives a configuration issue.</param>
        /// <returns>True when footstep and collision options are usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Audio validation never snaps thresholds or rewrites the surface catalog.
            warning = "Player audio needs finite positive step timing and nonnegative speed/collision thresholds.";
            if (Collision == null || !Collision.IsValid() || Footsteps && (!float.IsFinite(Interval) || Interval <= 0f
                || !float.IsFinite(MinimumSpeed) || MinimumSpeed < 0f || Surfaces == null))
                return false;
            if (Footsteps)
            {
                if (DefaultSurface is < FootstepSurface.Concrete or > FootstepSurface.Metal)
                    return false;
                foreach (FootstepLayers row in Surfaces)
                    if (row == null || row.Surface is < FootstepSurface.Concrete or > FootstepSurface.Metal)
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
