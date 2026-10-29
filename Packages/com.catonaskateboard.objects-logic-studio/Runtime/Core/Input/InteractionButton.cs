using System;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Buffers performed callbacks across dynamic or fixed input updates until the observer consumes them.</summary>
    internal sealed class InteractionButton : IDisposable
    {
        #region State

        private readonly InputAction action;
        private bool pending;
        private bool pressed;
        private InputControl control;
        private int consumedPulse = -1;

        #endregion

        #region Properties

        /// <summary>Whether an enabled button has performed since the last observer dispatch.</summary>
        internal bool Pending => InteractionProjectileInput.Active
            ? action.enabled && InteractionProjectileInput.Matches(action) && consumedPulse != InteractionProjectileInput.Serial
            : pending && action.enabled && !InteractionProjectileInput.Blocks(action);
        /// <summary>Fresh button actuation, including actions whose performed callback waits for Hold.</summary>
        internal bool Pressed => InteractionProjectileInput.Active ? Pending : pressed && action.enabled && !InteractionProjectileInput.Blocks(action);
        /// <summary>Whether the owning PlayerInput currently enables this action.</summary>
        internal bool Enabled => action.enabled;
        /// <summary>Current held state from the player's private action, independent of buffered presses.</summary>
        internal bool Held => InteractionProjectileInput.Active ? Pending : action.enabled && action.IsPressed() && !InteractionProjectileInput.Blocks(action);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Subscribes to an already owned action without enabling it or its map.</summary>
        /// <param name="action">Button from the current player's runtime action asset.</param>
        internal InteractionButton(InputAction action)
        {
            // One signal is shared by every object that references the same action.
            this.action = action;
            // Registry rebuilds during a held charge must retain the control needed to suppress its later release.
            control = action.activeControl;
            action.started += Started;
            action.performed += Performed;
            InputSystem.onActionChange += ActionChanged;
        }

        /// <summary>Detaches callbacks when input ownership or registered features change.</summary>
        public void Dispose()
        {
            // Action maps belong to PlayerInput and remain untouched.
            action.performed -= Performed;
            action.started -= Started;
            InputSystem.onActionChange -= ActionChanged;
            pending = pressed = false;
        }

        #endregion

        #region Signals

        /// <summary>Retains initial actuation separately from tap or hold completion.</summary>
        /// <param name="context">Input System callback identifying the actual device control.</param>
        private void Started(InputAction.CallbackContext context)
        {
            // Prolonged assembly owns its duration instead of waiting for a second configured Hold timer.
            pressed = true;
            control = context.control;
        }

        /// <summary>Retains a performed event even when button release occurs in the same input update.</summary>
        /// <param name="context">Input System callback for this button.</param>
        private void Performed(InputAction.CallbackContext context)
        {
            // The interaction executes once on performed, respecting configured tap/hold interactions.
            pending = true;
            pressed = true;
            control = context.control;
        }

        /// <summary>Recognizes the same physical press even when two actions have different stable IDs.</summary>
        /// <param name="other">Pending button owned by the competing driver.</param>
        /// <returns>True for the same action or the same device control in this dispatch pass.</returns>
        internal bool SharesPress(InteractionButton other)
        {
            // Pending state is checked by dispatch; control identity keeps keyboard and gamepad presses separate.
            return other != null && (ReferenceEquals(action, other.action)
                || (action.activeControl ?? control) != null
                    && ReferenceEquals(action.activeControl ?? control, other.action.activeControl ?? other.control));
        }

        /// <summary>Prevents a disabled and re-enabled map from replaying an old button event.</summary>
        /// <param name="changed">Action or map whose state changed.</param>
        /// <param name="change">Input System ownership notification.</param>
        private void ActionChanged(object changed, InputActionChange change)
        {
            // Ordinary button cancellation does not erase a same-update performed event.
            if ((change == InputActionChange.ActionDisabled && ReferenceEquals(changed, action))
                || (change == InputActionChange.ActionMapDisabled && ReferenceEquals(changed, action.actionMap)))
                pending = pressed = false;
        }

        /// <summary>Consumes the frame's buffered event after target arbitration.</summary>
        internal void Clear()
        {
            // Consuming a virtual hit never erases unrelated physical presses buffered for the normal pass.
            if (InteractionProjectileInput.Active)
            {
                if (InteractionProjectileInput.Matches(action))
                    consumedPulse = InteractionProjectileInput.Serial;
                return;
            }
            // All candidates see the same event before this shared reset.
            pending = pressed = false;
        }

        #endregion

        #endregion
    }
}
