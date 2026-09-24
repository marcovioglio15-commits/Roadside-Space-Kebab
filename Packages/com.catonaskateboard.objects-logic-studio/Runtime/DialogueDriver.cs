using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Arbitrates a single HUD across eligible dialogues while preserving all player movement controls.</summary>
    internal sealed class DialogueDriver
    {
        #region Bindings

        /// <summary>Holds independently buffered activation and page-advance actions for one dialogue.</summary>
        private readonly struct Buttons
        {
            internal readonly InteractionButton Start;
            internal readonly InteractionButton Advance;

            /// <summary>Captures buttons already resolved against the player's private action asset.</summary>
            /// <param name="start">Optional input-activation button.</param>
            /// <param name="advance">Required page-advance button.</param>
            internal Buttons(InteractionButton start, InteractionButton advance)
            {
                // These references are shared within the driver's input router.
                Start = start;
                Advance = advance;
            }
        }

        #endregion

        #region State

        private readonly InteractionInputRouter input = new InteractionInputRouter();
        private readonly Dictionary<ObjectDialogue, Buttons> bindings = new Dictionary<ObjectDialogue, Buttons>();
        private ObjectDialogue active;
        private Transform player;
        private int revision = -1;
        private int inputRevision = -1;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Interrupts presentation and detaches input when observer context disappears.</summary>
        internal void Reset()
        {
            // Interrupt retains each component's configured resume/restart policy.
            if (active != null)
                active.Interrupt();
            active = null;
            input.Reset();
            bindings.Clear();
            player = null;
            revision = inputRevision = -1;
        }

        /// <summary>Rebuilds button associations only when the feature catalog or player asset changes.</summary>
        /// <param name="observer">Player context shared with single interactions and hover.</param>
        private void Bind(HoverObserver observer)
        {
            // Replacement players do not inherit presentation or buffered presses from the previous owner.
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
            {
                if (dialogue == null || !dialogue.Ready)
                    continue;
                InteractionButton advance = input.Bind(dialogue.AdvanceAction);
                InteractionButton start = dialogue.Settings.Trigger == DialogueTrigger.InputAction ? input.Bind(dialogue.StartAction) : null;
                if (advance == null || dialogue.Settings.Trigger == DialogueTrigger.InputAction && start == null)
                {
                    Debug.LogWarning("Dialogue needs its Button actions in the Observer player's active PlayerInput asset.", dialogue);
                    continue;
                }
                bindings.Add(dialogue, new Buttons(start, advance));
            }
        }

        #endregion

        #region Arbitration

        /// <summary>Advances the active dialogue or selects the highest-priority eligible request.</summary>
        /// <param name="observer">Current player and view context.</param>
        /// <returns>The action consumed by dialogue this frame, or null for proximity-only work.</returns>
        internal InputActionReference Tick(HoverObserver observer)
        {
            // The router never enables maps, changes cursor state or locks the controller.
            Bind(observer);
            input.Consume(AssemblyInteractionRegistry.Consumed);
            foreach (InputActionReference action in InteractionUnlockRegistry.Consumed)
                input.Consume(action);
            if (!input.Usable || Time.timeScale <= 0f)
            {
                if (active != null && !input.Usable)
                    active.Interrupt();
                input.ClearSignals();
                return null;
            }
            if (active != null && (!active.IsSpeaking || !active.CanContinue(player)
                || !bindings.TryGetValue(active, out Buttons current) || !current.Advance.Enabled))
            {
                active.Interrupt();
                active = null;
            }
            InputActionReference consumed = null;
            ObjectDialogue selected = null;
            // Observe all ranges even while another dialogue is speaking, so completed entries rearm on departure.
            foreach (KeyValuePair<ObjectDialogue, Buttons> pair in bindings)
                if (pair.Key != null && pair.Key.CanStart(player) && active == null && pair.Value.Advance.Enabled
                    && (pair.Key.Settings.Trigger != DialogueTrigger.InputAction || pair.Value.Start.Pending)
                    && (selected == null || pair.Key.Settings.Priority > selected.Settings.Priority
                        || pair.Key.Settings.Priority == selected.Settings.Priority
                        && EntityId.ToULong(pair.Key.GetEntityId()) < EntityId.ToULong(selected.GetEntityId())))
                    selected = pair.Key;
            if (active != null)
            {
                if (bindings[active].Advance.Pending)
                {
                    consumed = active.AdvanceAction;
                    active.Advance();
                }
            }
            else if (selected != null)
            {
                selected.Begin();
                if (selected.IsSpeaking)
                {
                    active = selected;
                    consumed = selected.Settings.Trigger == DialogueTrigger.InputAction ? selected.StartAction : null;
                }
            }
            input.ClearSignals();
            return consumed;
        }

        #endregion

        #endregion
    }
}
