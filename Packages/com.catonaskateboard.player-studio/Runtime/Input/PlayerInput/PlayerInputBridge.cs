using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Caches movement and jump transitions from one assigned PlayerInput without owning its maps or devices.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player Studio/Player Input Bridge")]
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Connection")]
        [Tooltip("Player whose applied master supplies the Input preset. Reconnect explicitly after changing this assignment during Play.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Existing PlayerInput that owns the action instances, enabled maps and paired devices. Use Invoke C Sharp Events notification behavior.")]
        [SerializeField]
        private PlayerInput playerInput;

        #endregion

        #region Runtime State

        private readonly PlayerButtonBinding jumpBinding = new PlayerButtonBinding();
        private InputAction movementAction;
        private Vector2 movement;
        private bool hasStarted;
        private string connectionWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>The assigned player, used to reject a motor connected to another player's bridge.</summary>
        public PlayerHost Host => host;

        /// <summary>The last two-axis value, or zero while this connection or its action is inactive.</summary>
        public Vector2 Movement => IsConnected && movementAction.enabled ? movement : Vector2.zero;

        /// <summary>Rejects stale cached actions if the external owner replaces its action asset.</summary>
        public bool IsConnected => isActiveAndEnabled && playerInput != null && playerInput.isActiveAndEnabled
            && movementAction != null && movementAction.actionMap.asset == playerInput.actions;

        /// <summary>The most recent connection failure, available without logging every frame.</summary>
        public string ConnectionWarning => connectionWarning;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Waits until enabled PlayerInput components have initialized their action instances.</summary>
        private void Start()
        {
            // The first subscription belongs after OnEnable on the existing input owner.
            hasStarted = true;
            ConnectAndReport();
        }

        /// <summary>Restores subscriptions when an already started bridge is enabled again.</summary>
        private void OnEnable()
        {
            // First activation is deferred to Start; later activations reuse the same connection path.
            if (hasStarted)
                ConnectAndReport();
        }

        /// <summary>Releases only this bridge's callbacks and cached value.</summary>
        private void OnDisable()
        {
            // The owner remains free to serve other modules while this bridge is disabled.
            Disconnect();
        }

        #endregion

        #region Connection

        /// <summary>Connects at lifecycle boundaries and reports a configuration problem once per attempt.</summary>
        private void ConnectAndReport()
        {
            // A disabled map is valid; its owner decides when gameplay starts accepting input.
            if (!TryConnect(out string warning))
                Debug.LogWarning(warning, this);
        }

        /// <summary>Rebuilds this subscription after initialization or an explicit runtime reassignment.</summary>
        /// <param name="warning">Receives the missing owner, preset or action requirement.</param>
        /// <returns>True when the player's assigned action instance is connected.</returns>
        public bool TryConnect(out string warning)
        {
            // Drop previous callbacks before resolving a new owner or preset.
            Disconnect();
            if (!TryResolve(out movementAction, out warning) || !TryResolveJump(out InputAction jumpAction, out warning))
            {
                Disconnect();
                connectionWarning = warning;
                return false;
            }

            // One callback handles both changed values and cancellation; no Update polling is needed.
            jumpBinding.Bind(jumpAction);
            movementAction.performed += ReadMovement;
            movementAction.canceled += ReadMovement;
            movement = movementAction.enabled ? movementAction.ReadValue<Vector2>() : Vector2.zero;
            connectionWarning = string.Empty;
            return true;
        }

        /// <summary>Validates explicit ownership and resolves by ID in PlayerInput's current action asset.</summary>
        /// <param name="action">Receives the player's own action instance, never the preset reference's action.</param>
        /// <param name="warning">Receives the first connection incompatibility.</param>
        /// <returns>True when a compatible instance is available in Play mode.</returns>
        private bool TryResolve(out InputAction action, out string warning)
        {
            // Do not discover devices, create components or change map ownership implicitly.
            action = null;
            warning = string.Empty;
            if (!Application.isPlaying || !isActiveAndEnabled || playerInput == null || !playerInput.isActiveAndEnabled
                || playerInput.actions == null || host == null || host.MasterPreset == null || host.MasterPreset.InputPreset == null)
            {
                warning = "Connect an active PlayerInput and a Player Host with an Input preset during Play.";
                return false;
            }

            if (playerInput.notificationBehavior != PlayerNotifications.InvokeCSharpEvents)
            {
                warning = "Set PlayerInput Behavior to Invoke C Sharp Events to avoid name-based message dispatch.";
                return false;
            }

            if (!host.MasterPreset.InputPreset.TryGetMovementId(out System.Guid actionId, out warning))
                return false;

            // A map or action rename cannot redirect this lookup to a different action.
            InputAction candidate = playerInput.actions.FindAction(actionId);
            if (candidate == null || candidate.type != InputActionType.Value || candidate.expectedControlType != "Vector2")
            {
                warning = "The assigned PlayerInput must contain the preset's Value / Vector2 movement action ID.";
                return false;
            }

            action = candidate;
            return true;
        }

        /// <summary>Resolves the optional Button in the same player-owned asset as continuous movement.</summary>
        /// <param name="action">Receives the resolved Button, or null for an unassigned optional role.</param>
        /// <param name="warning">Receives an invalid mapping or missing player action warning.</param>
        /// <returns>True when the optional jump mapping is compatible with this PlayerInput.</returns>
        private bool TryResolveJump(out InputAction action, out string warning)
        {
            // Movement resolution already validated the owner, host and Input preset.
            action = null;
            if (!host.MasterPreset.InputPreset.TryGetJumpId(out System.Guid actionId, out warning))
                return false;
            if (actionId == System.Guid.Empty)
                return true;

            action = playerInput.actions.FindAction(actionId);
            if (action != null && action.type == InputActionType.Button)
                return true;

            warning = "The assigned PlayerInput must contain the preset's Button jump action ID.";
            return false;
        }

        /// <summary>Transfers jump edges to the motor once without enabling or disabling any action.</summary>
        /// <returns>The pending press, release and held state for this player's jump role.</returns>
        public PlayerButtonSignal ConsumeJump()
        {
            // The motor is the single consumer; diagnostics must not call this method.
            return jumpBinding.Consume(IsConnected, playerInput != null ? playerInput.actions : null);
        }

        /// <summary>Removes this subscriber without disabling another module's actions or destroying their owner.</summary>
        private void Disconnect()
        {
            // Unsubscribe before forgetting the cached instance, including when its asset was replaced.
            if (movementAction != null)
            {
                movementAction.performed -= ReadMovement;
                movementAction.canceled -= ReadMovement;
            }

            jumpBinding.Bind(null);
            movementAction = null;
            movement = Vector2.zero;
        }

        #endregion

        #region Input Capture

        /// <summary>Copies only the current value; the callback context never escapes its valid lifetime.</summary>
        /// <param name="context">Immediate notification from this player's assigned action.</param>
        private void ReadMovement(InputAction.CallbackContext context)
        {
            // Cancel clears held input when the owner disables a map or a control returns to rest.
            movement = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
        }

        #endregion

        #endregion
    }
}
