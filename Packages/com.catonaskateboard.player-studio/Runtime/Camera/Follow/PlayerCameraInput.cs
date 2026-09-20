using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Caches camera actions from one PlayerInput without owning maps, devices or action names.</summary>
    internal sealed class PlayerCameraInput : IDisposable
    {
        #region State

        private readonly PlayerButtonBinding cursor = new PlayerButtonBinding();
        private PlayerInput owner;
        private InputAction delta;
        private InputAction rate;
        private Vector2 pendingDelta;
        private Vector2 currentRate;
        private bool interrupted;

        #endregion

        #region Methods

        #region Connection

        /// <summary>Resolves configured action IDs once when the camera activates.</summary>
        /// <param name="input">Existing input owner initialized by Unity.</param>
        /// <param name="preset">Input role references for this player.</param>
        /// <param name="warning">Receives a missing owner or action warning.</param>
        /// <returns>True when at least one configured look action is available.</returns>
        public bool Connect(PlayerInput input, PlayerInputPreset preset, out string warning)
        {
            // Reconnection releases this subscriber without disabling someone else's actions.
            Dispose();
            warning = "Camera look needs an active PlayerInput and Input preset.";
            if (input == null || !input.isActiveAndEnabled || input.actions == null || preset == null)
                return false;
            if (!preset.TryGetLookIds(out Guid deltaId, out Guid rateId, out Guid toggleId, out warning))
                return false;
            delta = deltaId != Guid.Empty ? input.actions.FindAction(deltaId) : null;
            rate = rateId != Guid.Empty ? input.actions.FindAction(rateId) : null;
            InputAction toggle = toggleId != Guid.Empty ? input.actions.FindAction(toggleId) : null;
            if ((deltaId != Guid.Empty && delta == null) || (rateId != Guid.Empty && rate == null)
                || (toggleId != Guid.Empty && toggle == null) || (delta == null && rate == null))
            {
                warning = "Assign at least one Look action whose ID exists in this player's action asset.";
                return false;
            }
            if (delta != null && !PlayerLookActionCompatibility.IsCompatible(delta, true)
                || rate != null && !PlayerLookActionCompatibility.IsCompatible(rate, false)
                || toggle != null && toggle.type != InputActionType.Button)
            {
                warning = "PlayerInput Look Delta needs displacement, Look Rate needs continuous Vector2 values, and Cursor must be a Button.";
                Dispose();
                return false;
            }
            owner = input;
            cursor.Bind(toggle);
            InputSystem.onAfterUpdate += Capture;
            InputSystem.onActionChange += HandleActionChange;
            warning = string.Empty;
            return true;
        }

        /// <summary>Releases cursor callbacks and cached references without changing the input owner.</summary>
        public void Dispose()
        {
            // Called on disable and explicit reinitialization, never once per frame.
            InputSystem.onAfterUpdate -= Capture;
            InputSystem.onActionChange -= HandleActionChange;
            cursor.Bind(null);
            DiscardPending();
            owner = null;
            delta = null;
            rate = null;
        }

        #endregion

        #region Consumption

        /// <summary>Consumes collected displacement once and integrates the held rate over this camera frame.</summary>
        /// <param name="settings">Validated sensitivities and inversion.</param>
        /// <param name="duration">Elapsed frame duration used only for rate input.</param>
        /// <param name="toggle">Receives a single cursor-toggle press edge.</param>
        /// <param name="wasInterrupted">Receives whether input ownership was interrupted since the previous read.</param>
        /// <returns>Yaw and pitch displacement in degrees, or zero while input is inactive.</returns>
        public Vector2 Read(in PlayerCameraSettings settings, float duration, out bool toggle, out bool wasInterrupted)
        {
            // Input processing and rendering may run at different rates; never poll a delta twice between input updates.
            bool active = owner != null && owner.isActiveAndEnabled && owner.inputIsActive;
            bool deltaActive = active && IsAvailable(delta);
            bool rateActive = active && IsAvailable(rate);
            toggle = cursor.Consume(active, owner != null ? owner.actions : null).Pressed;
            if (!deltaActive && !rateActive)
                DiscardPending();
            wasInterrupted = interrupted;
            interrupted = false;
            Vector2 command = Scale(deltaActive ? pendingDelta : Vector2.zero,
                rateActive ? currentRate : Vector2.zero, settings, duration);
            pendingDelta = Vector2.zero;
            return command;
        }

        /// <summary>Converts displacement and normalized stick deflection to the same angular unit.</summary>
        /// <param name="delta">Accumulated pointer displacement since the previous simulation step.</param>
        /// <param name="rate">Current stick deflection, limited to the unit circle.</param>
        /// <param name="settings">Validated per-device calibration and inversion.</param>
        /// <param name="duration">Elapsed simulation seconds, applied only to rate input.</param>
        /// <returns>Yaw and pitch displacement in degrees.</returns>
        internal static Vector2 Scale(Vector2 delta, Vector2 rate, in PlayerCameraSettings settings, float duration)
        {
            // Normalize rate magnitude without turning mouse displacement into a frame-dependent velocity.
            Vector2 change = Vector2.Scale(delta, settings.DeltaSensitivity)
                + Vector2.Scale(Vector2.ClampMagnitude(rate, 1f), settings.RateSensitivity) * duration;
            change.y *= settings.InvertY ? 1f : -1f;
            return change;
        }

        /// <summary>Forgets unconsumed input at a pause, focus or ownership boundary.</summary>
        public void DiscardPending()
        {
            // Held rate is sampled again by the next input update; displaced pointer motion must never replay.
            pendingDelta = currentRate = Vector2.zero;
            interrupted = true;
        }

        /// <summary>Checks a cached role against the current owner's action instance.</summary>
        /// <param name="action">Optional role resolved during connection.</param>
        /// <returns>True when the role still belongs to the validated active owner.</returns>
        private bool IsAvailable(InputAction action)
        {
            // Callers check the owner before checking its individual roles.
            return action != null && action.enabled && action.actionMap.asset == owner.actions;
        }

        #endregion

        #region Input Updates

        /// <summary>Collects each completed gameplay input update, excluding editor and rendering-only passes.</summary>
        private void Capture()
        {
            // BeforeRender does not reset pointer deltas; counting it would duplicate gameplay displacement.
            if ((InputState.currentUpdateType & (InputUpdateType.Dynamic | InputUpdateType.Fixed | InputUpdateType.Manual)) == 0)
                return;
            if (owner == null || !owner.isActiveAndEnabled || !owner.inputIsActive)
            {
                DiscardPending();
                return;
            }

            // Read the final accumulated action value once, rather than summing cumulative performed callbacks.
            pendingDelta = IsAvailable(delta) ? pendingDelta + delta.ReadValue<Vector2>() : Vector2.zero;
            currentRate = IsAvailable(rate) ? rate.ReadValue<Vector2>() : Vector2.zero;
        }

        /// <summary>Invalidates queued motion even when a look map is disabled and re-enabled between frames.</summary>
        /// <param name="changed">Action or map affected by the ownership transition.</param>
        /// <param name="change">Input System action lifecycle notification.</param>
        private void HandleActionChange(object changed, InputActionChange change)
        {
            // A temporarily disabled role cannot carry an old delta or smoothing tail into its next activation.
            switch (change)
            {
                case InputActionChange.ActionDisabled:
                    if (changed == delta || changed == rate)
                        DiscardPending();
                    break;
                case InputActionChange.ActionMapDisabled:
                    if (changed == delta?.actionMap || changed == rate?.actionMap)
                        DiscardPending();
                    break;
            }
        }

        #endregion

        #endregion
    }
}
