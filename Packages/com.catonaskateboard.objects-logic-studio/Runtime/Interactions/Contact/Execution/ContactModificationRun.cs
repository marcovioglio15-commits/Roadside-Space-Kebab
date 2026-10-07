using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one modification's cached detection, timers and reversible reservations without creating components.</summary>
    internal sealed class ContactModificationRun : ScriptableObject
    {
        #region State

        private ObjectContactModifier owner;
        private ContactModificationRule settings;
        private readonly ContactDetection detection = new ContactDetection();
        private readonly ContactIdentityRun identityRun = new ContactIdentityRun();
        private readonly ContactPreparationRun preparation = new ContactPreparationRun();
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
        private bool running;
        private bool retained;
        private bool effectsStarted;
        private bool returning;
        private float revertAt = -1f;
        private string lastWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Flags, timing and effects owned by this independent modification.</summary>
        internal ContactModificationRule Settings => settings;
        /// <summary>Shared item whose reservations use this run as a distinct ownership token.</summary>
        internal ObjectItem Item => owner != null ? owner.Item : null;
        /// <summary>Whether the containing interaction remains active.</summary>
        internal bool Active => owner != null && owner.isActiveAndEnabled;
        /// <summary>Whether the containing interaction is locked.</summary>
        internal bool IsLocked => owner.IsLocked;
        /// <summary>Whether the containing interaction accepts the active player tool.</summary>
        internal bool ToolAllowed => owner.ToolAllowed;
        /// <summary>Whether any transition owned by this rule is running.</summary>
        internal bool IsRunning => running || returning || identityRun.IsRunning;
        /// <summary>Whether this rule retains a snapped item while awaiting its Button.</summary>
        internal bool Waiting => preparation.Waiting;
        /// <summary>Whether an interrupted visual transition retains its counterpart.</summary>
        internal bool IsPaused => paused;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Reuses this rule's state container when its component is activated.</summary>
        /// <param name="component">Interaction publishing this rule's lifecycle events.</param>
        /// <param name="configuration">Validated flags, timing and effects for this rule.</param>
        internal void Initialize(ObjectContactModifier component, ContactModificationRule configuration)
        {
            Cancel();
            owner = component;
            settings = configuration;
            contacts.Clear();
            entered.Clear();
            completed.Clear();
            failed.Clear();
            detection.Bind(Item);
            nextQuery = 0f;
            lastWarning = string.Empty;
        }

        /// <summary>Restores unfinished appearance and releases restrictions on the surviving participant.</summary>
        internal void Disable()
        {
            // Deactivation or consumption can occur inside another feature's completion callback.
            Cancel();
            contacts.Clear();
            entered.Clear();
        }

        /// <summary>Queries after carried poses settle, while animating only an active transition each frame.</summary>
        internal void Tick()
        {
            // Inactive items perform no physics queries or visual updates.
            if (!Active || Item == null || !Item.isActiveAndEnabled || Item.IsConsumed)
            {
                Cancel();
                return;
            }
            if (Time.timeScale <= 0f)
                return;
            if (returning)
            {
                // Consumption may already have deactivated the counterpart; only the owner animates its return.
                preparation.Tick(Time.deltaTime);
                if (!preparation.Returning)
                {
                    returning = false;
                    Release();
                }
                return;
            }
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
            if (!effectsStarted)
            {
                if (IsLocked || !ToolAllowed)
                    return;
                preparation.Tick(Time.deltaTime);
                if (!preparation.Ready)
                    return;
                effectsStarted = true;
                started = Time.time;
                Signal(InteractionMoment.Started);
                if (!running)
                    return;
            }
            else
                preparation.Tick(Time.deltaTime);
            if (!settings.WhileContact && (IsLocked || !ToolAllowed || !Eligible(other)
                || !settings.Preparation.Snap && !settings.CompleteAfterSeparation && !contacts.Contains(other)))
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
            detection.Bind(Item);
            nextQuery = 0f;
        }

        /// <summary>Uses the containing interaction's shared availability and item restrictions.</summary>
        /// <param name="channels">Interaction family being requested.</param>
        /// <returns>True when the component may start this modification.</returns>
        internal bool Available(InteractionChannels channels)
        {
            return owner.Available(channels);
        }

        /// <summary>Publishes a rule event while preserving its duration and reversible identity context.</summary>
        /// <param name="moment">Successful start or completion boundary.</param>
        internal void Signal(InteractionMoment moment)
        {
            owner.Publish(this, moment, Array.Empty<ObjectFlag>(), 0);
        }

        #endregion

        #region Contacts

        /// <summary>Accepts the observer's confirmation for this rule without resetting its snap.</summary>
        /// <returns>True when this rule was awaiting the request.</returns>
        internal bool Confirm()
        {
            return preparation.Confirm();
        }

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
                float score = (candidate.transform.position - owner.transform.position).sqrMagnitude;
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
            candidate.Acquire(this, settings.BlockOther | (settings.Preparation.Snap ? InteractionChannels.Grab : InteractionChannels.None));
            started = Time.time;
            elapsed = 0f;
            running = true;
            effectsStarted = false;
            lastWarning = string.Empty;
            preparation.Begin(owner.transform, candidate, settings.Preparation);
        }

        #endregion

        #region Completion

        /// <summary>Retains a pair's current transition or rolls it back according to the interruption policy.</summary>
        private void Interrupt()
        {
            // Keep visual ownership so another modifier cannot overwrite the retained baseline.
            if (settings.Preparation.Snap || !settings.ResumeAfterInterruption)
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
            ObjectFlag[] receipt = Array.Empty<ObjectFlag>();
            ObjectItem victim = settings.Self.Consume ? Item : other;
            int units = victim != null && victim.TryGetComponent(out ObjectGrab grab) ? grab.Units : 1;
            bool consumed = false;
            if (settings.Self.Consume)
                consumed = other.Consume(Item, this, out receipt);
            else if (settings.Other.Consume)
                consumed = Item.Consume(other, this, out receipt);
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
            else if (!settings.Self.Consume && preparation.Return())
                returning = true;
            else
                Release();
            // Expose this receipt only during the exact completion event, including self-consumption.
            owner.Publish(this, InteractionMoment.Completed, receipt, consumed ? units : 0);
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
            if (!running && !paused && !retained && !returning)
                return;
            bool committed = returning;
            running = false;
            paused = false;
            retained = false;
            returning = false;
            if (!committed)
            {
                selfEffect?.Cancel();
                otherEffect?.Cancel();
            }
            preparation.Release(!committed);
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
            preparation.Release(false);
            // Unity fake-null checks also cover a participant destroyed during a transition.
            if (Item != null)
                Item.Release(this);
            if (other != null)
                other.Release(this);
            other = null;
            revertAt = -1f;
            selfEffect = otherEffect = null;
        }

        /// <summary>Reports a changed configuration failure without repeating it every contact query.</summary>
        /// <param name="warning">Action needed to make the interaction usable.</param>
        private void Report(string warning)
        {
            // Successful activation clears the diagnostic so a later independent failure remains visible.
            if (lastWarning == warning)
                return;
            lastWarning = warning;
            Debug.LogWarning($"Modify by Contact '{owner.InteractionName}', modification '{settings.Name}': {warning}", owner);
        }

        #endregion

        #endregion
    }
}
