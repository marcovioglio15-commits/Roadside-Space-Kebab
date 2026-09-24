using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one runtime lock on an existing feature and releases it only after its configured conditions.</summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Objects Logic Studio/Unlock Interaction")]
    public sealed class ObjectInteractionUnlock : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Unlock Interaction")]
        [Tooltip("Existing target and independent event or input conditions. Runtime progress resets when this rule is re-enabled.")]
        [SerializeField]
        private InteractionUnlockSettings settings = new InteractionUnlockSettings();

        #endregion

        #region State

        private ObjectInteraction lockedTarget;
        private bool[] satisfied;
        private InteractionButton[] buttons;
        private bool ready;

        #endregion

        #region Properties

        /// <summary>Saved target and unlock conditions.</summary>
        public InteractionUnlockSettings Settings => settings;
        /// <summary>Whether this rule has released its target during the current activation.</summary>
        public bool IsUnlocked { get; private set; }
        /// <summary>Identifies the dedicated unlock category.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Unlock;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Locks before ordinary interactions run and subscribes to successful lifecycle events.</summary>
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
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, false);
            lockedTarget = null;
        }

        /// <summary>Starts a clean rule activation, including Play sessions with scene reload disabled.</summary>
        internal void Initialize()
        {
            // Clear previous ownership before reading a potentially changed target.
            ObjectInteraction.Signaled -= Observe;
            ObjectInteraction.Signaled += Observe;
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, false);
            IsUnlocked = false;
            ready = TryValidate(out string warning);
            lockedTarget = settings?.Target;
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, true);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            satisfied = new bool[settings.Conditions.Length];
            buttons = new InteractionButton[settings.Conditions.Length];
        }

        #endregion

        #region Conditions

        /// <summary>Checks a rule before it participates in event or player-input evaluation.</summary>
        /// <param name="warning">Receives the first invalid reference or setting.</param>
        /// <returns>True when every condition is usable.</returns>
        public override bool TryValidate(out string warning)
        {
            // Invalid conditions keep a selected target locked instead of silently granting access.
            warning = "Configure an existing target and at least one unlock condition.";
            return settings != null && settings.TryValidate(transform.root, out warning);
        }

        /// <summary>Resolves authored Buttons only when the registry or player action asset changes.</summary>
        /// <param name="input">Observer-owned input router.</param>
        internal void Bind(InteractionInputRouter input)
        {
            // Event-only conditions need no action subscriptions.
            if (!ready || IsUnlocked)
                return;
            for (int index = 0; index < buttons.Length; index++)
                buttons[index] = settings.Conditions[index].Trigger == UnlockTrigger.InputAction
                    ? input.Bind(settings.Conditions[index].Action) : null;
        }

        /// <summary>Accepts performed unlock commands only inside their independently configured ranges.</summary>
        /// <param name="player">Observer's tagged player root.</param>
        internal void Tick(Transform player)
        {
            // Conditions retain their progress while an active rule waits for its remaining requirements.
            if (!ready || IsUnlocked || !Available(InteractionChannels.None))
                return;
            for (int index = 0; index < buttons.Length; index++)
                if (!satisfied[index] && buttons[index] is { Pending: true }
                    && (settings.Target.transform.position - player.position).sqrMagnitude
                    <= settings.Conditions[index].Distance * settings.Conditions[index].Distance)
                {
                    satisfied[index] = true;
                    InteractionUnlockRegistry.Consume(settings.Conditions[index].Action);
                }
            TryUnlock();
        }

        /// <summary>Records the exact source component and lifecycle boundary required by each condition.</summary>
        /// <param name="source">Interaction that successfully advanced its lifecycle.</param>
        /// <param name="moment">Started or committed completion.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // No name matching occurs at runtime; duplicate and renamed cards keep their identity.
            if (!ready || IsUnlocked || !isActiveAndEnabled)
                return;
            for (int index = 0; index < satisfied.Length; index++)
                if (settings.Conditions[index].Trigger == UnlockTrigger.Interaction
                    && settings.Conditions[index].Source == source && settings.Conditions[index].Moment == moment)
                    satisfied[index] = true;
            TryUnlock();
        }

        /// <summary>Releases this rule's lock once its any/all requirement is satisfied.</summary>
        private void TryUnlock()
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
            IsUnlocked = true;
            Signal(InteractionMoment.Started);
            if (lockedTarget != null)
                lockedTarget.SetLocked(this, false);
            Signal(InteractionMoment.Completed);
        }

        #endregion

        #endregion
    }
}
