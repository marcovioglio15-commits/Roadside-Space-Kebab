using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks independent reversible identity contacts without retaining appearance reservations.</summary>
    internal sealed class ContactIdentityRun
    {
        #region State

        /// <summary>Retains the timers for one contacted item, including a completed temporary flag change.</summary>
        private sealed class Contact
        {
            internal float Entered;
            internal float Started;
            internal float RevertAt = -1f;
            internal bool Running;
            internal bool Applied;
        }

        private readonly Dictionary<ObjectItem, Contact> entries = new Dictionary<ObjectItem, Contact>();
        private readonly List<ObjectItem> candidates = new List<ObjectItem>();
        private int running;

        #endregion

        #region Properties

        /// <summary>Whether any contact is still within its modification duration.</summary>
        internal bool IsRunning => running > 0;

        #endregion

        #region Methods

        #region Evaluation

        /// <summary>Updates existing contacts and starts independent timers for newly eligible items.</summary>
        /// <param name="owner">Modifier supplying flag operations and timing.</param>
        /// <param name="contacts">Current physical contacts, including items whose flags have already changed.</param>
        internal void Tick(ContactModificationRun owner, HashSet<ObjectItem> contacts)
        {
            // Snapshot keys so lifecycle events may disable the modifier without invalidating enumeration.
            candidates.Clear();
            candidates.AddRange(entries.Keys);
            for (int index = 0; index < candidates.Count && owner.Active; index++)
                if (entries.TryGetValue(candidates[index], out Contact contact))
                    Update(owner, candidates[index], contact, contacts.Contains(candidates[index]));
            if (!owner.Active)
                return;
            // New arrivals retain their own dwell and transition times, even while another item is modified.
            candidates.Clear();
            candidates.AddRange(contacts);
            for (int index = 0; index < candidates.Count && owner.Active; index++)
            {
                ObjectItem item = candidates[index];
                if (item == null || entries.ContainsKey(item) || !owner.Eligible(item)
                    || item.Identity == null || !item.Identity.Matches(owner.Settings.Flags)
                    || !owner.Available(InteractionChannels.Passive) || item.IsBlocked(InteractionChannels.Passive))
                    continue;
                Contact contact = new Contact { Entered = Time.time };
                entries.Add(item, contact);
                Update(owner, item, contact, true);
            }
            if (entries.Count == 0 && owner.Item != null)
                owner.Item.Identity.RemoveTemporary(owner);
        }

        /// <summary>Advances one item while preserving its original identity beneath the temporary operation.</summary>
        /// <param name="owner">Modifier that owns this temporary operation.</param>
        /// <param name="item">Contacted item.</param>
        /// <param name="contact">Retained timers for this item.</param>
        /// <param name="touching">Whether the physical contact query still contains the item.</param>
        private void Update(ContactModificationRun owner, ObjectItem item, Contact contact, bool touching)
        {
            // Disabled, consumed or externally released items cannot retain a stale contact operation.
            if (item == null || !item.isActiveAndEnabled || item.IsConsumed
                || contact.Running && (!item.IsOwnedBy(owner) || !owner.Item.IsOwnedBy(owner)))
            {
                Remove(owner, item, contact);
                return;
            }
            bool eligible = !owner.IsLocked && owner.ToolAllowed && owner.Eligible(item);
            if (touching && eligible)
                contact.RevertAt = -1f;
            else
            {
                if (contact.RevertAt < 0f)
                    contact.RevertAt = Time.time + owner.Settings.RevertDelay;
                if (!contact.Running && !contact.Applied || Time.time >= contact.RevertAt)
                {
                    Remove(owner, item, contact);
                    return;
                }
            }
            if (!eligible || contact.Applied)
                return;
            if (!contact.Running)
            {
                if (!item.Identity.Matches(owner.Settings.Flags))
                {
                    Remove(owner, item, contact);
                    return;
                }
                if (Time.time - contact.Entered < owner.Settings.ContactDuration || !Begin(owner, item, contact))
                    return;
            }
            // A start listener may disable either participant; do not apply effects after its cancellation.
            if (owner.Active && contact.Running && item.isActiveAndEnabled
                && Time.time - contact.Started >= owner.Settings.Duration)
            {
                if (owner.Settings.ChangeContactFlag)
                    item.Identity.SetTemporary(owner, owner.Settings.ContactFlag, owner.Settings.ContactFlagOperation);
                contact.Applied = true;
                ReleaseTransition(owner, item, contact);
                owner.Signal(InteractionMoment.Completed);
            }
        }

        /// <summary>Acquires restrictions only for the unfinished transition, sharing owner locks across contacts.</summary>
        /// <param name="owner">Modifier requesting the transition.</param>
        /// <param name="item">New participant.</param>
        /// <param name="contact">Timers to activate after successful acquisition.</param>
        /// <returns>True when this contact started successfully.</returns>
        private bool Begin(ContactModificationRun owner, ObjectItem item, Contact contact)
        {
            // Acquiring no restrictions first avoids releasing a held object if the counterpart is unavailable.
            if (!owner.Available(InteractionChannels.Passive) || item.IsBlocked(InteractionChannels.Passive)
                || !owner.Item.Acquire(owner, running > 0 ? owner.Settings.BlockSelf : InteractionChannels.None))
                return false;
            if (!item.Acquire(owner, InteractionChannels.None))
            {
                if (running == 0)
                    owner.Item.Release(owner);
                return false;
            }
            owner.Item.Acquire(owner, owner.Settings.BlockSelf);
            item.Acquire(owner, owner.Settings.BlockOther);
            contact.Started = Time.time;
            contact.Running = true;
            running++;
            owner.Signal(InteractionMoment.Started);
            return true;
        }

        #endregion

        #region Cleanup

        /// <summary>Releases a finished transition without removing its temporary identity operation.</summary>
        /// <param name="owner">Modifier retaining this completed contact.</param>
        /// <param name="item">Participant whose transition ended.</param>
        /// <param name="contact">Transition being released.</param>
        private void ReleaseTransition(ContactModificationRun owner, ObjectItem item, Contact contact)
        {
            // Completed identity-only contacts remain available to Eject, Grab and other ordinary actions.
            if (!contact.Running)
                return;
            contact.Running = false;
            running--;
            if (item != null)
                item.Release(owner);
            if (running == 0 && owner.Item != null)
                owner.Item.Release(owner);
        }

        /// <summary>Withdraws one contact without changing other zones or permanent identity edits.</summary>
        /// <param name="owner">Modifier whose operation expires.</param>
        /// <param name="item">Surviving or destroyed participant.</param>
        /// <param name="contact">Stored transition state.</param>
        private void Remove(ContactModificationRun owner, ObjectItem item, Contact contact)
        {
            // Dictionary keys remain removable even after Unity destroys their native object.
            ReleaseTransition(owner, item, contact);
            if (item != null && item.Identity != null)
                item.Identity.RemoveTemporary(owner);
            entries.Remove(item);
        }

        /// <summary>Restores all temporary identities when the modifier is disabled or refreshed.</summary>
        /// <param name="owner">Modifier leaving its current contact session.</param>
        internal void Clear(ContactModificationRun owner)
        {
            // Restoration emits no completion events and cannot start another contact operation.
            foreach (KeyValuePair<ObjectItem, Contact> entry in entries)
            {
                ReleaseTransition(owner, entry.Key, entry.Value);
                if (entry.Key != null && entry.Key.Identity != null)
                    entry.Key.Identity.RemoveTemporary(owner);
            }
            entries.Clear();
            candidates.Clear();
            running = 0;
            if (owner.Item != null)
                owner.Item.Identity.RemoveTemporary(owner);
        }

        #endregion

        #endregion
    }
}
