using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Arbitrates visible pages while waiting tree cursors leave the shared HUD free.</summary>
    internal sealed class DialogueDriver
    {
        #region Types

        /// <summary>Retains already resolved buttons for one complete conversation sheet.</summary>
        private sealed class Buttons
        {
            internal readonly Dictionary<InputActionReference, InteractionButton> Starts = new Dictionary<InputActionReference, InteractionButton>();
            internal readonly InteractionButton Advance;

            /// <summary>Binds the selected spawn tree once against private player input.</summary>
            /// <param name="dialogue">Dialogue containing row trigger references.</param>
            /// <param name="input">Observer-owned input router.</param>
            internal Buttons(ObjectDialogue dialogue, InteractionInputRouter input)
            {
                Advance = input.Bind(dialogue.AdvanceAction);
                dialogue.BindStarts(input, Starts);
            }
        }

        #endregion

        #region State

        private readonly InteractionInputRouter input = new InteractionInputRouter();
        private readonly DialogueVisibility visibility = new DialogueVisibility();
        private readonly Dictionary<ObjectDialogue, Buttons> bindings = new Dictionary<ObjectDialogue, Buttons>();
        private ObjectDialogue active;
        private Transform player;
        private int revision = -1;
        private int inputRevision = -1;
        private bool missingHudReported;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Releases presentation and player-specific subscriptions when observer context ends.</summary>
        internal void Reset()
        {
            if (active != null)
                active.Interrupt();
            active = null;
            input.Reset();
            visibility.Reset();
            bindings.Clear();
            player = null;
            revision = inputRevision = -1;
            missingHudReported = false;
        }

        /// <summary>Rebuilds action associations only at registry or player-input changes.</summary>
        /// <param name="observer">Current player and view context.</param>
        private void Bind(HoverObserver observer)
        {
            if (player != observer.Player)
            {
                Reset();
                player = observer.Player;
            }
            input.Refresh(player);
            if (revision == DialogueRegistry.Revision && inputRevision == input.Revision)
                return;
            input.ClearBindings();
            bindings.Clear();
            revision = DialogueRegistry.Revision;
            inputRevision = input.Revision;
            foreach (ObjectDialogue dialogue in DialogueRegistry.Items)
                if (dialogue != null && dialogue.Ready)
                {
                    Buttons buttons = new Buttons(dialogue, input);
                    if (buttons.Advance != null)
                        bindings.Add(dialogue, buttons);
                    else
                        Debug.LogWarning("Dialogue advance must be a Button in the Observer player's Input Actions asset.", dialogue);
                }
        }

        #endregion

        #region Arbitration

        /// <summary>Advances the current page or starts one matching row from the highest-priority tree.</summary>
        /// <param name="observer">Current camera, player and shared HUD.</param>
        /// <param name="singles">Single-action driver sharing physical input presses.</param>
        /// <returns>The action consumed this frame, or null for automatic work.</returns>
        internal InputActionReference Tick(HoverObserver observer, SingleInteractionDriver singles)
        {
            Bind(observer);
            if (InteractionProjectileInput.Active && active != null && !InteractionProjectileInput.Allows(active))
                return null;
            input.Consume(singles.AssemblyButton);
            input.Consume(AssemblyInteractionRegistry.Consumed);
            foreach (InputActionReference action in InteractionUnlockRegistry.Consumed)
                input.Consume(action);
            bool ready = observer.DialogueHud != null && observer.DialogueHud.IsOwnedBy(observer);
            if (!ready && bindings.Count > 0 && !missingHudReported)
            {
                Debug.LogWarning("Assign an active saved Dialogue HUD in Objects Logic Studio > Scene Observer.", observer);
                missingHudReported = true;
            }
            if (ready)
                missingHudReported = false;
            if (!input.Usable || !ready || Time.timeScale <= 0f)
            {
                if (active != null && (!input.Usable || !ready))
                    active.Interrupt();
                input.ClearSignals();
                return null;
            }
            if (active != null && (!active.IsSpeaking || !active.CanContinue(player) || !active.CanPresent(observer, visibility, true)
                || !bindings.TryGetValue(active, out Buttons current) || !current.Advance.Enabled))
            {
                active.Interrupt();
                active = null;
            }
            ObjectDialogue selected = null;
            // Observe ranges while another page is visible, but choose at most one waiting tree.
            foreach (KeyValuePair<ObjectDialogue, Buttons> pair in bindings)
                if (pair.Key != null && InteractionProjectileInput.Allows(pair.Key) && pair.Key.CanStart(player, pair.Value.Starts, observer, visibility)
                    && active == null && pair.Value.Advance.Enabled
                    && (selected == null || pair.Key.Presentation.Priority > selected.Presentation.Priority
                        || pair.Key.Presentation.Priority == selected.Presentation.Priority
                        && EntityId.ToULong(pair.Key.GetEntityId()) < EntityId.ToULong(selected.GetEntityId()))
                    && pair.Key.CanPresent(observer, visibility, false))
                    selected = pair.Key;
            InputActionReference consumed = null;
            if (active != null && bindings[active].Advance.Pending)
            {
                consumed = active.AdvanceAction;
                if ((!active.Presentation.PreferSingleActions || !singles.TryPrioritize(observer, bindings[active].Advance, active.Presentation.PreferredActions))
                    && active.CanAdvance(observer, visibility))
                {
                    InteractionProjectileInput.Confirm(active);
                    active.Advance();
                }
            }
            else if (active == null && selected != null)
            {
                selected.PrepareRequest();
                InputActionReference requested = selected.RequestedAction;
                bool preferred = requested != null && selected.Presentation.PreferSingleActions
                    && bindings[selected].Starts.TryGetValue(requested, out InteractionButton start)
                    && singles.TryPrioritize(observer, start, selected.Presentation.PreferredActions);
                if (preferred)
                    consumed = requested;
                else
                {
                    if (selected.Begin(observer.DialogueHud))
                        consumed = requested;
                    if (selected.IsSpeaking)
                        active = selected;
                }
            }
            if (consumed != null)
                singles.Suppress(input.Find(consumed));
            input.ClearSignals();
            return consumed;
        }

        #endregion

        #endregion
    }
}
