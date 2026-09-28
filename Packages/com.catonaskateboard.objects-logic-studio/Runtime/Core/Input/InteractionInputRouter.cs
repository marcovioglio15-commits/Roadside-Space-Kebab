using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares PlayerInput discovery and performed-button buffering across interaction drivers.</summary>
    internal sealed class InteractionInputRouter
    {
        #region State

        private readonly Dictionary<Guid, InteractionButton> buttons = new Dictionary<Guid, InteractionButton>();
        private PlayerInput input;
        private Transform player;
        private float nextSearch;

        #endregion

        #region Properties

        /// <summary>Private runtime action asset currently owned by the selected player.</summary>
        internal InputActionAsset Asset { get; private set; }
        /// <summary>Changes when the player or runtime action asset changes.</summary>
        internal int Revision { get; private set; }
        /// <summary>Whether input dispatch is currently allowed without enabling any action maps.</summary>
        internal bool Usable => input != null && input.isActiveAndEnabled && input.inputIsActive && Asset != null;

        #endregion

        #region Methods

        #region Context

        /// <summary>Rebinds only when ownership changes or a missing PlayerInput is retried.</summary>
        /// <param name="root">Player root selected by the observer.</param>
        internal void Refresh(Transform root)
        {
            // PlayerInput owns its action maps and device pairing throughout this router's lifetime.
            if (player != root)
            {
                Reset();
                player = root;
            }
            if (input == null && player != null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                input = player.GetComponentInChildren<PlayerInput>();
            }
            InputActionAsset current = input != null && input.isActiveAndEnabled ? input.actions : null;
            if (Asset == current)
                return;
            ClearBindings();
            Asset = current;
            Revision++;
        }

        /// <summary>Releases context and subscriptions after an observer loses its player.</summary>
        internal void Reset()
        {
            // Reset also prevents stale performed events from reaching a replacement player.
            ClearBindings();
            player = null;
            input = null;
            Asset = null;
            nextSearch = 0f;
            Revision++;
        }

        #endregion

        #region Buttons

        /// <summary>Resolves one imported reference against the current private runtime asset.</summary>
        /// <param name="reference">Authored Button action reference.</param>
        /// <returns>A shared buffered button, or null when the reference cannot be bound.</returns>
        internal InteractionButton Bind(InputActionReference reference)
        {
            // Lookup and subscriptions occur only at registry or input-context boundaries.
            if (Asset == null || reference == null || reference.action == null)
                return null;
            Guid identity = reference.action.id;
            if (buttons.TryGetValue(identity, out InteractionButton existing))
                return existing;
            InputAction action = Asset.FindAction(identity.ToString());
            if (action == null || action.type != InputActionType.Button)
                return null;
            InteractionButton button = new InteractionButton(action);
            buttons.Add(identity, button);
            return button;
        }

        /// <summary>Consumes one action already used to start or advance a dialogue.</summary>
        /// <param name="reference">Used authored action, or null when no input was consumed.</param>
        internal void Consume(InputActionReference reference)
        {
            // Other movement actions remain unaffected, even while a dialogue is visible.
            if (reference != null && reference.action != null && buttons.TryGetValue(reference.action.id, out InteractionButton button))
                button.Clear();
        }

        /// <summary>Discards all buffered presses after one arbitration pass.</summary>
        internal void ClearSignals()
        {
            // Ineligible presses must never become delayed interactions on a later frame.
            foreach (InteractionButton button in buttons.Values)
                button.Clear();
        }

        /// <summary>Detaches buffered buttons before rebuilding the active feature catalog.</summary>
        internal void ClearBindings()
        {
            // Disposal does not enable or disable the player's maps or individual actions.
            foreach (InteractionButton button in buttons.Values)
                button.Dispose();
            buttons.Clear();
        }

        #endregion

        #endregion
    }
}
