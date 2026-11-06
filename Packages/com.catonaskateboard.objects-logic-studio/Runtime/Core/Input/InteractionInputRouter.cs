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
        private Guid toolActionId;
        private InteractionButton toolButton;

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
        /// <param name="reference">Configured Button action reference.</param>
        /// <returns>A shared buffered button, or null when the reference cannot be bound.</returns>
        internal InteractionButton Bind(InputActionReference reference)
        {
            // Lookup and subscriptions occur only at registry or input-context boundaries.
            return reference != null && reference.action != null ? Bind(reference.action.id) : null;
        }

        /// <summary>Reuses the same buffered button when the primary and tool routes share an action.</summary>
        /// <param name="identity">Stable ID from a configured action or captured tool contact.</param>
        /// <returns>The private Button, or null when this player cannot supply it.</returns>
        private InteractionButton Bind(Guid identity)
        {
            // Reuse any existing subscription before resolving the private action.
            if (Asset == null || identity == Guid.Empty)
                return null;
            if (Find(identity) is InteractionButton existing)
                return existing;
            InputAction action = Asset.FindAction(identity);
            if (action == null || action.type != InputActionType.Button)
                return null;
            InteractionButton button = new InteractionButton(action);
            buttons.Add(identity, button);
            return button;
        }

        /// <summary>Resolves the additional tool route once per input identity and binding lifetime.</summary>
        internal void PrepareToolAction()
        {
            // A projectile keeps the input it fired with even if the player changes tool before impact.
            Guid identity = InteractionProjectileInput.Active ? InteractionProjectileInput.ActionId : HoverObserver.ActiveTools?.UseActionId ?? Guid.Empty;
            if (identity == toolActionId)
                return;
            toolActionId = identity;
            toolButton = Bind(identity);
        }

        /// <summary>Selects the targeting press first, then the eligible Use Tool press for an explicitly restricted feature.</summary>
        /// <param name="feature">Interaction that can accept the current player or the captured hit.</param>
        /// <param name="targeting">Button resolved from this feature's configured action.</param>
        /// <param name="requested">Optional press that must share its action or physical control.</param>
        /// <param name="includePressed">Whether the caller can start on initial actuation before a Hold performs.</param>
        /// <returns>The exact button that supplied the input, or null when neither route can act.</returns>
        internal InteractionButton Read(ObjectInteraction feature, InteractionButton targeting, InteractionButton requested = null, bool includePressed = false)
        {
            // Read live signals before evaluating tool restrictions or the captured hit hierarchy.
            if (feature == null || targeting == null)
                return null;
            InteractionButton selected = targeting;
            if (!Matches(selected, requested, includePressed))
                selected = toolButton != targeting && Matches(toolButton, requested, includePressed)
                    && feature.ToolRequirement is { HasExplicitTools: true } ? toolButton : null;
            return selected != null && InteractionProjectileInput.Allows(feature) ? selected : null;
        }

        /// <summary>Checks whether a targeting or tool action can receive future presses.</summary>
        /// <param name="feature">Interaction whose tool requirement enables the additional route.</param>
        /// <param name="targeting">Configured Button in the private action asset.</param>
        /// <returns>True when at least one route is enabled.</returns>
        internal bool HasEnabledAction(ObjectInteraction feature, InteractionButton targeting)
        {
            // A disabled targeting map can still leave the eligible tool route available.
            return targeting is { Enabled: true } || targeting != null && toolButton is { Enabled: true }
                && feature.ToolRequirement is { HasExplicitTools: true } && feature.RequestToolAllowed;
        }

        /// <summary>Identifies an exact Use Tool binding during condition arbitration.</summary>
        /// <param name="button">Button that supplied the selected press.</param>
        /// <returns>True when it matches the current tool route.</returns>
        internal bool IsToolAction(InteractionButton button) => button == toolButton;

        /// <summary>Checks a buffered signal against a competing press when arbitration has one.</summary>
        /// <param name="button">Candidate input route.</param>
        /// <param name="requested">Optional action or physical control to match.</param>
        /// <param name="includePressed">Whether initial actuation can start a hold.</param>
        /// <returns>True when the button has an eligible signal.</returns>
        private static bool Matches(InteractionButton button, InteractionButton requested, bool includePressed)
        {
            return button != null && (button.Pending || includePressed && button.Pressed)
                && (requested == null || button.SharesPress(requested));
        }

        /// <summary>Reserves the exact action already used by an earlier driver.</summary>
        /// <param name="identity">Resolved action ID, or an empty ID when no input was consumed.</param>
        internal void Consume(Guid identity)
        {
            // Only this action is consumed, even when it shares a key with another action.
            if (identity != Guid.Empty)
                Find(identity)?.Clear();
        }

        /// <summary>Discards competing performed actions originating from the same physical control.</summary>
        /// <param name="consumed">Button already assigned to a dialogue or prioritized action.</param>
        internal void Consume(InteractionButton consumed)
        {
            if (consumed == null)
                return;
            // Device-control identity also covers independently authored actions sharing one key.
            foreach (InteractionButton button in buttons.Values)
                if ((button.Pending || button.Pressed) && button.SharesPress(consumed))
                    button.Clear();
        }

        /// <summary>Finds an already buffered action without subscribing to a new input.</summary>
        /// <param name="identity">Stable ID of the action selected during arbitration.</param>
        /// <returns>The existing button, or null when no interaction uses this action.</returns>
        internal InteractionButton Find(Guid identity)
        {
            return buttons.TryGetValue(identity, out InteractionButton button) ? button : null;
        }

        /// <summary>Finds the cached button for a configured action without subscribing to it.</summary>
        /// <param name="reference">Imported action reference.</param>
        /// <returns>The cached button, or null when the reference is unresolved.</returns>
        internal InteractionButton Find(InputActionReference reference)
        {
            return reference != null && reference.action != null ? Find(reference.action.id) : null;
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
            toolActionId = Guid.Empty;
            toolButton = null;
        }

        #endregion

        #endregion
    }
}
