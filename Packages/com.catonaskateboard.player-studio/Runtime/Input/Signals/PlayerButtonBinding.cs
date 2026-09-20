using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Latches button edges from one action instance without owning its map or enabled state.</summary>
    internal sealed class PlayerButtonBinding
    {
        #region State

        private InputAction action;
        private bool pressed;
        private bool released;
        private bool held;
        private bool interrupted;

        #endregion

        #region Methods

        #region Connection

        /// <summary>Replaces the subscriber and starts with no pending request.</summary>
        /// <param name="source">The player's own optional Button action, or null to disconnect.</param>
        public void Bind(InputAction source)
        {
            // Unsubscribe before replacing an instance; map ownership always stays with PlayerInput.
            if (action != null)
            {
                action.started -= Read;
                action.performed -= Read;
                action.canceled -= Read;
                InputSystem.onActionChange -= HandleActionChange;
            }
            action = source;
            Clear();
            if (action == null)
                return;

            // Capture current held state without fabricating a press at reconnection.
            held = action.enabled && action.IsPressed();
            action.started += Read;
            action.performed += Read;
            action.canceled += Read;
            InputSystem.onActionChange += HandleActionChange;
        }

        #endregion

        #region Consumption

        /// <summary>Returns both edges once, including a complete tap between motor updates.</summary>
        /// <param name="ownerActive">Whether the bridge and its PlayerInput still own an active connection.</param>
        /// <param name="asset">Current player action instance, used to reject a stale reference.</param>
        /// <returns>Pending edges and held state, or an empty signal when the connection is inactive.</returns>
        public PlayerButtonSignal Consume(bool ownerActive, InputActionAsset asset)
        {
            // Lost ownership invalidates queued presses rather than replaying them on another player.
            if (!ownerActive || action == null || !action.enabled || action.actionMap.asset != asset)
            {
                Clear();
                return default;
            }

            PlayerButtonSignal signal = new PlayerButtonSignal(pressed, released, held, interrupted);
            pressed = released = interrupted = false;
            return signal;
        }

        /// <summary>Removes queued edges and the held value at an ownership boundary.</summary>
        private void Clear()
        {
            // A disconnected input must not retain an earlier press.
            pressed = released = held = false;
            interrupted = true;
        }

        #endregion

        #region Events

        /// <summary>Captures threshold transitions; holding never repeats a press by itself.</summary>
        /// <param name="context">Immediate action callback; only its pressed state is retained.</param>
        private void Read(InputAction.CallbackContext context)
        {
            // Started and performed share the same edge guard, so one physical press is latched once.
            bool isPressed = !context.canceled && context.action.IsPressed();
            pressed |= isPressed && !held;
            released |= !isPressed && held;
            held = isPressed;
        }

        /// <summary>Clears a queued press even if a map is disabled and re-enabled between motor ticks.</summary>
        /// <param name="changed">Action or map whose enabled state changed.</param>
        /// <param name="change">Input System notification received after the ownership change.</param>
        private void HandleActionChange(object changed, InputActionChange change)
        {
            // A release keeps a short tap; a disabled action must instead invalidate the entire request.
            if ((change == InputActionChange.ActionDisabled && changed == action)
                || (change == InputActionChange.ActionMapDisabled && changed == action.actionMap))
                Clear();
        }

        #endregion

        #endregion
    }
}
