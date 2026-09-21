using System;
using UnityEditor;
using UnityEngine.InputSystem;
using CatOnASkateboard.StudioInput.Editor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Supplies Player Studio's role-specific filters to the shared map/action menu.</summary>
    internal static class PlayerInputActionMenu
    {
        #region Filters

        private static readonly Func<InputAction, bool> buttons = action => IsCompatible(action, "jumpAction");
        private static readonly Func<InputAction, bool> movement = action => IsCompatible(action, "movementAction");
        private static readonly Func<InputAction, bool> delta = action => IsCompatible(action, "lookDeltaAction");
        private static readonly Func<InputAction, bool> rate = action => IsCompatible(action, "lookRateAction");

        #endregion

        #region Methods

        #region Controls

        /// <summary>Lists imported actions compatible with one player input role.</summary>
        /// <param name="serialized">Preset or detached draft containing the action field.</param>
        /// <param name="name">Serialized input role.</param>
        internal static void Draw(SerializedObject serialized, string name)
        {
            // The shared menu caches asset discovery; compatibility is evaluated only on rebuilds.
            StudioInputActionMenu.Draw(serialized, name, name, name switch
            {
                "jumpAction" or "cursorToggleAction" => buttons,
                "movementAction" => movement,
                "lookDeltaAction" => delta,
                _ => rate
            });
        }

        /// <summary>Preserves button, movement and look-unit compatibility rules.</summary>
        /// <param name="action">Imported action configuration.</param>
        /// <param name="role">Requested player input field.</param>
        /// <returns>True when the action supplies the role's expected value and units.</returns>
        private static bool IsCompatible(InputAction action, string role)
        {
            // Binding inspection also supports controls on devices that are currently disconnected.
            if (role == "jumpAction" || role == "cursorToggleAction")
                return action.type == InputActionType.Button;
            if (action.type == InputActionType.Button || action.expectedControlType != "Vector2"
                || role == "movementAction" && action.type != InputActionType.Value)
                return false;
            return role == "movementAction" || PlayerLookActionCompatibility.IsCompatible(action, role == "lookDeltaAction");
        }

        #endregion

        #endregion
    }
}
