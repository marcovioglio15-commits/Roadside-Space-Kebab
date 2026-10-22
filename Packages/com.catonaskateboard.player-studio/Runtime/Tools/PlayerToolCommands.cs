using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Owns tool input subscriptions without enabling or disabling actions owned by PlayerInput.</summary>
    internal sealed class PlayerToolCommands : IDisposable
    {
        #region State

        private readonly PlayerTools owner;
        private readonly PlayerInput input;
        private readonly PlayerToolInputSettings settings;
        private readonly bool switching;
        private InputActionAsset bound;
        private InputAction[] selection = Array.Empty<InputAction>();
        private InputAction activate;
        private InputAction reload;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Retains the selected input roles and player-local owner.</summary>
        /// <param name="tools">Runtime module receiving commands.</param>
        /// <param name="playerInput">Optional private input owner.</param>
        /// <param name="roles">Optional input preset roles.</param>
        /// <param name="allowSwitching">False for single-tool players.</param>
        internal PlayerToolCommands(PlayerTools tools, PlayerInput playerInput, PlayerToolInputSettings roles, bool allowSwitching)
        {
            owner = tools;
            input = playerInput;
            settings = roles;
            switching = allowSwitching;
            Refresh();
        }

        /// <summary>Rebinds only when this player's active input asset changes.</summary>
        internal void Refresh()
        {
            InputActionAsset asset = input != null && input.isActiveAndEnabled && input.inputIsActive ? input.actions : null;
            if (asset == bound)
                return;
            Dispose();
            bound = asset;
            if (asset == null || settings == null)
                return;
            // Selection subscriptions are entirely absent in single-tool mode.
            selection = switching ? new InputAction[settings.Mode == PlayerToolInputMode.SharedCycle ? 1 : settings.Bindings.Length]
                : Array.Empty<InputAction>();
            for (int index = 0; index < selection.Length; index++)
                selection[index] = Subscribe(settings.Mode == PlayerToolInputMode.SharedCycle ? settings.UseTool : settings.Bindings[index].Action);
            activate = Subscribe(settings.Activate);
            reload = Subscribe(settings.Reload);
        }

        /// <summary>Releases all private callbacks before the owner stops or rebinds.</summary>
        public void Dispose()
        {
            foreach (InputAction action in selection)
                if (action != null)
                    action.performed -= Performed;
            if (activate != null)
                activate.performed -= Performed;
            if (reload != null)
                reload.performed -= Performed;
            selection = Array.Empty<InputAction>();
            activate = reload = null;
            bound = null;
        }

        /// <summary>Resolves an imported action ID into the current player's own action instance.</summary>
        /// <param name="reference">Optional imported Button role.</param>
        /// <returns>Subscribed private action, or null when unassigned or absent.</returns>
        private InputAction Subscribe(InputActionReference reference)
        {
            InputAction action = reference != null ? bound.FindAction(reference.action.id) : null;
            if (action != null)
                action.performed += Performed;
            return action;
        }

        #endregion

        #region Routing

        /// <summary>Routes accepted Button events without buffering input while paused or deactivated.</summary>
        /// <param name="context">Callback from the private action asset.</param>
        private void Performed(InputAction.CallbackContext context)
        {
            if (!owner.IsReady || !owner.isActiveAndEnabled || Time.timeScale <= 0f || !Application.isFocused
                || input == null || !input.isActiveAndEnabled || !input.inputIsActive || input.actions != bound)
                return;
            // Activation and reload have separate identities from all selection commands.
            if (context.action == activate)
                owner.Use();
            else if (context.action == reload)
                owner.Reload();
            else if (settings.Mode == PlayerToolInputMode.SharedCycle)
                owner.Cycle();
            else
                for (int index = 0; index < selection.Length; index++)
                    if (selection[index] == context.action)
                    {
                        owner.Select(settings.Bindings[index].Tool);
                        break;
                    }
        }

        #endregion

        #endregion
    }
}
