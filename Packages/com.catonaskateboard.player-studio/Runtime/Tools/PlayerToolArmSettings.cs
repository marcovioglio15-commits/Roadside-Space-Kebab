using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Identifies an existing arm and its optional looping transform tracks.</summary>
    [Serializable]
    public sealed class PlayerToolArmSettings
    {
        #region Fields

        [Header("Player Arm")]
        [Tooltip("Bind an existing player arm below Tools Hierarchy Root. No object is created or reparented.")]
        public bool Enabled;
        [Tooltip("Arm transform relative to Tools Hierarchy Root. Empty selects that root when it is a player child.")]
        public string Path = string.Empty;
        [Tooltip("Let the arm follow parent rotation with a small, bounded delay while preserving authored animations.")]
        public bool SmoothRotation;
        [Tooltip("How quickly the arm catches up with its parent's rotation. Higher values reduce the delay.")]
        [Range(1f, 40f)]
        public float RotationResponse = 14f;
        [Tooltip("Maximum angular delay from the parent in degrees, including sudden turns or teleports.")]
        [Range(0.1f, 30f)]
        public float MaximumRotationLag = 10f;
        [Tooltip("Play a looping keyframe animation on the arm while no use, reload or switch animation is running.")]
        public bool IdleEnabled;
        [Tooltip("Looping tracks relative to the selected arm. Use start-relative keys and equal endpoints for smooth vertical or horizontal sway.")]
        public PlayerToolAnimationPreset Idle;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks only enabled arm motion without rewriting authored values.</summary>
        /// <param name="warning">Receives a missing path, invalid motion value or invalid idle clip.</param>
        /// <returns>True when this arm configuration can be bound.</returns>
        public bool TryValidate(out string warning)
        {
            // Hidden motion settings retain their values without blocking an inactive arm.
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Assign an arm path and positive, finite rotation response and angular delay when smoothing is enabled.";
            if (Path == null || SmoothRotation && (!float.IsFinite(RotationResponse) || RotationResponse <= 0f
                || !float.IsFinite(MaximumRotationLag) || MaximumRotationLag <= 0f || MaximumRotationLag > 30f))
                return false;
            warning = "Assign a valid idle animation when arm idle is enabled.";
            if (IdleEnabled && (Idle == null || !Idle.TryValidate(out warning)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
