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

        #endregion

        #region Properties

        /// <summary>Whether an enabled button has performed since the last observer dispatch.</summary>
        internal bool Pending => pending && action.enabled;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Subscribes to an already owned action without enabling it or its map.</summary>
        /// <param name="action">Button from the current player's runtime action asset.</param>
        internal InteractionButton(InputAction action)
        {
            // One signal is shared by every object that references the same action.
            this.action = action;
            action.performed += Performed;
            InputSystem.onActionChange += ActionChanged;
        }

        /// <summary>Detaches callbacks when input ownership or registered features change.</summary>
        public void Dispose()
        {
            // Action maps belong to PlayerInput and remain untouched.
            action.performed -= Performed;
            InputSystem.onActionChange -= ActionChanged;
            pending = false;
        }

        #endregion

        #region Signals

        /// <summary>Retains a performed event even when button release occurs in the same input update.</summary>
        /// <param name="context">Input System callback for this button.</param>
        private void Performed(InputAction.CallbackContext context)
        {
            // The interaction executes once on performed, respecting authored tap/hold interactions.
            pending = true;
        }

        /// <summary>Prevents a disabled and re-enabled map from replaying an old button event.</summary>
        /// <param name="changed">Action or map whose state changed.</param>
        /// <param name="change">Input System ownership notification.</param>
        private void ActionChanged(object changed, InputActionChange change)
        {
            // Ordinary button cancellation does not erase a same-update performed event.
            if ((change == InputActionChange.ActionDisabled && ReferenceEquals(changed, action))
                || (change == InputActionChange.ActionMapDisabled && ReferenceEquals(changed, action.actionMap)))
                pending = false;
        }

        /// <summary>Consumes the frame's buffered event after target arbitration.</summary>
        internal void Clear()
        {
            // All candidates see the same event before this shared reset.
            pending = false;
        }

        #endregion

        #endregion
    }
}
