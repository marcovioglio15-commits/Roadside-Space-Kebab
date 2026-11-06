using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs one spawn-selected conversation sheet through the observer's shared dialogue channel.</summary>
    [AddComponentMenu("Objects Logic Studio/Dialogue")]
    public sealed class ObjectDialogue : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Dialogue")]
        [Tooltip("Complete tree presets, visibility, priority, replay and audio.")]
        [SerializeField]
        private DialogueSettings settings = new DialogueSettings();
        [Header("Input")]
        [Tooltip("Default Button used by Input Action trigger presets without an override.")]
        [SerializeField]
        private InputActionReference startAction;
        [Tooltip("Button advancing explicit pages and closing the final page.")]
        [SerializeField]
        private InputActionReference advanceAction;

        #endregion

        #region Stored Progress

        /// <summary>Retains graph progress while a Container suspends this item's components.</summary>
        internal readonly struct StoredProgress
        {
            internal readonly DialogueSession Session;
            internal readonly bool Armed;
            internal readonly bool Retained;
            internal readonly DialoguePolicy Presentation;

            /// <summary>Captures the cursor after the interruption policy has been applied.</summary>
            /// <param name="dialogue">Dialogue entering storage.</param>
            internal StoredProgress(ObjectDialogue dialogue)
            {
                Session = dialogue.session;
                Armed = dialogue.armed;
                Retained = dialogue.retained;
                Presentation = dialogue.Presentation;
            }
        }

        #endregion

        #region State

        private readonly List<int> eligible = new List<int>();
        private readonly HashSet<DialogueTriggerPreset> consumptionMatches = new HashSet<DialogueTriggerPreset>();
        private readonly DialogueAudioRun audio = new DialogueAudioRun();
        internal readonly DialogueSightGeometry SightGeometry = new DialogueSightGeometry();
        private ObjectAvailableOrders orders;
        private ObjectContactModifier contact;
        private DialogueSession session;
        private DialogueTree content;
        private DialoguePolicy presentation;
        private DialogueHud hud;
        private int generation = -1;
        private int pending = -1;
        private int receiptRevision = -1;
        private int contactRevision = -1;
        private int orderRevision = -1;
        private bool armed = true;
        private bool retained;
        private bool awaitingSight;
        private bool arrivalOnly;
        private bool arrivalPending;
        private bool arrivalSession;
        private bool projectileStarted;
        private bool ignoreExitRange;

        #endregion

        #region Properties

        /// <summary>Local presentation policy and full tree preset references.</summary>
        public DialogueSettings Settings => settings;
        /// <summary>Policy selected by the requested or active conversation branch.</summary>
        internal DialoguePolicy Presentation => presentation ?? settings;
        /// <summary>Trigger that claimed the current request, including a routing entry.</summary>
        private DialogueTriggerPreset RequestTrigger => session.RouteRow?.Trigger ?? (retained ? session.ActivationTrigger : session.RequestBlock.Entries[pending].Trigger);
        /// <summary>Arrival range and cursor bypass remains limited to its configured branch.</summary>
        private bool ArrivalContext => (arrivalPending || arrivalSession) && (string.IsNullOrEmpty(content.ArrivalBlock)
            || (session.Route ?? session.Block).Id == content.ArrivalBlock);
        /// <summary>Default Input Action trigger binding.</summary>
        public InputActionReference StartAction => startAction;
        /// <summary>Page advance binding.</summary>
        public InputActionReference AdvanceAction => advanceAction;
        /// <summary>Whether this component currently owns a visible page.</summary>
        public bool IsSpeaking { get; private set; }
        /// <summary>One-based occurrence shared with Available Orders.</summary>
        public int SpawnIndex { get; private set; }
        /// <summary>Current block key for tree diagnostics.</summary>
        public string CurrentBlock => session?.Block.Id;
        /// <summary>Current entry row key for tree diagnostics.</summary>
        public string CurrentEntry => session?.Row?.Id;
        /// <summary>Block published only during a step completion signal.</summary>
        public string CompletedBlock { get; private set; }
        /// <summary>Entry published only during a step completion signal.</summary>
        public string CompletedEntry { get; private set; }
        /// <summary>Whether activation validation passed.</summary>
        internal bool Ready { get; private set; }
        /// <summary>Whether Day Flow requests the first block for this clone.</summary>
        internal bool ArrivalOnly => arrivalOnly;
        /// <summary>Input selected for the pending row, or null for an automatic trigger.</summary>
        internal InputActionReference RequestedAction => pending >= 0 && RequestTrigger.Type == DialogueTrigger.InputAction
            && !arrivalPending ? ActionFor(RequestTrigger) : null;
        /// <summary>Identifies the dialogue feature card.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Dialogue;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Validates independent progress before registering for observer arbitration.</summary>
        private void OnEnable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            Initialize();
            DialogueRegistry.Register(this);
        }

        /// <summary>Withdraws visible presentation before removing input bindings.</summary>
        private void OnDisable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            Interrupt();
            DialogueRegistry.Unregister(this);
        }

        /// <summary>Resets transient state for activation and retained-scene Play entry.</summary>
        internal void Initialize()
        {
            if (IsSpeaking && hud != null)
                hud.Hide();
            hud = null;
            presentation = settings;
            IsSpeaking = retained = awaitingSight = false;
            armed = true;
            pending = -1;
            receiptRevision = contactRevision = orderRevision = -1;
            orders = GetComponent<ObjectAvailableOrders>();
            contact = GetComponent<ObjectContactModifier>();
            audio.Stop();
            SightGeometry.Bind(transform);
            PrepareSpawn();
            Ready = TryValidate(out string warning);
            session = Ready ? new DialogueSession(content, Item.ConsumptionRevision, contact != null ? contact.ContactRevision : -1) : null;
            if (!Ready)
                Debug.LogWarning("Dialogue '" + InteractionName + "': " + warning, this);
        }

        /// <summary>Selects the complete tree once for the current prefab occurrence.</summary>
        internal void PrepareSpawn()
        {
            if (Item == null)
                return;
            Item.PrepareSpawn(ObjectSpawnHistory.Source, ObjectSpawnHistory.Occurrence);
            if (generation == ObjectSpawnHistory.Generation && SpawnIndex == Item.Spawn.Index && content != null)
                return;
            generation = ObjectSpawnHistory.Generation;
            SpawnIndex = Item.Spawn.Index;
            content = settings?.ResolveTree(SpawnIndex);
        }

        /// <summary>Ticks voice intervals only while this dialogue presents a page.</summary>
        private void Update()
        {
            if (IsSpeaking && Time.timeScale > 0f)
                audio.Tick(Presentation.Audio, transform);
        }

        #endregion

        #region Validation And Input

        /// <summary>Configures local action identifiers before a new runtime dialogue is enabled.</summary>
        /// <param name="start">Default activation Button.</param>
        /// <param name="advance">Page advance Button.</param>
        internal void BindActions(InputActionReference start, InputActionReference advance)
        {
            startAction = start;
            advanceAction = advance;
        }

        /// <summary>Revalidates changed settings, preserving pages when the selected tree is unchanged.</summary>
        internal void RefreshRuntime()
        {
            DialogueTree next = settings.ResolveTree(SpawnIndex);
            if (next != content)
            {
                Interrupt();
                content = next;
                session = new DialogueSession(content, Item.ConsumptionRevision, contact != null ? contact.ContactRevision : -1);
                retained = false;
                armed = true;
            }
            Ready = TryValidate(out string warning);
            if (!Ready)
                Debug.LogWarning(warning, this);
            receiptRevision = -1;
            DialogueRegistry.Invalidate();
        }

        /// <summary>Checks the saved component and all reachable tree presets.</summary>
        /// <param name="warning">Receives the first missing dependency.</param>
        /// <returns>True when the observer can run this dialogue.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, startAction, advanceAction, out warning);
        }

        /// <summary>Checks a detached configuration before Apply or runtime replacement.</summary>
        /// <param name="configuration">Proposed tree and presentation settings.</param>
        /// <param name="start">Default trigger action.</param>
        /// <param name="advance">Page advance action.</param>
        /// <param name="warning">Receives an invalid tree or binding.</param>
        /// <returns>True for a complete proposal.</returns>
        public bool TryValidate(DialogueSettings configuration, InputActionReference start, InputActionReference advance, out string warning)
        {
            warning = "Dialogue needs Object Item, a complete tree and an advance Button.";
            if (GetComponent<ObjectItem>() == null || configuration == null
                || !configuration.TryValidate(out warning, GetComponent<ObjectAvailableOrders>()?.Settings, true))
                return false;
            if (advance == null || advance.action is not { type: InputActionType.Button })
            {
                warning = "Assign an advance Button action.";
                return false;
            }
            foreach (DialogueTree tree in Trees(configuration))
                foreach (DialogueNode block in tree.Nodes)
                    foreach (DialogueRow row in block.Entries)
                        if ((!arrivalOnly || block.Id != tree.Start) && row.Trigger.Type == DialogueTrigger.InputAction && row.Trigger.Action == null
                            && (start == null || start.action is not { type: InputActionType.Button }))
                        {
                            warning = "Assign a default start Button or an action on every Input Action trigger.";
                            return false;
                        }
            warning = string.Empty;
            return true;
        }

        /// <summary>Enumerates all configured sheets for destination-aware validation.</summary>
        /// <param name="configuration">Default and spawn-specific sheet selection.</param>
        /// <returns>Each sheet that can be selected at spawn.</returns>
        private static IEnumerable<DialogueTree> Trees(DialogueSettings configuration)
        {
            yield return configuration.Tree;
            if (configuration.UseSpawnTrees)
                foreach (DialogueSpawnTree rule in configuration.SpawnTrees)
                    yield return rule.Tree;
        }

        /// <summary>Binds actions once when the observer's registry or private input asset changes.</summary>
        /// <param name="router">Observer-owned action router.</param>
        /// <param name="buttons">Per-dialogue action lookup filled by this call.</param>
        internal void BindStarts(InteractionInputRouter router, Dictionary<InputActionReference, InteractionButton> buttons)
        {
            foreach (DialogueNode block in content.Nodes)
                foreach (DialogueRow row in block.Entries)
                    if (row.Trigger.Type == DialogueTrigger.InputAction)
                    {
                        InputActionReference action = ActionFor(row.Trigger);
                        if (action != null && !buttons.ContainsKey(action) && router.Bind(action) is InteractionButton button)
                            buttons.Add(action, button);
                    }
        }

        /// <summary>Resolves a trigger override against this object's local default action.</summary>
        /// <param name="trigger">Input Action trigger.</param>
        /// <returns>The selected project action identifier.</returns>
        private InputActionReference ActionFor(DialogueTriggerPreset trigger)
        {
            return trigger.Action != null ? trigger.Action : startAction;
        }

        #endregion

        #region Eligibility

        /// <summary>Collects eligible text rows or selects one triggered conversation branch.</summary>
        /// <param name="player">Current player root.</param>
        /// <param name="buttons">Already resolved input buttons.</param>
        /// <param name="input">Router for configured triggers and accepted tool presses.</param>
        /// <param name="observer">Optional view used while choosing routing branches.</param>
        /// <param name="visibility">Observer-owned visibility cache.</param>
        /// <returns>True when this component may claim the idle dialogue channel.</returns>
        internal bool CanStart(Transform player, Dictionary<InputActionReference, InteractionButton> buttons, InteractionInputRouter input,
            HoverObserver observer = null, DialogueVisibility visibility = null)
        {
            if (!Ready || session == null || IsSpeaking || !Available(InteractionChannels.Dialogue)
                || arrivalOnly && !arrivalSession && !arrivalPending)
                return false;
            float distance = (transform.position - player.position).sqrMagnitude;
            if (distance > Presentation.ExitDistance * Presentation.ExitDistance)
            {
                session.ProximityUsed.Clear();
                if (Presentation.ReplayOnReturn)
                    armed = true;
            }
            eligible.Clear();
            RefreshConsumption();
            session.Route = null;
            session.RouteRow = null;
            DialogueNode block = session.RequestBlock;
            bool routed = block.Routing && !retained;
            if (routed && !SelectRoute(block, distance, buttons, input, observer, visibility))
                return false;
            block = session.RequestBlock;
            SetPresentation(block.OverridePolicy ? block.Policy : settings);
            // Child blocks keep independent sequence cursors while the router owns their activation event.
            for (int index = 0; index < block.Entries.Length; index++)
                if ((!retained || session.Row == null || block.Entries[index] == session.Row)
                    && (block.Selection != DialogueSelection.WeightedRandom || block.Entries[index].Weight > 0f)
                    && Matches(block.Entries[index], Presentation, distance, buttons, input, !routed))
                    eligible.Add(index);
            return eligible.Count > 0;
        }

        /// <summary>Chooses the highest-priority matching route without displaying a routing page.</summary>
        /// <param name="router">Routing block containing trigger-only entries.</param>
        /// <param name="distance">Squared player distance.</param>
        /// <param name="buttons">Resolved activation buttons.</param>
        /// <param name="input">Router for configured triggers and accepted tool presses.</param>
        /// <param name="observer">Current view, when supplied by the dialogue driver.</param>
        /// <param name="visibility">Shared visibility cache.</param>
        /// <returns>True when a conversation branch was selected.</returns>
        private bool SelectRoute(DialogueNode router, float distance, Dictionary<InputActionReference, InteractionButton> buttons,
            InteractionInputRouter input, HoverObserver observer, DialogueVisibility visibility)
        {
            DialoguePolicy selected = null;
            DialogueNode selectedBlock = null;
            DialogueRow selectedRow = null;
            // Stable row order breaks equal priorities; unrelated branches never advance their sequence cursor.
            foreach (DialogueRow route in router.Entries)
            {
                DialogueNode target = session.Find(route.Next);
                DialoguePolicy policy = target.OverridePolicy ? target.Policy : settings;
                session.Route = target;
                if (selected != null && selected.Priority >= policy.Priority || !Matches(route, policy, distance, buttons, input, true))
                    continue;
                SetPresentation(policy);
                if (observer != null && !CanPresent(observer, visibility, false))
                    continue;
                selected = policy;
                selectedBlock = target;
                selectedRow = route;
            }
            session.Route = selectedBlock;
            session.RouteRow = selectedRow;
            SetPresentation(selected ?? settings);
            return selected != null;
        }

        /// <summary>Keeps a previous branch's sight interruption from blocking another branch.</summary>
        /// <param name="policy">Presentation policy selected by the current graph cursor.</param>
        private void SetPresentation(DialoguePolicy policy)
        {
            if (presentation != policy)
                awaitingSight = false;
            presentation = policy;
        }

        /// <summary>Evaluates one trigger using its branch's range and replay settings.</summary>
        /// <param name="row">Trigger-bearing entry or routing link.</param>
        /// <param name="policy">Presentation policy of the destination branch.</param>
        /// <param name="distance">Squared player distance.</param>
        /// <param name="buttons">Resolved input buttons.</param>
        /// <param name="input">Router for configured triggers and accepted tool presses.</param>
        /// <param name="replay">Whether this is a new request rather than a routed text row.</param>
        /// <returns>True when this row can accept the current event.</returns>
        private bool Matches(DialogueRow row, DialoguePolicy policy, float distance,
            Dictionary<InputActionReference, InteractionButton> buttons, InteractionInputRouter input, bool replay)
        {
            DialogueTriggerPreset trigger = row.Trigger;
            if (InteractionProjectileInput.Active && replay && trigger.Type != DialogueTrigger.InputAction)
                return false;
            float range = trigger.OverrideRange ? trigger.Distance : policy.Distance;
            float exit = trigger.OverrideRange ? trigger.ExitDistance : policy.ExitDistance;
            if (distance > exit * exit)
                session.ProximityUsed.Remove(row.Id);
            if (!ArrivalContext && !InteractionProjectileInput.IgnoreRange && distance > range * range)
                return false;
            bool matches = retained && row == session.Row || arrivalPending && ArrivalContext || (trigger.Type switch
            {
                DialogueTrigger.InputAction => ActionFor(trigger) is InputActionReference action
                    && buttons.TryGetValue(action, out InteractionButton button) && input.Read(this, button) != null,
                DialogueTrigger.Proximity => !session.ProximityUsed.Contains(row.Id),
                DialogueTrigger.Consumption => consumptionMatches.Contains(trigger) && (AwaitsContact(trigger)
                    ? contact != null && session.ContactRevision != contact.ContactRevision : session.ConsumptionRevision != Item.ConsumptionRevision
                        || session.Completed && armed && policy.ReplayOnReturn),
                DialogueTrigger.SpawnArrival => false,
                DialogueTrigger.OnEnter => true,
                _ => false
            });
            return matches && (!replay || !session.Completed || armed || trigger.Type == DialogueTrigger.Consumption
                || policy.ReplayOnInput && trigger.Type == DialogueTrigger.InputAction);
        }
        /// <summary>Evaluates order and receipt filters only after their revisions change.</summary>
        private void RefreshConsumption()
        {
            if (receiptRevision == Item.ConsumptionRevision && contactRevision == (contact != null ? contact.ContactRevision : -1)
                && orderRevision == (orders != null ? orders.Revision : -1))
                return;
            receiptRevision = Item.ConsumptionRevision;
            contactRevision = contact != null ? contact.ContactRevision : -1;
            orderRevision = orders != null ? orders.Revision : -1;
            consumptionMatches.Clear();
            foreach (DialogueNode block in content.Nodes)
                foreach (DialogueRow row in block.Entries)
                    if (row.Trigger.Type == DialogueTrigger.Consumption)
                    {
                        DialogueConsumptionFilter filter = row.Trigger.Consumption;
                        bool awaiting = AwaitsContact(row.Trigger);
                        if ((awaiting ? contact != null && contact.AwaitingContact : Item.HasConsumption) && filter.Matches(Item, orders))
                            consumptionMatches.Add(row.Trigger);
                    }
        }

        /// <summary>Distinguishes rejected contacts from real consumption receipts.</summary>
        /// <param name="trigger">Consumption trigger evaluated for this entry.</param>
        /// <returns>True when the enabled filter requests Awaiting Orders.</returns>
        private static bool AwaitsContact(DialogueTriggerPreset trigger)
        {
            return trigger.Consumption.Enabled && trigger.Consumption.Result == DialogueOrderResult.AwaitingOrders;
        }

        /// <summary>Checks range and availability while this component presents a page.</summary>
        /// <param name="player">Observed player root.</param>
        /// <returns>True when presentation can continue.</returns>
        internal bool CanContinue(Transform player)
        {
            float range = session.Row.Trigger.OverrideRange ? session.Row.Trigger.ExitDistance : Presentation.ExitDistance;
            return Available(InteractionChannels.Dialogue) && hud != null && hud.Ready
                && (ArrivalContext || ignoreExitRange || (transform.position - player.position).sqrMagnitude <= range * range);
        }

        /// <summary>Applies the shared free-cursor policy at presentation boundaries.</summary>
        /// <param name="observer">Current player and camera context.</param>
        /// <param name="visibility">Observer-owned sight cache.</param>
        /// <param name="continuing">Whether a page is already visible.</param>
        /// <returns>True when this page may be shown.</returns>
        internal bool CanPresent(HoverObserver observer, DialogueVisibility visibility, bool continuing)
        {
            if (InteractionProjectileInput.CursorUse)
                return visibility.HasSight(observer, this);
            if (ArrivalContext || InteractionProjectileInput.PhysicalImpact || continuing && projectileStarted
                || (continuing ? !Presentation.HideWhenSightLost : !Presentation.RequireSightToStart && !awaitingSight))
                return true;
            bool visible = visibility.HasSight(observer, this);
            if (visible || continuing)
                awaitingSight = !visible;
            return visible;
        }

        /// <summary>Checks the cursor before one page advance without changing graph progress.</summary>
        /// <param name="observer">Current player and camera.</param>
        /// <param name="visibility">Shared sight query cache.</param>
        /// <returns>True when the advance press is accepted.</returns>
        internal bool CanAdvance(HoverObserver observer, DialogueVisibility visibility)
        {
            return ArrivalContext || InteractionProjectileInput.Active || !Presentation.RequireSightToContinue || visibility.HasSight(observer, this);
        }

        #endregion

        #region Flow

        /// <summary>Reserves this clone's first request for Day Flow arrival.</summary>
        internal void ConfigureArrival()
        {
            arrivalOnly = true;
        }

        /// <summary>Queues one arrival request until this component wins the shared HUD.</summary>
        internal void RequestArrival()
        {
            arrivalPending = true;
        }

        /// <summary>Chooses one matching row after winning arbitration.</summary>
        internal void PrepareRequest()
        {
            pending = retained && session.Row != null ? System.Array.IndexOf(session.Block.Entries, session.Row) : session.Choose(eligible);
        }

        /// <summary>Resumes a frozen page or starts the selected row without changing player controls.</summary>
        /// <param name="presentation">Already saved and owned dialogue panel.</param>
        /// <returns>True when the request accepted its activation input.</returns>
        internal bool Begin(DialogueHud presentation)
        {
            if (pending < 0 || presentation == null || !presentation.Ready)
                return false;
            hud = presentation;
            if (!retained || session.Row == null)
                session.Begin(pending);
            retained = false;
            armed = false;
            arrivalSession |= arrivalPending;
            arrivalPending = false;
            session.ConsumptionRevision = Item.ConsumptionRevision;
            session.ContactRevision = contact != null ? contact.ContactRevision : -1;
            if (session.ActivationTrigger.Type == DialogueTrigger.Proximity)
                session.ProximityUsed.Add(session.ActivationId);
            projectileStarted = InteractionProjectileInput.PhysicalImpact;
            ignoreExitRange = InteractionProjectileInput.IgnoreRange;
            IsSpeaking = true;
            audio.Begin(Presentation.Audio, transform);
            hud.Show(session.Line, session.Row.Entry.TextEffects, Presentation.Box, session.PageTime);
            if (!session.Started)
            {
                session.Started = true;
                Signal(InteractionMoment.Started);
            }
            if (!Available(InteractionChannels.Dialogue))
                Interrupt();
            return true;
        }

        /// <summary>Advances one page or publishes a completed row before following its connection.</summary>
        internal void Advance()
        {
            if (!IsSpeaking)
                return;
            if (hud.RevealPage())
                return;
            if (!session.Advance())
            {
                hud.Show(session.Line, session.Row.Entry.TextEffects, Presentation.Box, 0f);
                return;
            }
            CompletedBlock = session.Block.Id;
            CompletedEntry = session.Row.Id;
            DialogueSession completed = session;
            session.Follow();
            // A new block observes future receipts. A self-loop also retains deliveries received during its page.
            if (session.Block.Id != CompletedBlock)
            {
                session.ConsumptionRevision = Item.ConsumptionRevision;
                session.ContactRevision = contact != null ? contact.ContactRevision : -1;
            }
            hud.Hide();
            IsSpeaking = false;
            retained = false;
            audio.Stop();
            // Step listeners see the new graph cursor but cannot mistake a checkpoint for tree completion.
            Signal(InteractionMoment.Progressed);
            CompletedBlock = CompletedEntry = string.Empty;
            if (session == completed && session.Completed)
            {
                audio.Finish(Presentation.Audio, transform);
                Signal(InteractionMoment.Completed);
            }
        }

        /// <summary>Applies interruption policy while leaving completed gameplay effects intact.</summary>
        internal void Interrupt()
        {
            if (!IsSpeaking)
                return;
            session.PageTime = hud != null ? hud.PageTime : session.PageTime;
            if (hud != null)
                hud.Hide();
            IsSpeaking = false;
            audio.Stop();
            retained = Presentation.Interruption != DialogueInterruption.RestartTree;
            if (Presentation.Interruption == DialogueInterruption.Restart)
            {
                session.Page = 0;
                session.PageTime = 0f;
            }
            else if (!retained)
            {
                session.Restart(false);
                session.ConsumptionRevision = session.ContactRevision = -1;
            }
        }

        /// <summary>Captures existing progress after interruption. New spawn clones keep their first activation state.</summary>
        /// <returns>Progress to restore, or null before a dialogue session exists.</returns>
        internal StoredProgress? CaptureStoredProgress()
        {
            // Walk-in staging can suspend a clone before its first OnEnable creates a cursor.
            if (session == null)
                return null;
            Interrupt();
            return new StoredProgress(this);
        }

        /// <summary>Restores a saved cursor without reopening presentation or emitting signals.</summary>
        /// <param name="progress">Snapshot captured before inventory storage.</param>
        internal void RestoreStoredProgress(StoredProgress progress)
        {
            session = progress.Session;
            armed = progress.Armed;
            retained = progress.Retained;
            presentation = progress.Presentation;
            receiptRevision = -1;
        }

        #endregion

        #endregion
    }
}
