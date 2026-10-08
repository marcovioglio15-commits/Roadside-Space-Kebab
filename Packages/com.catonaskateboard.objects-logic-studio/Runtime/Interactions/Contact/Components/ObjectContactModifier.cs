using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs independently configured contact modifications through one object-local interaction.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Modify By Contact")]
    public sealed class ObjectContactModifier : ObjectRequestedInteraction
    {
        #region Fields

        [Header("Modify By Contact")]
        [Tooltip("Reusable flag-filtered modifications evaluated in array order. Each retains independent contact timers and effects.")]
        [SerializeField]
        private ContactModificationSettings settings = new ContactModificationSettings();
        private ContactModificationRun[] runs = Array.Empty<ContactModificationRun>();
        private ContactModificationRun signaling;
        private ObjectFlag[] consumedFlags = Array.Empty<ObjectFlag>();
        private ObjectFlag[] consumedIngredients = Array.Empty<ObjectFlag>();
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Shared modification definitions selected by this interaction.</summary>
        public ContactModificationSettings Settings => settings;
        /// <summary>Whether at least one snapped modification needs confirmation.</summary>
        public override bool UsesInput => settings.RequiresInput;
        /// <summary>Shared confirmation targeting for this object's modifications.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Logical units exposed only during the current consumption completion event.</summary>
        internal int ConsumedUnits { get; private set; }
        /// <summary>Rule context available only while publishing its start or completion.</summary>
        internal ContactModificationRun SignalingRun => signaling;
        /// <summary>Duration of the rule currently starting its visual effect.</summary>
        internal override float VfxDuration => signaling != null ? signaling.Settings.Duration : settings.MinimumDuration;
        /// <summary>Whether any rule still owns an active transition.</summary>
        internal override bool VfxRunning => IsModifying || IsPaused;
        /// <summary>Whether the active transition is temporarily paused.</summary>
        internal override bool VfxPaused => IsPaused;
        /// <summary>Whether any rule is actively modifying a participant.</summary>
        public bool IsModifying => Array.Exists(runs, run => run != null && run.IsRunning);
        /// <summary>Whether an interrupted rule retains its appearance for later resumption.</summary>
        public bool IsPaused => Array.Exists(runs, run => run != null && run.IsPaused);
        /// <summary>Identifies this object's sole contact interaction.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ModifyByContact;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Starts reusable rule state only when an object enters gameplay.</summary>
        protected override void OnEnable()
        {
            if (Application.isPlaying)
                Initialize();
        }

        /// <summary>Recovers cached rule state when scene or domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery is restricted to Play entry.
            foreach (ObjectContactModifier modifier in FindObjectsByType<ObjectContactModifier>())
                if (modifier.isActiveAndEnabled)
                    modifier.Initialize();
        }

        /// <summary>Validates once and reuses one distinct reservation owner per modification.</summary>
        private void Initialize()
        {
            OnDisable();
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            // Runtime state needs distinct Unity identities for existing reservation and temporary-flag ownership.
            if (runs.Length != settings.Modifications.Length)
            {
                DisposeRuns();
                runs = new ContactModificationRun[settings.Modifications.Length];
            }
            for (int index = 0; index < runs.Length; index++)
            {
                if (runs[index] == null)
                {
                    runs[index] = ScriptableObject.CreateInstance<ContactModificationRun>();
                    runs[index].hideFlags = HideFlags.HideAndDontSave;
                }
                runs[index].Initialize(this, settings.Modifications[index].Settings);
            }
            base.OnEnable();
        }

        /// <summary>Advances cached rules after carried objects settle without allocating per frame.</summary>
        private void LateUpdate()
        {
            if (!ready)
                return;
            // A callback may disable this component while a preceding rule publishes an event.
            for (int index = 0; index < runs.Length && isActiveAndEnabled; index++)
                runs[index].Tick();
        }

        /// <summary>Releases only this interaction's active reservations and reversible effects.</summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (ContactModificationRun run in runs)
                if (run != null)
                    run.Disable();
            ready = false;
        }

        /// <summary>Releases the cached runtime containers when their owning object is destroyed.</summary>
        private void OnDestroy()
        {
            DisposeRuns();
        }

        /// <summary>Disposes per-rule Unity ownership identities outside the frame update path.</summary>
        private void DisposeRuns()
        {
            foreach (ContactModificationRun run in runs)
                if (run != null)
                {
                    run.Disable();
                    if (Application.isPlaying)
                        Destroy(run);
                    else
                        DestroyImmediate(run);
                }
            runs = Array.Empty<ContactModificationRun>();
        }

        #endregion
        #region Validation

        /// <summary>Recaches rule geometry after assembly changes without discarding contact history.</summary>
        internal void RefreshGeometry()
        {
            // Assembly may supply usable geometry after activation initially failed validation.
            if (!ready || runs.Length != settings.Modifications.Length)
            {
                Initialize();
                return;
            }
            ready = TryValidate(out string warning);
            if (!ready)
            {
                OnDisable();
                Debug.LogWarning(warning, this);
                return;
            }
            foreach (ContactModificationRun run in runs)
                run.RefreshGeometry();
        }

        /// <summary>Checks the authored rule array and this object's contact geometry.</summary>
        /// <param name="warning">Receives invalid settings or missing dependencies.</param>
        /// <returns>True when every modification can run on this object.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, out warning) && ValidateInput(settings.RequiresInput, Action, out warning);
        }

        /// <summary>Validates a detached proposal against the destination's existing collider hierarchy.</summary>
        /// <param name="configuration">Proposed shared modification references.</param>
        /// <param name="warning">Receives an invalid definition or unusable contact geometry.</param>
        /// <returns>True when every rule has valid data and compatible geometry.</returns>
        public bool TryValidate(ContactModificationSettings configuration, out string warning)
        {
            warning = "Modify By Contact requires an Object Item and complete modification settings.";
            ObjectItem item = GetComponent<ObjectItem>();
            if (item == null || configuration == null || !configuration.TryValidate(out warning))
                return false;
            // Collider discovery is limited to activation and explicit authoring checks.
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (ContactModificationDefinition definition in configuration.Modifications)
            {
                ContactPreparationSettings preparation = definition.Settings.Preparation;
                if (!preparation.AnimateOther && !preparation.ValidateTarget(item, out warning))
                    return false;
                bool found = false;
                foreach (Collider collider in colliders)
                    if (collider.GetComponentInParent<ObjectItem>() == item && ContactDetection.Usable(collider, definition.Settings.IncludeTriggers))
                    {
                        found = true;
                        break;
                    }
                if (!found && (Application.isPlaying || GetComponent<ObjectAssemblyProduct>() == null))
                {
                    warning = "Add an enabled Box, Sphere, Capsule or convex Mesh Collider compatible with every modification's trigger policy.";
                    return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #region Events

        /// <summary>Offers confirmation only while an acquired modification is waiting.</summary>
        /// <returns>True when the local tool and lock policies allow confirmation.</returns>
        internal override bool CanRequest()
        {
            return ready && isActiveAndEnabled && !IsLocked && ToolAllowed && Array.Exists(runs, run => run.Waiting);
        }

        /// <summary>Confirms the first waiting modification in authored priority order.</summary>
        /// <returns>True when one process consumed the player's request.</returns>
        internal override bool Request()
        {
            if (!CanRequest())
                return false;
            foreach (ContactModificationRun run in runs)
                if (run.Confirm())
                    return true;
            return false;
        }

        /// <summary>Publishes a successful rule boundary with its exact receipt and temporary identity context.</summary>
        /// <param name="run">Independent rule publishing the event.</param>
        /// <param name="moment">Successful start or completion.</param>
        /// <param name="receipt">Consumed identity flags, or an empty array for non-consumption.</param>
        /// <param name="units">Number of logical units consumed by this completion.</param>
        /// <param name="ingredients">Captured assembly ingredient flags, or null for non-consumption events.</param>
        internal void Publish(ContactModificationRun run, InteractionMoment moment, ObjectFlag[] receipt, int units, ObjectFlag[] ingredients = null)
        {
            signaling = run;
            consumedFlags = receipt;
            consumedIngredients = ingredients ?? Array.Empty<ObjectFlag>();
            ConsumedUnits = units;
            try
            {
                Signal(moment);
            }
            finally
            {
                signaling = null;
                consumedFlags = Array.Empty<ObjectFlag>();
                consumedIngredients = Array.Empty<ObjectFlag>();
                ConsumedUnits = 0;
            }
        }

        /// <summary>Matches only the victim of the completion currently being published.</summary>
        /// <param name="flags">Alternative identity flags required by an order or availability condition.</param>
        /// <returns>True when this successful consumption included any requested flag.</returns>
        internal bool ConsumedAny(ObjectFlag[] flags)
        {
            return ConsumedMatches(flags, false);
        }

        /// <summary>Checks this completion's receipt against one order's identity selection.</summary>
        /// <param name="flags">Flags identifying the requested item.</param>
        /// <param name="requireAll">Whether the same item must carry every selected flag.</param>
        /// <returns>True when the current receipt satisfies the requested identity.</returns>
        internal bool ConsumedMatches(ObjectFlag[] flags, bool requireAll)
        {
            // Cumulative receipts and later identity changes cannot satisfy this event's filter.
            if (flags == null || flags.Length == 0 || consumedFlags.Length == 0)
                return false;
            foreach (ObjectFlag flag in flags)
                if ((flag != null && Array.IndexOf(consumedFlags, flag) >= 0) != requireAll)
                    return !requireAll;
            return requireAll;
        }

        /// <summary>Checks both the consumed root identity and the actual recipe ingredients for one drawn candidate.</summary>
        /// <param name="candidate">Base or recipe-specific order drawn for this spawn.</param>
        /// <returns>True when this exact receipt satisfies every active order requirement.</returns>
        internal bool ConsumedMatches(OrderCandidate candidate)
        {
            return ConsumedUnits > 0 && ConsumedMatches(candidate.Entry.Flags, candidate.Entry.RequireAllFlags)
                && (candidate.Variant == null || candidate.Variant.Matches(consumedIngredients));
        }


        /// <summary>Keeps this interaction's identity change scoped to the rule that published it.</summary>
        /// <param name="moment">Successful event being published.</param>
        protected override void ApplyIdentityChange(InteractionMoment moment)
        {
            if (signaling == null || !signaling.Settings.WhileContact)
                base.ApplyIdentityChange(moment);
            else if (FlagChange.Enabled && FlagChange.Moment == moment && Item != null)
                Item.Identity.SetTemporary(signaling, FlagChange.Flag, FlagChange.Operation);
        }

        #endregion
        #endregion
    }
}
