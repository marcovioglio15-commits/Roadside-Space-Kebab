using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Configures optional crouching next to jumping without changing the standing body preset.</summary>
    [Serializable]
    public sealed class PlayerCrouchSettings
    {
        #region Fields

        [Header("Crouch")]
        [Tooltip("Enable crouching through the Input preset's Crouch Button action.")]
        public bool Enabled;
        [Tooltip("Press once to crouch and again to stand. Disable to crouch only while the button is held.")]
        public bool Toggle = true;
        [Tooltip("Crouched capsule height as a fraction of standing height. Must still fit twice the body radius.")]
        [Range(0.25f, 0.95f)]
        public float HeightRatio = 0.65f;
        [Tooltip("Planar speed while fully crouched as a fraction of normal movement speed.")]
        [Range(0f, 1f)]
        public float SpeedMultiplier = 0.5f;
        [Tooltip("Seconds for a full transition between standing and crouched height. Zero changes height immediately.")]
        [Range(0f, 1f)]
        public float Transition = 0.18f;
        [Tooltip("Lower the camera with the capsule while crouching.")]
        public bool LowerCamera = true;
        [Tooltip("Allow Jump while crouched. Standing is still blocked by overhead obstacles.")]
        public bool AllowJump;
        [Tooltip("Solid layers that prevent standing. Trigger colliders and the player's own hierarchy are ignored.")]
        public LayerMask ClearanceLayers = ~0;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Reports invalid active values without changing the authored configuration.</summary>
        /// <param name="warning">Receives incompatible ratios or timing.</param>
        /// <returns>True when the disabled or enabled configuration is usable.</returns>
        public bool TryValidate(out string warning)
        {
            warning = string.Empty;
            if (Enabled && (!float.IsFinite(HeightRatio) || HeightRatio <= 0f || HeightRatio >= 1f
                || !float.IsFinite(SpeedMultiplier) || SpeedMultiplier < 0f || SpeedMultiplier > 1f
                || !float.IsFinite(Transition) || Transition < 0f))
                warning = "Crouch needs a height ratio between zero and one, a speed multiplier from zero to one and finite nonnegative transition time.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
