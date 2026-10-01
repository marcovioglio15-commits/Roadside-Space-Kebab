using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs reversible visual transitions after continuous contact and commits consumption once.</summary>
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("Objects Logic Studio/Modify By Contact")]
    public sealed class ObjectContactModifier : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Modify By Contact")]
        [Tooltip("Contact conditions, effects and temporary restrictions for both participants.")]
        [SerializeField]
        private ContactModificationSettings settings = new ContactModificationSettings();

        #endregion

        #region State

        private readonly ContactDetection detection = new ContactDetection();
        private readonly ContactIdentityRun identityRun = new ContactIdentityRun();
        private readonly HashSet<ObjectItem> contacts = new HashSet<ObjectItem>();
        private readonly HashSet<ObjectItem> completed = new HashSet<ObjectItem>();
        private readonly HashSet<ObjectItem> failed = new HashSet<ObjectItem>();
        private readonly Dictionary<ObjectItem, float> entered = new Dictionary<ObjectItem, float>();
        private readonly List<ObjectItem> departed = new List<ObjectItem>();
        private ObjectItem other;
        private ContactEffectRun selfEffect;
        private ContactEffectRun otherEffect;
        private float nextQuery;
        private float started;
        private float elapsed;
        private bool paused;
        private bool ready;
        private bool running;
        private bool retained;
        private float revertAt = -1f;
        private string lastWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Modification duration, excluding the contact dwell before a successful start.</summary>
        internal override float VfxDuration => settings.Duration;
        /// <summary>Whether the effect still belongs to a running or retained transition.</summary>
        internal override bool VfxRunning => running || paused || identityRun.IsRunning;
        /// <summary>Whether automatic effect timing must pause with the contact transition.</summary>
        internal override bool VfxPaused => paused;

        /// <summary>Reusable settings edited by the passive-interaction card.</summary>
        public ContactModificationSettings Settings => settings;
        /// <summary>Whether effects currently own both participants.</summary>
        public bool IsModifying => running || identityRun.IsRunning;
        /// <summary>Whether an interrupted transition retains its appearance and counterpart.</summary>
        public bool IsPaused => paused;
        /// <summary>Identifies the passive feature in the workspace.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ModifyByContact;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Validates dependencies and captures geometry at activation.</summary>
        private void OnEnable()
        {
            // Pool activation refreshes geometry while retaining this item's consumption receipts.
            Initialize();
        }

        /// <summary>Restores cached contact state when Play preserves scene objects and managed fields.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery occurs once per Play entry, never during ordinary contact polling.
            foreach (ObjectContactModifier modifier in FindObjectsByType<ObjectContactModifier>())
                if (modifier.isActiveAndEnabled)
                    modifier.Initialize();
        }

        /// <summary>Starts a clean contact session and validates the currently authored collider types.</summary>
        private void Initialize()
        {
            // Re-enabling starts a new contact session without clearing either item's receipts.
            Cancel();
            contacts.Clear();
            entered.Clear();
            completed.Clear();
            failed.Clear();
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Report(warning);
                return;
            }
            detection.Bind(Item);
            nextQuery = 0f;
        }

        /// <summary>Restores unfinished appearance and releases restrictions on the surviving participant.</summary>
        private void OnDisable()
        {
            // Deactivation or consumption can occur inside another feature's completion callback.
            Cancel();
            contacts.Clear();
            entered.Clear();
        }

        /// <summary>Queries after carried poses settle, while animating only an active transition each frame.</summary>
        private void LateUpdate()
        {
            // Inactive items perform no physics queries or visual updates.
            if (!ready || Item == null || !Item.isActiveAndEnabled || Item.IsConsumed)
            {
                Cancel();
                return;
            }
            if (Time.timeScale <= 0f)
                return;
            if (settings.IdentityOnly)
            {
                // Flag-only zones track every contacted item and release reservations when each change completes.
                if (Time.time >= nextQuery)
                {
                    nextQuery = Time.time + settings.QueryInterval;
                    detection.Query(Item, settings, contacts);
                    identityRun.Tick(this, contacts);
                }
                return;
            }
            // Locked effects skip contact queries but still release a lost counterpart or interrupt their active transition.
            if ((!IsLocked && ToolAllowed || settings.WhileContact && (running || retained)) && Time.time >= nextQuery)
            {
                nextQuery = Time.time + settings.QueryInterval;
                detection.Query(Item, settings, contacts);
                UpdateContacts();
            }
            if ((running || paused || retained) && (other == null || !other.IsOwnedBy(this) || !Item.IsOwnedBy(this)))
            {
                Cancel();
                return;
            }
            if (settings.WhileContact && (running || retained))
            {
                UpdateRetention();
                if (retained || !running || IsLocked || !ToolAllowed || !Eligible(other))
                    return;
            }
            if (!running)
                return;
            if (!settings.WhileContact && (IsLocked || !ToolAllowed || !Eligible(other)
                || !settings.CompleteAfterSeparation && !contacts.Contains(other)))
            {
                Interrupt();
                return;
            }
            float progress = settings.Duration > 0f ? Mathf.Clamp01((Time.time - started) / settings.Duration) : 1f;
            selfEffect.Animate(progress);
            otherEffect.Animate(progress);
            if (progress >= 1f)
                Complete();
        }

        #endregion

        #region Validation

        /// <summary>Recaches compound geometry after a completed assembly change without clearing contact history.</summary>
        internal void RefreshGeometry()
        {
            // Assembly rejects reserved products, so an unfinished effect is never rebound here.
            ready = TryValidate(out string warning);
            if (ready)
                detection.Bind(Item);
            else
                Report(warning);
            nextQuery = 0f;
        }

        /// <summary>Checks contact settings, flag existence and owned contact geometry.</summary>
        /// <param name="warning">Receives the first configuration or dependency issue.</param>
        /// <returns>True when this interaction can detect contact safely.</returns>
        public override bool TryValidate(out string warning)
        {
            // Runtime activation and editor proposals share the same validation path.
            return TryValidate(settings, out warning);
        }

        /// <summary>Checks a detached tool proposal against this object's existing dependencies.</summary>
        /// <param name="configuration">Contact settings proposed by the tool.</param>
        /// <param name="warning">Receives invalid settings or missing geometry.</param>
        /// <returns>True when the proposed configuration can run on this object.</returns>
        public bool TryValidate(ContactModificationSettings configuration, out string warning)
        {
            // Validate references before recurring contact queries.
            warning = "Modify by contact requires an Object Item and complete settings.";
            if (GetComponent<ObjectItem>() == null || configuration == null || !configuration.TryValidate(out warning))
                return false;
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                if (collider.GetComponentInParent<ObjectItem>() == GetComponent<ObjectItem>()
                    && ContactDetection.Usable(collider, configuration.IncludeTriggers))
                {
                    warning = string.Empty;
                    return true;
                }
            if (!Application.isPlaying && GetComponent<ObjectAssemblyProduct>() != null)
            {
                warning = string.Empty;
                return true;
            }
            warning = "Add an enabled Box, Sphere, Capsule or convex Mesh Collider for contact detection.";
            return false;
        }

        #endregion

        #region Contacts

        /// <summary>Maintains continuous per-item timing and selects one deterministic ready participant.</summary>
        private void UpdateContacts()
        {
            // Compound colliders share one timer; leaving contact removes their accumulated dwell time.
            departed.Clear();
            foreach (ObjectItem candidate in entered.Keys)
                if (candidate == null || !contacts.Contains(candidate) || !Eligible(candidate) || !Matches(candidate))
                    departed.Add(candidate);
            foreach (ObjectItem candidate in departed)
            {
                entered.Remove(candidate);
                failed.Remove(candidate);
                if (settings.RepeatAfterSeparation)
                    completed.Remove(candidate);
            }
            ObjectItem selected = null;
            float distance = float.PositiveInfinity;
            foreach (ObjectItem candidate in contacts)
            {
                if (!Eligible(candidate) || !Matches(candidate))
                    continue;
                if (!entered.TryGetValue(candidate, out float time))
                    entered.Add(candidate, time = Time.time);
                if (running || retained || paused && candidate != other
                    || !Available(InteractionChannels.Passive) || candidate.IsBlocked(InteractionChannels.Passive)
                    || completed.Contains(candidate) || failed.Contains(candidate) || Time.time - time < settings.ContactDuration)
                    continue;
                float score = (candidate.transform.position - transform.position).sqrMagnitude;
                if (score < distance || score == distance && selected != null
                    && EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(selected.GetEntityId()))
                {
                    selected = candidate;
                    distance = score;
                }
            }
            if (selected != null)
            {
                if (paused)
                {
                    Item.Acquire(this, settings.BlockSelf);
                    other.Acquire(this, settings.BlockOther);
                    started = Time.time - elapsed;
                    paused = false;
                    running = true;
                }
                else
                    Begin(selected);
            }
        }

        /// <summary>Checks both independently configured carry conditions before starting or continuing effects.</summary>
        /// <param name="candidate">Other participant in this contact pair.</param>
        /// <returns>True when neither participant violates its carry policy.</returns>
        internal bool Eligible(ObjectItem candidate)
        {
            // Cached grab state avoids hierarchy searches in contact polling.
            return (settings.AllowCarriedSelf || !Item.IsCarried) && (settings.AllowCarriedOther || !candidate.IsCarried);
        }

        /// <summary>Retains physical contact after this modifier changes its counterpart's identity.</summary>
        /// <param name="candidate">Contacted item being evaluated.</param>
        /// <returns>True for an accepted alternative or this modifier's retained counterpart.</returns>
        private bool Matches(ObjectItem candidate)
        {
            // A completion-time flag replacement must not itself simulate separation.
            return retained && candidate == other || candidate.Identity != null && candidate.Identity.Matches(settings.Flags);
        }

        /// <summary>Reserves both items and prepares all effects before the first visible change.</summary>
        /// <param name="candidate">Matching item that completed its uninterrupted contact delay.</param>
        private void Begin(ObjectItem candidate)
        {
            // Failed ownership acquisition does not consume contact time or modify either object.
            if (!Item.Acquire(this, InteractionChannels.None))
                return;
            if (!candidate.Acquire(this, InteractionChannels.None))
            {
                Item.Release(this);
                return;
            }
            if (!ContactEffectRun.TryPrepare(Item, settings.Self, out selfEffect, out string warning)
                || !ContactEffectRun.TryPrepare(candidate, settings.Other, out otherEffect, out warning))
            {
                Item.Release(this);
                candidate.Release(this);
                Report(warning);
                // Invalid bindings are retried only after separation, avoiding recurring allocations and warnings.
                failed.Add(candidate);
                return;
            }
            other = candidate;
            revertAt = -1f;
            Item.Acquire(this, settings.BlockSelf);
            candidate.Acquire(this, settings.BlockOther);
            started = Time.time;
            elapsed = 0f;
            running = true;
            lastWarning = string.Empty;
            Signal(InteractionMoment.Started);
        }

        #endregion

        #region Completion

        /// <summary>Retains a pair's current transition or rolls it back according to the interruption policy.</summary>
        private void Interrupt()
        {
            // Keep visual ownership so another modifier cannot overwrite the retained baseline.
            if (!settings.ResumeAfterInterruption)
            {
                Cancel();
                return;
            }
            elapsed = Time.time - started;
            running = false;
            paused = true;
            Item.Acquire(this, InteractionChannels.None);
            other.Acquire(this, InteractionChannels.None);
            entered.Clear();
        }

        /// <summary>Commits mesh effects and a single consumption receipt before releasing ownership.</summary>
        private void Complete()
        {
            // Clear running first: deactivating the victim must not roll back an already completed tint.
            selfEffect.Commit();
            otherEffect.Commit();
            if (!settings.Self.Consume && !settings.Other.Consume)
                completed.Add(other);
            running = false;
            if (settings.Self.Consume)
                other.Consume(Item, this);
            else if (settings.Other.Consume)
                Item.Consume(other, this);
            // Receipts describe what was consumed; completion listeners observe the counterpart's final flag.
            if (settings.ChangeContactFlag && other != null)
            {
                if (settings.WhileContact)
                    other.Identity.SetTemporary(this, settings.ContactFlag, settings.ContactFlagOperation);
                else if (!InteractionFlagChange.TryApplyFlag(other.gameObject, settings.ContactFlag, out string warning, settings.ContactFlagOperation))
                    Report(warning);
            }
            if (settings.WhileContact)
            {
                // Keep visual ownership but release interaction restrictions so either item may move away.
                retained = true;
                Item.Acquire(this, InteractionChannels.None);
                other.Acquire(this, InteractionChannels.None);
            }
            else
                Release();
            Signal(InteractionMoment.Completed);
        }

        /// <summary>Restores an active or completed temporary change after uninterrupted separation.</summary>
        private void UpdateRetention()
        {
            // Physical contact remains authoritative even if completion changed the counterpart's flags.
            if (contacts.Contains(other) && Eligible(other) && !IsLocked && ToolAllowed)
            {
                revertAt = -1f;
                return;
            }
            if (revertAt < 0f)
                revertAt = Time.time + settings.RevertDelay;
            if (Time.time >= revertAt)
                Cancel();
        }

        /// <summary>Reverses only unfinished visual effects and restarts continuous contact timing.</summary>
        private void Cancel()
        {
            // Independent identity contacts may be active even when no visual transition owns the item.
            identityRun.Clear(this);
            // Permanent completions no longer own a snapshot; temporary completions restore theirs here.
            if (!running && !paused && !retained)
                return;
            running = false;
            paused = false;
            retained = false;
            selfEffect.Cancel();
            otherEffect.Cancel();
            if (other != null)
            {
                completed.Remove(other);
                other.Identity.RemoveTemporary(this);
            }
            if (Item != null)
                Item.Identity.RemoveTemporary(this);
            Release();
            entered.Clear();
        }

        /// <summary>Drops effect references and releases each participant's independent restrictions.</summary>
        private void Release()
        {
            // Unity fake-null checks also cover a participant destroyed during a transition.
            if (Item != null)
                Item.Release(this);
            if (other != null)
                other.Release(this);
            other = null;
            revertAt = -1f;
            selfEffect = otherEffect = null;
        }

        /// <summary>Keeps the owner's identity changes reversible for the lifetime of a contact effect.</summary>
        /// <param name="moment">Successful start or completion being published.</param>
        protected override void ApplyIdentityChange(InteractionMoment moment)
        {
            // A temporary contact never overwrites permanent membership or another zone's pending operation.
            if (!settings.WhileContact)
                base.ApplyIdentityChange(moment);
            else if (FlagChange.Enabled && FlagChange.Moment == moment && Item != null)
                Item.Identity.SetTemporary(this, FlagChange.Flag, FlagChange.Operation);
        }

        /// <summary>Reports a changed configuration failure without repeating it every contact query.</summary>
        /// <param name="warning">Action needed to make the interaction usable.</param>
        private void Report(string warning)
        {
            // Successful activation clears the diagnostic so a later independent failure remains visible.
            if (lastWarning == warning)
                return;
            lastWarning = warning;
            Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
