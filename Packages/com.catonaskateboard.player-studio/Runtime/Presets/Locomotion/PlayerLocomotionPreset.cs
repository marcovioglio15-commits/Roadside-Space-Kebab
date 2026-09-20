using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Defines the requested planar speed and response independently of input bindings and collisions.</summary>
    [CreateAssetMenu(fileName = "PlayerLocomotion", menuName = "Player Studio/Locomotion Preset")]
    public sealed class PlayerLocomotionPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Planar Movement")]
        [Tooltip("Maximum requested speed in metres per second. Analog input retains its magnitude; diagonal input cannot exceed this speed.")]
        [SerializeField]
        private float speed = 5f;

        [Tooltip("Velocity change in metres per second squared while input requests a direction, including turns and changes in analog magnitude.")]
        [SerializeField]
        private float acceleration = 20f;

        [Tooltip("Velocity change in metres per second squared when directional input is released. Must be greater than zero.")]
        [SerializeField]
        private float deceleration = 30f;

        [Header("Gravity")]
        [Tooltip("Apply world-down gravity through the native controller; disable for planar-only movement.")]
        [SerializeField]
        private bool useGravity = true;

        [Tooltip("Downward acceleration in metres per second squared; must be positive when gravity is enabled.")]
        [SerializeField]
        private float gravityAcceleration = 20f;

        [Tooltip("Maximum downward speed in metres per second; this limits falling without changing the stored preset.")]
        [SerializeField]
        private float terminalSpeed = 50f;

        [Tooltip("Downward request after ground contact, in metres per second. Positive and no greater than Terminal Speed; not a ground teleport.")]
        [SerializeField]
        private float groundSpeed = 2f;

        [Header("Jump")]
        [Tooltip("Allow one jump from support or within Coyote Time. Requires active gravity and an assigned Button action.")]
        [SerializeField]
        private bool useJump = false;

        [Tooltip("Requested free-space jump height in metres, measured above takeoff. Must be positive when jumping is enabled.")]
        [SerializeField]
        private float jumpHeight = 1.5f;

        [Tooltip("Seconds a press can wait for support. Zero disables retention beyond the current motor tick.")]
        [SerializeField]
        private float jumpBufferTime = 0.1f;

        [Tooltip("Seconds one jump remains available after walking off an edge. A performed jump spends this opportunity.")]
        [SerializeField]
        private float coyoteTime = 0.1f;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>Captures the numbers used by one initialized motor or an Editor preview.</summary>
        /// <param name="settings">Receives the validated snapshot.</param>
        /// <param name="warning">Receives a configuration warning without changing the asset.</param>
        /// <returns>True when the preset can drive planar movement.</returns>
        public bool TryGetSettings(out PlayerLocomotionSettings settings, out string warning)
        {
            // Presets and drafts share the same numeric rules.
            settings = default;
            return PlayerGravitySettings.TryCreate(useGravity, gravityAcceleration, terminalSpeed, groundSpeed,
                out PlayerGravitySettings gravity, out warning)
                && PlayerJumpSettings.TryCreate(useJump, jumpHeight, jumpBufferTime, coyoteTime, gravity,
                    out PlayerJumpSettings jump, out warning)
                && PlayerLocomotionSettings.TryCreate(speed, acceleration, deceleration, gravity, jump, out settings, out warning);
        }

        #endregion

        #region Unity Callbacks

        /// <summary>Reports invalid Inspector edits without snapping values to defaults.</summary>
        private void OnValidate()
        {
            // Leave the original numbers available for an explicit correction.
            if (!TryGetSettings(out _, out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
