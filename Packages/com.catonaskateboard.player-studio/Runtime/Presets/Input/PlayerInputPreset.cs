using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Maps a gameplay input role to an assigned action, independently of action and binding names.</summary>
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "Player Studio/Input Preset")]
    public sealed class PlayerInputPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Movement")]
        [Tooltip("Value action with Vector2 control type. Its ID is resolved in the assigned PlayerInput's actions; names and bindings remain yours.")]
        [SerializeField]
        private InputActionReference movementAction;

        [Header("Jump")]
        [Tooltip("Optional Button action resolved by ID in PlayerInput. Use a standard press binding; leave empty when jumping is disabled.")]
        [SerializeField]
        private InputActionReference jumpAction;

        [Header("Camera")]
        [Tooltip("Optional Vector2 delta action, typically pointer movement. Resolved by ID in this player's action asset.")]
        [SerializeField]
        private InputActionReference lookDeltaAction;

        [Tooltip("Optional Vector2 rate action, typically a stick. Its value is integrated using elapsed time.")]
        [SerializeField]
        private InputActionReference lookRateAction;

        [Tooltip("Optional Button action that toggles camera cursor capture. No command or binding is assumed.")]
        [SerializeField]
        private InputActionReference cursorToggleAction;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>Reads the configured action's identity without enabling or reading its shared asset instance.</summary>
        /// <param name="actionId">Receives the stable action ID used to find the player's own instance.</param>
        /// <param name="warning">Receives a missing-reference or incompatible action-type warning.</param>
        /// <returns>True when the assigned action describes a continuous two-axis value.</returns>
        public bool TryGetMovementId(out Guid actionId, out string warning)
        {
            // This reference supplies configuration only, never a player's current input value.
            InputAction action = movementAction != null ? movementAction.action : null;
            actionId = Guid.Empty;
            warning = string.Empty;
            if (action == null || action.type != InputActionType.Value || action.expectedControlType != "Vector2")
            {
                warning = "Assign a movement action with Action Type Value and Control Type Vector2.";
                return false;
            }

            // Renaming an action or its map leaves its serialized ID unchanged.
            actionId = action.id;
            return true;
        }

        /// <summary>Reads the optional jump identity without changing action ownership.</summary>
        /// <param name="actionId">Receives the action ID, or Guid.Empty when no jump is assigned.</param>
        /// <param name="warning">Receives a warning for an assigned action with the wrong type.</param>
        /// <returns>True for an empty optional reference or a Button action.</returns>
        public bool TryGetJumpId(out Guid actionId, out string warning)
        {
            // Missing jump input is valid until a motor explicitly enables jumping.
            actionId = Guid.Empty;
            warning = string.Empty;
            if (jumpAction == null)
                return true;
            if (jumpAction.action == null || jumpAction.action.type != InputActionType.Button)
            {
                warning = "Assign a Jump action with Action Type Button, or leave the optional reference empty.";
                return false;
            }

            actionId = jumpAction.action.id;
            return true;
        }

        /// <summary>Resolves optional view input identities without reading shared asset values.</summary>
        /// <param name="delta">Receives the displacement action ID, or empty when unassigned.</param>
        /// <param name="rate">Receives the rate action ID, or empty when unassigned.</param>
        /// <param name="toggle">Receives the cursor Button ID, or empty when unassigned.</param>
        /// <param name="warning">Receives an incompatible assigned action warning.</param>
        /// <returns>True when all assigned camera roles use compatible controls.</returns>
        public bool TryGetLookIds(out Guid delta, out Guid rate, out Guid toggle, out string warning)
        {
            // Role names describe configuration fields, never action names to search for.
            delta = Guid.Empty;
            rate = Guid.Empty;
            toggle = Guid.Empty;
            if (!TryGetOptionalId(lookDeltaAction, false, out delta, out warning)
                || !TryGetOptionalId(lookRateAction, false, out rate, out warning))
                return false;
            // Reject mixed units and duplicate roles before input callbacks can reach camera integration.
            if (lookDeltaAction != null && !PlayerLookActionCompatibility.IsCompatible(lookDeltaAction.action, true)
                || lookRateAction != null && !PlayerLookActionCompatibility.IsCompatible(lookRateAction.action, false)
                || delta != Guid.Empty && delta == rate)
            {
                warning = "Use separate Look Delta actions with pointer delta bindings and Look Rate actions with continuous bindings such as a stick.";
                return false;
            }
            return TryGetOptionalId(cursorToggleAction, true, out toggle, out warning);
        }

        /// <summary>Validates an optional camera reference without enabling its action.</summary>
        /// <param name="reference">Reference configured in the Input preset.</param>
        /// <param name="button">Whether a Button is required instead of a Vector2 value.</param>
        /// <param name="id">Receives the configured ID or empty.</param>
        /// <param name="warning">Receives a missing or incompatible action warning.</param>
        /// <returns>True for an empty role or a compatible assigned action.</returns>
        private static bool TryGetOptionalId(InputActionReference reference, bool button, out Guid id, out string warning)
        {
            // An unassigned optional role is valid until a camera specifically requires look input.
            id = Guid.Empty;
            warning = string.Empty;
            if (reference == null)
                return true;
            InputAction action = reference.action;
            if (action == null || (button ? action.type != InputActionType.Button
                : action.type == InputActionType.Button || action.expectedControlType != "Vector2"))
            {
                warning = button ? "Cursor Toggle requires a Button action." : "Look actions require a Vector2 Value or Pass Through action.";
                return false;
            }
            id = action.id;
            return true;
        }

        #endregion

        #endregion
    }
}
