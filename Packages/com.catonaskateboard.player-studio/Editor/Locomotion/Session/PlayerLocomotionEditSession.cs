using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Holds movement numbers apart from their asset until the shared session confirms them.</summary>
    [Serializable]
    internal sealed class PlayerLocomotionEditSession
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Locomotion asset opened through the selected master's applied slot.")]
        [SerializeField]
        private PlayerLocomotionPreset source;

        [Header("Baseline")]
        [Tooltip("Speed last read from the asset, used to detect outside edits.")]
        [SerializeField]
        private float originalSpeed;

        [Tooltip("Acceleration last read from the asset.")]
        [SerializeField]
        private float originalAcceleration;

        [Tooltip("Deceleration last read from the asset.")]
        [SerializeField]
        private float originalDeceleration;

        [Header("Draft")]
        [Tooltip("Proposed maximum speed in metres per second; not applied to the asset yet.")]
        [SerializeField]
        private float speed;

        [Tooltip("Proposed response rate while input requests a direction.")]
        [SerializeField]
        private float acceleration;

        [Tooltip("Proposed braking rate after release.")]
        [SerializeField]
        private float deceleration;

        [Header("Gravity Baseline and Draft")]
        [Tooltip("Gravity baseline used to detect external edits.")]
        [SerializeField]
        private bool originalUseGravity;

        [Tooltip("Apply world-down gravity through the native controller; disable for planar-only movement.")]
        [SerializeField]
        private bool useGravity;

        [Tooltip("Gravity baseline used to detect external edits.")]
        [SerializeField]
        private float originalGravityAcceleration;

        [Tooltip("Downward acceleration in metres per second squared; must be positive when gravity is enabled.")]
        [SerializeField]
        private float gravityAcceleration;

        [Tooltip("Gravity baseline used to detect external edits.")]
        [SerializeField]
        private float originalTerminalSpeed;

        [Tooltip("Maximum downward speed in metres per second; this limits falling without changing the stored preset.")]
        [SerializeField]
        private float terminalSpeed;

        [Tooltip("Gravity baseline used to detect external edits.")]
        [SerializeField]
        private float originalGroundSpeed;

        [Tooltip("Downward request after ground contact, in metres per second. Positive and no greater than Terminal Speed; not a ground teleport.")]
        [SerializeField]
        private float groundSpeed;

        [Header("Jump Baseline and Draft")]
        [Tooltip("Applied jump baseline used to detect outside edits.")]
        [SerializeField]
        private bool originalUseJump;

        [Tooltip("Whether the draft enables jumping; requires gravity and a Button mapping.")]
        [SerializeField]
        private bool useJump;

        [Tooltip("Applied jump baseline used to detect outside edits.")]
        [SerializeField]
        private float originalJumpHeight;

        [Tooltip("Proposed jump height above takeoff, in metres.")]
        [SerializeField]
        private float jumpHeight;

        [Tooltip("Applied jump baseline used to detect outside edits.")]
        [SerializeField]
        private float originalJumpBufferTime;

        [Tooltip("Seconds a press can wait for support; zero disables the extra interval.")]
        [SerializeField]
        private float jumpBufferTime;

        [Tooltip("Applied jump baseline used to detect outside edits.")]
        [SerializeField]
        private float originalCoyoteTime;

        [Tooltip("Seconds one jump remains available after walking off support.")]
        [SerializeField]
        private float coyoteTime;

        #endregion

        #region Properties

        /// <summary>The asset that may receive this draft.</summary>
        public PlayerLocomotionPreset Source => source;

        /// <summary>The exact speed entered in the draft.</summary>
        public float Speed => speed;

        /// <summary>The exact active-input response rate entered in the draft.</summary>
        public float Acceleration => acceleration;

        /// <summary>The exact release braking rate entered in the draft.</summary>
        public float Deceleration => deceleration;

        /// <summary>Apply world-down gravity through the native controller; disable for planar-only movement.</summary>
        public bool UseGravity => useGravity;

        /// <summary>Downward acceleration in metres per second squared; must be positive when gravity is enabled.</summary>
        public float GravityAcceleration => gravityAcceleration;

        /// <summary>Maximum downward speed in metres per second; this limits falling without changing the stored preset.</summary>
        public float TerminalSpeed => terminalSpeed;

        /// <summary>Downward request after ground contact, in metres per second. Positive and no greater than Terminal Speed; not a ground teleport.</summary>
        public float GroundSpeed => groundSpeed;

        /// <summary>Whether the draft enables jumping; requires gravity and a Button mapping.</summary>
        public bool UseJump => useJump;

        /// <summary>Proposed jump height above takeoff, in metres.</summary>
        public float JumpHeight => jumpHeight;

        /// <summary>Seconds a press can wait for support; zero disables the extra interval.</summary>
        public float JumpBufferTime => jumpBufferTime;

        /// <summary>Seconds one jump remains available after walking off support.</summary>
        public float CoyoteTime => coyoteTime;

        /// <summary>Detects pending values even after the source reference is lost.</summary>
        public bool HasChanges => !speed.Equals(originalSpeed) || !acceleration.Equals(originalAcceleration)
            || !deceleration.Equals(originalDeceleration)
            || !useGravity.Equals(originalUseGravity)
            || !gravityAcceleration.Equals(originalGravityAcceleration)
            || !terminalSpeed.Equals(originalTerminalSpeed)
            || !groundSpeed.Equals(originalGroundSpeed)
            || !useJump.Equals(originalUseJump)
            || !jumpHeight.Equals(originalJumpHeight)
            || !jumpBufferTime.Equals(originalJumpBufferTime)
            || !coyoteTime.Equals(originalCoyoteTime);

        #endregion

        #region Methods

        #region Draft

        /// <summary>Follows the selected master only while no movement numbers are pending.</summary>
        /// <param name="master">Selected applied master, or null for direct Body editing.</param>
        public void Refresh(PlayerMasterPreset master)
        {
            // Preserve a dirty draft even when its slot changes outside the window.
            if (HasChanges)
                return;

            source = master != null ? master.LocomotionPreset : null;
            Discard();
        }

        /// <summary>Retains entered values, including invalid input, for explicit confirmation or correction.</summary>
        /// <param name="newSpeed">Proposed speed.</param>
        /// <param name="newAcceleration">Proposed active-input response rate.</param>
        /// <param name="newDeceleration">Proposed release braking rate.</param>
        public void SetDraft(float newSpeed, float newAcceleration, float newDeceleration)
        {
            // Only window state changes here, so Undo belongs to the owning window.
            speed = newSpeed;
            acceleration = newAcceleration;
            deceleration = newDeceleration;
        }

        /// <summary>Retains the optional gravity proposal without writing the asset.</summary>
        /// <param name="enabled">Whether the gravity fields will be used.</param>
        /// <param name="newAcceleration">Proposed downward acceleration.</param>
        /// <param name="newTerminalSpeed">Proposed falling speed limit.</param>
        /// <param name="newGroundSpeed">Proposed downward contact refresh speed.</param>
        public void SetGravityDraft(bool enabled, float newAcceleration, float newTerminalSpeed, float newGroundSpeed)
        {
            // Hidden values are preserved when gravity is disabled.
            useGravity = enabled;
            gravityAcceleration = newAcceleration;
            terminalSpeed = newTerminalSpeed;
            groundSpeed = newGroundSpeed;
        }

        /// <summary>Stores jump choices in the same asset draft without changing the Input preset.</summary>
        /// <param name="enabled">Whether jumping will be active.</param>
        /// <param name="height">Requested free-space height.</param>
        /// <param name="bufferTime">Requested press retention interval.</param>
        /// <param name="graceTime">Requested support grace interval.</param>
        public void SetJumpDraft(bool enabled, float height, float bufferTime, float graceTime)
        {
            // Invalid and hidden values remain available for an explicit correction.
            useJump = enabled;
            jumpHeight = height;
            jumpBufferTime = bufferTime;
            coyoteTime = graceTime;
        }

        /// <summary>Checks draft numbers using the same rules as the saved preset.</summary>
        /// <param name="warning">Receives a numeric configuration warning.</param>
        /// <returns>True when the draft can be confirmed without correcting its values.</returns>
        public bool TryValidate(out string warning)
        {
            // The immutable settings type owns the rules for both editing and runtime initialization.
            return PlayerGravitySettings.TryCreate(useGravity, gravityAcceleration, terminalSpeed, groundSpeed,
                out PlayerGravitySettings gravity, out warning)
                && PlayerJumpSettings.TryCreate(useJump, jumpHeight, jumpBufferTime, coyoteTime, gravity,
                    out PlayerJumpSettings jump, out warning)
                && PlayerLocomotionSettings.TryCreate(speed, acceleration, deceleration, gravity, jump, out _, out warning);
        }

        /// <summary>Reloads the current asset without writing over external edits.</summary>
        public void Discard()
        {
            // Read raw serialized numbers so an invalid preset can still be repaired explicitly.
            if (source != null)
            {
                using SerializedObject serialized = new SerializedObject(source);
                originalSpeed = serialized.FindProperty("speed").floatValue;
                originalAcceleration = serialized.FindProperty("acceleration").floatValue;
                originalDeceleration = serialized.FindProperty("deceleration").floatValue;
                originalUseGravity = serialized.FindProperty("useGravity").boolValue;
                originalGravityAcceleration = serialized.FindProperty("gravityAcceleration").floatValue;
                originalTerminalSpeed = serialized.FindProperty("terminalSpeed").floatValue;
                originalGroundSpeed = serialized.FindProperty("groundSpeed").floatValue;
                originalUseJump = serialized.FindProperty("useJump").boolValue;
                originalJumpHeight = serialized.FindProperty("jumpHeight").floatValue;
                originalJumpBufferTime = serialized.FindProperty("jumpBufferTime").floatValue;
                originalCoyoteTime = serialized.FindProperty("coyoteTime").floatValue;
            }
            else
            {
                originalSpeed = originalAcceleration = originalDeceleration = 0f;
                originalUseGravity = originalUseJump = false;
                originalJumpHeight = originalJumpBufferTime = originalCoyoteTime = 0f;
                originalGravityAcceleration = originalTerminalSpeed = originalGroundSpeed = 0f;
            }

            SetDraft(originalSpeed, originalAcceleration, originalDeceleration);
            SetGravityDraft(originalUseGravity, originalGravityAcceleration, originalTerminalSpeed, originalGroundSpeed);
            SetJumpDraft(originalUseJump, originalJumpHeight, originalJumpBufferTime, originalCoyoteTime);
        }

        #endregion

        #region Confirmation

        /// <summary>Prepares properties without writing them ahead of a simultaneous Transform confirmation.</summary>
        /// <param name="master">Master that must still refer to the opened locomotion asset.</param>
        /// <param name="changes">Receives pending properties owned and disposed by the shared confirmation path.</param>
        /// <param name="warning">Receives a numeric, identity or outside-edit conflict.</param>
        /// <returns>True when this asset draft is ready for the shared Apply operation.</returns>
        public bool TryPrepareApply(PlayerMasterPreset master, out SerializedObject changes, out string warning)
        {
            // A lost or redirected slot must not silently send this draft to another preset.
            changes = null;
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode || master == null || source == null
                || master.LocomotionPreset != source || !EditorUtility.IsPersistent(source))
            {
                warning = "The Locomotion slot is unavailable or changed. Use Discard to reload the current master in Edit mode.";
                return false;
            }

            if (!TryValidate(out warning) || !HasChanges)
                return warning.Length == 0;

            // Compare every edited property immediately before preparing the candidate.
            SerializedObject serialized = new SerializedObject(source);
            if (!serialized.FindProperty("speed").floatValue.Equals(originalSpeed)
                || !serialized.FindProperty("acceleration").floatValue.Equals(originalAcceleration)
                || !serialized.FindProperty("deceleration").floatValue.Equals(originalDeceleration)
                || !serialized.FindProperty("useGravity").boolValue.Equals(originalUseGravity)
                || !serialized.FindProperty("gravityAcceleration").floatValue.Equals(originalGravityAcceleration)
                || !serialized.FindProperty("terminalSpeed").floatValue.Equals(originalTerminalSpeed)
                || !serialized.FindProperty("groundSpeed").floatValue.Equals(originalGroundSpeed)
                || !serialized.FindProperty("useJump").boolValue.Equals(originalUseJump)
                || !serialized.FindProperty("jumpHeight").floatValue.Equals(originalJumpHeight)
                || !serialized.FindProperty("jumpBufferTime").floatValue.Equals(originalJumpBufferTime)
                || !serialized.FindProperty("coyoteTime").floatValue.Equals(originalCoyoteTime))
            {
                serialized.Dispose();
                warning = "The Locomotion preset changed outside this session. Discard to reload its current values.";
                return false;
            }

            changes = serialized;
            changes.FindProperty("speed").floatValue = speed;
            changes.FindProperty("acceleration").floatValue = acceleration;
            changes.FindProperty("deceleration").floatValue = deceleration;
            changes.FindProperty("useGravity").boolValue = useGravity;
            changes.FindProperty("gravityAcceleration").floatValue = gravityAcceleration;
            changes.FindProperty("terminalSpeed").floatValue = terminalSpeed;
            changes.FindProperty("groundSpeed").floatValue = groundSpeed;
            changes.FindProperty("useJump").boolValue = useJump;
            changes.FindProperty("jumpHeight").floatValue = jumpHeight;
            changes.FindProperty("jumpBufferTime").floatValue = jumpBufferTime;
            changes.FindProperty("coyoteTime").floatValue = coyoteTime;
            return true;
        }

        #endregion

        #endregion
    }
}
