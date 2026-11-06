using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Advances permanent tint and flag changes independently for each contacted item.</summary>
    internal sealed class ContactParallelEffects
    {
        #region Types

        /// <summary>Keeps per-item dwell timing and the acquired tint baseline.</summary>
        private sealed class Contact
        {
            internal float Since;
            internal ContactEffectRun Effect;
        }

        #endregion

        #region State

        private readonly Dictionary<ObjectItem, Contact> entries = new Dictionary<ObjectItem, Contact>();
        private readonly List<ObjectItem> removed = new List<ObjectItem>();
        private readonly List<ObjectItem> finishing = new List<ObjectItem>();
        private readonly HashSet<ObjectItem> completed = new HashSet<ObjectItem>();

        #endregion

        #region Properties

        /// <summary>Whether at least one counterpart is visibly transitioning.</summary>
        internal bool Running { get; private set; }

        #endregion

        #region Methods

        #region Progress

        /// <summary>Reconciles the measured contacts while retaining their independent dwell timers.</summary>
        /// <param name="owner">Rule owning each counterpart reservation.</param>
        /// <param name="contacts">Most recent measured contacts.</param>
        internal void Scan(ContactModificationRun owner, HashSet<ObjectItem> contacts)
        {
            ContactModificationRule settings = owner.Settings;
            removed.Clear();
            foreach (ObjectItem item in completed)
                if (item == null || settings.RepeatAfterSeparation && !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
                completed.Remove(item);

            // Only a new eligible contact creates state. The animation loop reuses it until completion.
            foreach (ObjectItem item in contacts)
                if (owner.Eligible(item) && item.Identity.Matches(settings.Flags) && !completed.Contains(item)
                    && !entries.ContainsKey(item))
                    entries.Add(item, new Contact { Since = Time.time });
        }

        /// <summary>Starts, advances or cancels each counterpart without reserving the source.</summary>
        /// <param name="owner">Rule supplying effects and signals.</param>
        /// <param name="contacts">Most recent measured contacts.</param>
        internal void Tick(ContactModificationRun owner, HashSet<ObjectItem> contacts)
        {
            ContactModificationRule settings = owner.Settings;
            Running = false;
            removed.Clear();
            finishing.Clear();
            foreach (KeyValuePair<ObjectItem, Contact> pair in entries)
            {
                ObjectItem item = pair.Key;
                Contact state = pair.Value;
                if (!Matches(owner, item)
                    || !contacts.Contains(item) && (state.Effect == null || !settings.CompleteAfterSeparation)
                    || state.Effect != null && !item.IsOwnedBy(owner))
                {
                    Cancel(owner, item, state);
                    removed.Add(item);
                    continue;
                }
                if (state.Effect == null && (Time.time - state.Since < settings.ContactDuration
                    || item.IsBlocked(InteractionChannels.Passive) || !Begin(owner, item, state)))
                    continue;
                float progress = settings.Duration > 0f ? Mathf.Clamp01((Time.time - state.Since) / settings.Duration) : 1f;
                if (progress < 1f)
                {
                    state.Effect.Animate(progress);
                    Running = true;
                }
                else
                    finishing.Add(item);
            }
            foreach (ObjectItem item in removed)
                entries.Remove(item);

            // A flag listener may disable the rule. Finish outside the dictionary traversal.
            for (int index = 0; index < finishing.Count; index++)
                if (entries.TryGetValue(finishing[index], out Contact state))
                    Complete(owner, finishing[index], state);
        }

        /// <summary>Commits the final tint only when the requested flag operation succeeds.</summary>
        /// <param name="owner">Rule that owns the counterpart.</param>
        /// <param name="item">Counterpart reaching the end of cooking or another permanent effect.</param>
        /// <param name="state">Original tint and its prepared transition.</param>
        private void Complete(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            // An earlier completion can invalidate another contact through a flag listener.
            entries.Remove(item);
            if (!Matches(owner, item) || !item.IsOwnedBy(owner))
            {
                Cancel(owner, item, state);
                return;
            }
            ContactModificationRule settings = owner.Settings;
            using ObjectIdentityChangeScope change = item.Identity.GroupChanges();
            if (settings.ChangeContactFlag && !InteractionFlagChange.TryApplyFlag(item.gameObject, settings.ContactFlag,
                out string warning, settings.ContactFlagOperation))
            {
                Cancel(owner, item, state);
                Debug.LogWarning(warning, item);
            }
            else
            {
                state.Effect.Animate(1f);
                state.Effect.Commit();
                item.Release(owner);
            }
            completed.Add(item);
        }

        /// <summary>Checks whether an existing contact still qualifies for this rule.</summary>
        /// <param name="owner">Rule defining the carry and flag filters.</param>
        /// <param name="item">Counterpart retained by the current run.</param>
        /// <returns>True when the counterpart is active and still passes both filters.</returns>
        private static bool Matches(ContactModificationRun owner, ObjectItem item)
        {
            return item != null && item.isActiveAndEnabled && owner.Eligible(item) && item.Identity.Matches(owner.Settings.Flags);
        }

        /// <summary>Captures a single counterpart's tint baseline once it can be reserved.</summary>
        /// <param name="owner">Rule owning the reservation.</param>
        /// <param name="item">Eligible counterpart.</param>
        /// <param name="state">Its dwell and effect state.</param>
        /// <returns>True when the timed effect can begin.</returns>
        private bool Begin(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            if (!item.Acquire(owner, owner.Settings.BlockOther))
                return false;
            if (!ContactEffectRun.TryPrepare(item, owner.Settings.Other, out ContactEffectRun effect, out string warning))
            {
                item.Release(owner);
                completed.Add(item);
                removed.Add(item);
                Debug.LogWarning(warning, item);
                return false;
            }
            state.Effect = effect;
            state.Since = Time.time;
            return true;
        }

        #endregion

        #region Teardown

        /// <summary>Restores pending changes when the source is unavailable.</summary>
        /// <param name="owner">Rule releasing its counterpart reservations.</param>
        /// <param name="resetHistory">Also allow completed pairs to run again on the next activation.</param>
        internal void Clear(ContactModificationRun owner, bool resetHistory = false)
        {
            foreach (KeyValuePair<ObjectItem, Contact> pair in entries)
                Cancel(owner, pair.Key, pair.Value);
            entries.Clear();
            finishing.Clear();
            Running = false;
            if (resetHistory)
                completed.Clear();
        }

        /// <summary>Restores an unfinished tint and releases its exact owner.</summary>
        /// <param name="owner">Rule that acquired the item.</param>
        /// <param name="item">Counterpart leaving the rule.</param>
        /// <param name="state">Tint baseline retained by the run.</param>
        private static void Cancel(ContactModificationRun owner, ObjectItem item, Contact state)
        {
            if (state.Effect == null)
                return;
            state.Effect.Cancel();
            if (item != null)
                item.Release(owner);
        }

        #endregion

        #endregion
    }
}
