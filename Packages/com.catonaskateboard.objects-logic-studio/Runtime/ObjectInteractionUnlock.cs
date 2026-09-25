using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Changes the availability of existing interactions once its independent conditions are satisfied.</summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Objects Logic Studio/Unlock Interaction")]
    public sealed class ObjectInteractionUnlock : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Availability Rule")]
        [Tooltip("Existing targets, availability operation and event or input conditions. Re-enabling resets this rule and its owned locks.")]
        [SerializeField]
        private InteractionUnlockSettings settings = new InteractionUnlockSettings();

        #endregion

        #region State

        private ObjectInteraction lockedTarget;
        private ObjectInteraction replacementTarget;
        private bool[] satisfied;
        private InteractionButton[] buttons;
        private bool ready;

        #endregion

        #region Properties

        /// <summary>Saved availability operation, existing targets and conditions.</summary>
        public InteractionUnlockSettings Settings => settings;
        /// <summary>Whether this rule has applied its Unlock, Lock or Replace operation during this activation.</summary>
        public bool IsApplied { get; private set; }
        /// <summary>Identifies the dedicated unlock category.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Unlock;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Applies initial availability before ordinary interactions run and subscribes to their events.</summary>
        private void OnEnable()
        {
            // Each rule has independent ownership even when several rules refer to the same target.
            Initialize();
            InteractionUnlockRegistry.Register(this);
        }

        /// <summary>Removes only this rule's subscriptions and lock when it is disabled or destroyed.</summary>
        private void OnDisable()
        {
            // Other unlock rules or passive restrictions remain active.
            ObjectInteraction.Signaled -= Observe;
            InteractionUnlockRegistry.Unregister(this);
            ReleaseLocks();
        }

        /// <summary>Starts a clean rule activation, including Play sessions with scene reload disabled.</summary>
        internal void Initialize()
        {
            // Clear previous ownership before reading a potentially changed target.
            ObjectInteraction.Signaled -= Observe;
            ObjectInteraction.Signaled += Observe;
            ReleaseLocks();
            IsApplied = false;
            ready = TryValidate(out string warning);
            lockedTarget = settings != null && InteractionUnlockSettings.IsLocal(settings.Target, transform.root) ? settings.Target : null;
            replacementTarget = settings != null && settings.Operation == InteractionAvailabilityChange.Replace
                && InteractionUnlockSettings.IsLocal(settings.Replacement, transform.root)
                && settings.Replacement != lockedTarget && InteractionUnlockSettings.IsSameKind(lockedTarget, settings.Replacement)
                ? settings.Replacement : null;
            ApplyLocks();
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            satisfied = new bool[settings.Conditions.Length];
            buttons = new InteractionButton[settings.Conditions.Length];
        }

        /// <summary>Removes only this activation's ownership before resetting or disabling the rule.</summary>
        private void ReleaseLocks()
        {
            // Independent rules retain their own restrictions on the same components.
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, false);
            if (replacementTarget != null)
                replacementTarget.SetLocked(this, false);
            lockedTarget = replacementTarget = null;
        }

        /// <summary>Applies the initial or completed phase without changing component enabled states.</summary>
        private void ApplyLocks()
        {
            // Replacement releases the incoming feature only after blocking the outgoing one.
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, settings.Operation switch
                {
                    InteractionAvailabilityChange.Unlock => !IsApplied,
                    InteractionAvailabilityChange.Lock or InteractionAvailabilityChange.Replace => IsApplied,
                    _ => false
                });
            if (replacementTarget != null)
                replacementTarget.SetLocked(this, !IsApplied);
        }

        #endregion

        #region Conditions

        /// <summary>Checks a rule before it participates in event or player-input evaluation.</summary>
        /// <param name="warning">Receives the first invalid reference or setting.</param>
        /// <returns>True when every condition is usable.</returns>
        public override bool TryValidate(out string warning)
        {
            // Invalid conditions preserve the operation's initial phase instead of committing a change.
            warning = "Configure an existing target and at least one availability condition.";
            return settings != null && settings.TryValidate(transform.root, out warning);
        }

        /// <summary>Captures satisfied conditions before inventory temporarily disables this component.</summary>
        /// <returns>A detached progress snapshot, or null for an uninitialized rule.</returns>
        internal bool[] CaptureStoredProgress()
        {
            // Storage preserves progress without changing the normal reset-on-enable authoring policy.
            return satisfied != null ? (bool[])satisfied.Clone() : null;
        }

        /// <summary>Restores inventory-retained conditions after the original rule has reactivated.</summary>
        /// <param name="conditions">Progress captured before storage.</param>
        /// <param name="applied">Whether this rule had already committed its availability operation.</param>
        internal void RestoreStoredProgress(bool[] conditions, bool applied)
        {
            // Restoration never publishes another unlock completion or changes other rules' ownership.
            if (!ready || conditions == null || satisfied.Length != conditions.Length)
                return;
            System.Array.Copy(conditions, satisfied, conditions.Length);
            IsApplied = applied;
            ApplyLocks();
        }

        /// <summary>Resolves authored Buttons only when the registry or player action asset changes.</summary>
        /// <param name="input">Observer-owned input router.</param>
        internal void Bind(InteractionInputRouter input)
        {
            // Event-only conditions need no action subscriptions.
            if (!ready || IsApplied)
                return;
            for (int index = 0; index < buttons.Length; index++)
                buttons[index] = settings.Conditions[index].Trigger == UnlockTrigger.InputAction
                    ? input.Bind(settings.Conditions[index].Action) : null;
        }

        /// <summary>Accepts availability commands only inside their independently configured ranges.</summary>
        /// <param name="player">Observer's tagged player root.</param>
        internal void Tick(Transform player)
        {
            // Conditions retain their progress while an active rule waits for its remaining requirements.
            if (!ready || IsApplied || !Available(InteractionChannels.None))
                return;
            for (int index = 0; index < buttons.Length; index++)
                if (!satisfied[index] && buttons[index] is { Pending: true }
                    && (settings.Target.transform.position - player.position).sqrMagnitude
                    <= settings.Conditions[index].Distance * settings.Conditions[index].Distance)
                {
                    satisfied[index] = true;
                    InteractionUnlockRegistry.Consume(settings.Conditions[index].Action);
                }
            TryApply();
        }

        /// <summary>Records the exact source component and lifecycle boundary required by each condition.</summary>
        /// <param name="source">Interaction that successfully advanced its lifecycle.</param>
        /// <param name="moment">Started or committed completion.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // No name matching occurs at runtime; duplicate and renamed cards keep their identity.
            if (!ready || IsApplied || !Available(InteractionChannels.None))
                return;
            for (int index = 0; index < satisfied.Length; index++)
                if (settings.Conditions[index].Trigger == UnlockTrigger.Interaction
                    && settings.Conditions[index].Source == source && settings.Conditions[index].Moment == moment)
                    satisfied[index] = true;
            TryApply();
        }

        /// <summary>Commits this rule's operation once its any/all requirement is satisfied.</summary>
        private void TryApply()
        {
            // A target with multiple rules becomes usable only after every owning rule releases it.
            bool any = false;
            foreach (bool condition in satisfied)
            {
                if (settings.RequireAll && !condition)
                    return;
                any |= condition;
            }
            if (!any)
                return;
            IsApplied = true;
            ApplyLocks();
            Signal(InteractionMoment.Started);
            Signal(InteractionMoment.Completed);
        }

        #endregion

        #endregion
    }
}
