using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Subscribes to existing menu actions without a per-frame input scan.</summary>
    public sealed class MenuInput : MonoBehaviour
    {
        #region Fields
        [Header("Preauthored Actions")]
        [Tooltip("Menu host controlled by these actions.")]
        public MenuHost Host;
        [Tooltip("Open or close a gameplay pause menu.")]
        public InputActionReference Pause;
        [Tooltip("Close the top menu overlay.")]
        public InputActionReference Cancel;
        [Tooltip("Select the previous settings tab.")]
        public InputActionReference PreviousTab;
        [Tooltip("Select the next settings tab.")]
        public InputActionReference NextTab;
        [Tooltip("Optional separate Credits close action.")]
        public InputActionReference CreditsClose;
        private readonly HashSet<InputAction> acquired = new HashSet<InputAction>();
        private static readonly Dictionary<InputAction, int> leases = new Dictionary<InputAction, int>();
        private static readonly HashSet<InputAction> enabledHere = new HashSet<InputAction>();
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Clears action ownership for Play mode sessions without domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLeases()
        {
            // Scene components acquire fresh leases when their next session begins.
            leases.Clear();
            enabledHere.Clear();
        }

        /// <summary>Enables and subscribes to the generated or overridden action references.</summary>
        private void OnEnable()
        {
            // Shared actions remain enabled until their final menu subscriber releases them.
            foreach (InputActionReference reference in new[] { Pause, Cancel, PreviousTab, NextTab, CreditsClose })
                if (reference != null && reference.action != null && acquired.Add(reference.action))
                {
                    InputAction action = reference.action;
                    leases.TryGetValue(action, out int count);
                    leases[action] = count + 1;
                    if (count == 0 && !action.enabled)
                    {
                        enabledHere.Add(action);
                        action.Enable();
                    }
                    action.performed += Handle;
                }
        }

        /// <summary>Releases subscriptions and only the actions enabled by this menu system.</summary>
        private void OnDisable()
        {
            // Do not disable actions that were already owned by project input code.
            foreach (InputAction action in acquired)
            {
                action.performed -= Handle;
                if (!leases.TryGetValue(action, out int count))
                    continue;
                if (count > 1)
                    leases[action] = count - 1;
                else
                {
                    leases.Remove(action);
                    if (enabledHere.Remove(action))
                        action.Disable();
                }
            }
            acquired.Clear();
        }
        #endregion

        #region Routing
        /// <summary>Routes input only to currently relevant menu operations.</summary>
        /// <param name="context">Performed action context.</param>
        private void Handle(InputAction.CallbackContext context)
        {
            // A shared Pause/Cancel action uses the host's same-frame guard.
            if (Host == null || Host.Preset == null || !Host.Preset.Navigation.Enabled)
                return;
            if (Pause != null && context.action == Pause.action)
                Host.TogglePause();
            else if (Cancel != null && context.action == Cancel.action)
                Host.Back();
            else if (CreditsClose != null && context.action == CreditsClose.action && Host.CurrentPage == MenuPageKind.Credits)
                Host.Back();
            else if (Host.Visible && Host.CurrentPage == MenuPageKind.Settings && Host.Settings != null)
            {
                if (PreviousTab != null && context.action == PreviousTab.action)
                    Host.Settings.ChangeTab(-1);
                else if (NextTab != null && context.action == NextTab.action)
                    Host.Settings.ChangeTab(1);
            }
        }
        #endregion
        #endregion
    }
}
