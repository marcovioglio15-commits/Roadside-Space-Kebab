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

            /// <summary>Captures the cursor after the interruption policy has been applied.</summary>
            /// <param name="dialogue">Dialogue entering storage.</param>
            internal StoredProgress(ObjectDialogue dialogue)
            {
                Session = dialogue.session;
                Armed = dialogue.armed;
                Retained = dialogue.retained;
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
        internal InputActionReference RequestedAction => pending >= 0 && session.RequestBlock.Entries[pending].Trigger.Type == DialogueTrigger.InputAction
            && !arrivalPending ? ActionFor(session.RequestBlock.Entries[pending].Trigger) : null;
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
                audio.Tick(settings.Audio, transform);
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

        /// <summary>Collects matching rows only in the active block, without advancing random or sequence state.</summary>
        /// <param name="player">Current player root.</param>
        /// <param name="buttons">Already resolved input buttons.</param>
        /// <returns>True when this component may claim the idle dialogue channel.</returns>
        internal bool CanStart(Transform player, Dictionary<InputActionReference, InteractionButton> buttons)
        {
            if (!Ready || session == null || IsSpeaking || !Available(InteractionChannels.Dialogue))
                return false;
            if (arrivalOnly && !arrivalSession && !arrivalPending)
                return false;
            float distance = (transform.position - player.position).sqrMagnitude;
            if (distance > settings.ExitDistance * settings.ExitDistance)
            {
                session.ProximityUsed.Clear();
                if (settings.ReplayOnReturn)
                    armed = true;
            }
            eligible.Clear();
            RefreshConsumption();
            DialogueNode block = session.RequestBlock;
            // Retained pages keep their chosen entry even if a later delivery changes the filters.
            for (int index = 0; index < block.Entries.Length; index++)
            {
                DialogueRow row = block.Entries[index];
                DialogueTriggerPreset trigger = row.Trigger;
                if (InteractionProjectileInput.Active && trigger.Type != DialogueTrigger.InputAction)
                    continue;
                if (retained && session.Row != null && row != session.Row || block.Selection == DialogueSelection.WeightedRandom && row.Weight <= 0f)
                    continue;
                float range = trigger.OverrideRange ? trigger.Distance : settings.Distance;
                float exit = trigger.OverrideRange ? trigger.ExitDistance : settings.ExitDistance;
                if (distance > exit * exit)
                    session.ProximityUsed.Remove(row.Id);
                if (!arrivalPending && !arrivalSession && !InteractionProjectileInput.IgnoreRange && distance > range * range)
                    continue;
                bool matches = retained && row == session.Row || arrivalPending || (trigger.Type switch
                {
                    DialogueTrigger.InputAction => ActionFor(trigger) is InputActionReference action && buttons.TryGetValue(action, out InteractionButton button) && button.Pending,
                    DialogueTrigger.Proximity => !session.ProximityUsed.Contains(row.Id),
                    DialogueTrigger.Consumption => consumptionMatches.Contains(trigger) && (AwaitsContact(trigger)
                        ? contact != null && session.ContactRevision != contact.ContactRevision : session.ConsumptionRevision != Item.ConsumptionRevision
                            || session.Completed && armed && settings.ReplayOnReturn),
                    DialogueTrigger.SpawnArrival => false,
                    DialogueTrigger.OnEnter => true,
                    _ => false
                });
                if (matches && (!session.Completed || armed || trigger.Type == DialogueTrigger.Consumption
                    || settings.ReplayOnInput && trigger.Type == DialogueTrigger.InputAction))
                    eligible.Add(index);
            }
            return eligible.Count > 0;
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
                        if ((awaiting ? contact != null && contact.AwaitingContact : Item.HasConsumption) && filter.Matches(Item, orders)
                            && row.Trigger.AdditionalConsumption.Matches(Item, orders))
                            consumptionMatches.Add(row.Trigger);
                    }
        }

        /// <summary>Distinguishes rejected contacts from real consumption receipts.</summary>
        /// <param name="trigger">Consumption trigger and its optional additional filter.</param>
        /// <returns>True when either active filter requests Awaiting Orders.</returns>
        private static bool AwaitsContact(DialogueTriggerPreset trigger)
        {
            return trigger.Consumption.Enabled && trigger.Consumption.Result == DialogueOrderResult.AwaitingOrders
                || trigger.AdditionalConsumption.Enabled && trigger.AdditionalConsumption.Result == DialogueOrderResult.AwaitingOrders;
        }

        /// <summary>Checks range and availability while this component presents a page.</summary>
        /// <param name="player">Observed player root.</param>
        /// <returns>True when presentation can continue.</returns>
        internal bool CanContinue(Transform player)
        {
            float range = session.Row.Trigger.OverrideRange ? session.Row.Trigger.ExitDistance : settings.ExitDistance;
            return Available(InteractionChannels.Dialogue) && hud != null && hud.Ready
                && (arrivalSession || ignoreExitRange || (transform.position - player.position).sqrMagnitude <= range * range);
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
            if (arrivalPending || arrivalSession || InteractionProjectileInput.PhysicalImpact || continuing && projectileStarted
                || (continuing ? !settings.HideWhenSightLost : !settings.RequireSightToStart && !awaitingSight))
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
            return arrivalSession || InteractionProjectileInput.Active || !settings.RequireSightToContinue || visibility.HasSight(observer, this);
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
            if (session.Row.Trigger.Type == DialogueTrigger.Proximity)
                session.ProximityUsed.Add(session.Row.Id);
            projectileStarted = InteractionProjectileInput.PhysicalImpact;
            ignoreExitRange = InteractionProjectileInput.IgnoreRange;
            IsSpeaking = true;
            audio.Begin(settings.Audio, transform);
            hud.Show(session.Line, session.Row.Entry.TextEffects, settings.Box, session.PageTime);
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
                hud.Show(session.Line, session.Row.Entry.TextEffects, settings.Box, 0f);
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
                audio.Finish(settings.Audio, transform);
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
            retained = settings.Interruption != DialogueInterruption.RestartTree;
            if (settings.Interruption == DialogueInterruption.Restart)
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
            receiptRevision = -1;
        }

        #endregion

        #endregion
    }
}
