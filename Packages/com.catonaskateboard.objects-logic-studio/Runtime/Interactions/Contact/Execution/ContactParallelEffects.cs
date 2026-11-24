using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs independent counterpart preparation and effects while sharing read-only source restrictions.</summary>
    internal sealed class ContactParallelEffects
    {
        #region Types

        /// <summary>Retains one item's dwell, preparation, appearance snapshot and completion timer.</summary>
        private sealed class Contact
        {
            internal float Entered;
            internal float Started;
            internal ContactEffectRun Effect;
            internal readonly ContactPreparationRun Preparation = new ContactPreparationRun();
            internal bool StartedEffect;
            internal bool Returning;
        }

        #endregion
        #region State

        private readonly Dictionary<ObjectItem, Contact> entries = new Dictionary<ObjectItem, Contact>();
        private readonly List<ObjectItem> snapshot = new List<ObjectItem>();
        private readonly HashSet<ObjectItem> completed = new HashSet<ObjectItem>();
        private bool restricted;

        #endregion
        #region Properties

        /// <summary>Whether preparation, an effect or its return animation is active.</summary>
        internal bool Running { get; private set; }
        /// <summary>Whether any effect is currently advancing.</summary>
        internal bool Audible { get; private set; }
        /// <summary>Whether any prepared item awaits confirmation.</summary>
        internal bool Waiting
        {
            get
            {
                foreach (Contact state in entries.Values)
                    if (state.Preparation.Waiting)
                        return true;
                return false;
            }
        }

        #endregion
        #region Methods
        #region Evaluation

        /// <summary>Adds eligible physical contacts and resets completed history only after real separation.</summary>
        /// <param name="owner">Rule accepting the counterparts.</param>
        /// <param name="contacts">Physical contacts plus this rule's retained magnet leases.</param>
        internal void Scan(ContactModificationRun owner, HashSet<ObjectItem> contacts)
        {
            // Snapshot removals so callbacks cannot invalidate the history traversal.
            snapshot.Clear();
            foreach (ObjectItem item in completed)
                if (item == null || owner.Settings.RepeatAfterSeparation && !contacts.Contains(item))
                    snapshot.Add(item);
            foreach (ObjectItem item in snapshot)
                completed.Remove(item);
            foreach (ObjectItem item in contacts)
                if (Matches(owner, item) && !completed.Contains(item) && !entries.ContainsKey(item))
                    entries.Add(item, new Contact { Entered = Time.time });
        }

        /// <summary>Advances each prepared item without reserving the source's appearance.</summary>
        /// <param name="owner">Rule owning each independent transition.</param>
        /// <param name="contacts">Most recent contact set.</param>
        internal void Tick(ContactModificationRun owner, HashSet<ObjectItem> contacts)
        {
            // Event listeners may remove participants; never iterate the live dictionary across signals.
            snapshot.Clear();
            snapshot.AddRange(entries.Keys);
            Running = Audible = false;
            for (int index = 0; index < snapshot.Count && owner.Active; index++)
                if (entries.TryGetValue(snapshot[index], out Contact state))
                    Advance(owner, snapshot[index], state, contacts.Contains(snapshot[index]));
            if (!Running && restricted)
            {
                owner.Item.Release(owner);
                restricted = false;
            }
        }

        /// <summary>Prepares, animates and completes one item using its own timer.</summary>
        /// <param name="owner">Rule supplying preparation and effects.</param>
        /// <param name="item">Accepted counterpart.</param>
        /// <param name="state">Independent transition state.</param>
        /// <param name="touching">Whether contact or docking remains valid.</param>
        private void Advance(ContactModificationRun owner, ObjectItem item, Contact state, bool touching)
        {
            // Returning animation may outlive the flag that originally selected this rule.
            if (item == null || !item.isActiveAndEnabled || item.IsConsumed
                || state.Effect != null && !item.IsOwnedBy(owner)
                || !state.Returning && (!Matches(owner, item) || !touching && !owner.Settings.CompleteAfterSeparation))
            {
                ReleaseContact(owner, item);
                return;
            }
            if (state.Effect == null)
            {
                if (Time.time - state.Entered < owner.Settings.ContactDuration || item.IsBlocked(InteractionChannels.Passive))
                    return;
                if (!owner.PrepareDock(item))
                {
                    Running |= owner.HasDock(item);
                    return;
                }
                if (!Begin(owner, item, state))
                    return;
            }
            Running = true;
            state.Preparation.Tick(Time.deltaTime);
            if (state.Returning)
            {
                if (!state.Preparation.Returning)
                    Finish(owner, item, state);
                return;
            }
            if (!state.Preparation.Ready)
                return;
            if (!state.StartedEffect)
            {
                state.StartedEffect = true;
                state.Started = Time.time;
                owner.Signal(InteractionMoment.Started, item);
                if (!owner.Active || !entries.ContainsKey(item))
                    return;
            }
            float progress = owner.Settings.Duration > 0f ? Mathf.Clamp01((Time.time - state.Started) / owner.Settings.Duration) : 1f;
            state.Effect.Animate(progress);
            Audible |= progress < 1f;
            if (progress >= 1f)
                Complete(owner, item, state);
        }

        /// <summary>Captures effects only after the item reaches its docking pose.</summary>
        /// <param name="owner">Modification owning restrictions.</param>
        /// <param name="item">Counterpart ready for preparation.</param>
        /// <param name="state">Transition receiving the effect snapshot.</param>
        /// <returns>True when preparation started successfully.</returns>
        private bool Begin(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            if (!item.Acquire(owner, owner.Settings.BlockOther))
                return false;
            if (!owner.Settings.Preparation.ValidateTarget(item, out string warning)
                || !ContactEffectRun.TryPrepare(item, owner.Settings.Other, out state.Effect, out warning))
            {
                ReleaseContact(owner, item);
                completed.Add(item);
                Debug.LogWarning(warning, item);
                return false;
            }
            // Shared restrictions retain the source's existing component and reservation state.
            owner.Item.Restrict(owner, owner.Settings.BlockSelf);
            restricted = true;
            state.Preparation.Begin(owner.Item.transform, item, owner.Settings.Preparation, owner.UsesMagnets);
            return true;
        }

        /// <summary>Commits only this counterpart and releases its slot after any return animation.</summary>
        /// <param name="owner">Rule publishing the completion.</param>
        /// <param name="item">Completed counterpart.</param>
        /// <param name="state">Appearance and preparation transaction.</param>
        private void Complete(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            if (!state.Effect.CanCommit())
            {
                ReleaseContact(owner, item);
                completed.Add(item);
                return;
            }
            using ObjectIdentityChangeScope changes = item.Identity.GroupChanges();
            if (owner.Settings.ChangeContactFlag && !InteractionFlagChange.TryApplyFlag(item.gameObject,
                owner.Settings.ContactFlag, out string warning, owner.Settings.ContactFlagOperation))
            {
                ReleaseContact(owner, item);
                Debug.LogWarning(warning, item);
                return;
            }
            state.Effect.Commit();
            completed.Add(item);
            state.Returning = state.Preparation.Return();
            if (!state.Returning)
                Finish(owner, item, state);
            owner.Signal(InteractionMoment.Completed, item);
        }

        /// <summary>Releases a committed counterpart without reverting its final appearance.</summary>
        /// <param name="owner">Rule ending ownership.</param>
        /// <param name="item">Participant whose return ended.</param>
        /// <param name="state">Completed transaction.</param>
        private void Finish(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            entries.Remove(item);
            state.Preparation.Release(false);
            item.Release(owner);
            owner.ReleaseDock(item);
            ReleaseRestrictions(owner);
        }

        /// <summary>Checks counterpart activation and rule identity.</summary>
        /// <param name="owner">Rule supplying filters.</param>
        /// <param name="item">Candidate counterpart.</param>
        /// <returns>True when active and compatible.</returns>
        private static bool Matches(ContactModificationRun owner, ObjectItem item)
        {
            return item != null && item.isActiveAndEnabled && !item.IsConsumed && owner.Eligible(item)
                && item.Identity != null && item.Identity.Matches(owner.Settings.Flags);
        }

        #endregion
        #region Input And Timing

        /// <summary>Keeps automatic feedback alive only for its own active counterpart effect.</summary>
        /// <param name="item">Participant captured when the feedback started.</param>
        /// <returns>True between this participant's effect start and completion or cancellation.</returns>
        internal bool IsModifying(ObjectItem item)
        {
            // Preparation and return movement do not extend the modification's feedback.
            return item != null && entries.TryGetValue(item, out Contact state) && state.StartedEffect && !state.Returning;
        }

        /// <summary>Confirms the first waiting item without replaying its attraction.</summary>
        /// <returns>True when one waiting preparation consumed the input.</returns>
        internal bool Confirm()
        {
            foreach (Contact state in entries.Values)
                if (state.Preparation.Confirm())
                    return true;
            return false;
        }

        /// <summary>Estimates the selected item's completion for occupied-slot policies.</summary>
        /// <param name="item">Participant being ranked.</param>
        /// <param name="duration">Configured effect duration.</param>
        /// <returns>Remaining preparation and effect time.</returns>
        internal float Remaining(ObjectItem item, float duration)
        {
            return !entries.TryGetValue(item, out Contact state) ? duration
                : state.Returning ? state.Preparation.Remaining
                : state.Preparation.Remaining + (state.StartedEffect ? Mathf.Max(0f, duration - (Time.time - state.Started)) : duration);
        }

        #endregion
        #region Teardown

        /// <summary>Cancels only the participant leaving this rule.</summary>
        /// <param name="owner">Rule retaining its effect and slot.</param>
        /// <param name="item">Picked-up, disabled or incompatible item.</param>
        internal void ReleaseContact(ContactModificationRun owner, ObjectItem item)
        {
            if (!entries.TryGetValue(item, out Contact state))
                return;
            entries.Remove(item);
            state.Effect?.Cancel();
            state.Preparation.Release(true);
            if (item != null)
                item.Release(owner);
            owner.ReleaseDock(item);
            ReleaseRestrictions(owner);
        }

        /// <summary>Restores source availability immediately after its last independent contact ends.</summary>
        /// <param name="owner">Rule retaining shared restrictions.</param>
        private void ReleaseRestrictions(ContactModificationRun owner)
        {
            if (entries.Count != 0 || !restricted)
                return;
            owner.Item.Release(owner);
            restricted = false;
        }

        /// <summary>Releases transitions and restrictions when the source becomes unavailable.</summary>
        /// <param name="owner">Rule ending its contact session.</param>
        /// <param name="resetHistory">Whether completed items may start again after activation.</param>
        internal void Clear(ContactModificationRun owner, bool resetHistory = false)
        {
            snapshot.Clear();
            snapshot.AddRange(entries.Keys);
            for (int index = 0; index < snapshot.Count; index++)
                ReleaseContact(owner, snapshot[index]);
            if (owner.Item != null)
                owner.Item.Release(owner);
            restricted = Running = Audible = false;
            if (resetHistory)
                completed.Clear();
        }

        #endregion
        #endregion
    }
}
