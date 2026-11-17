using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Accumulates continuous wetting without counting simultaneous droplets as extra exposure time.</summary>
    internal sealed class SprayExposure
    {
        #region State
        /// <summary>Retains the selected rule and one contact interval for a receiving item.</summary>
        private sealed class Contact
        {
            internal SpraySurfaceRule Rule;
            internal float Started;
            internal float Last;
            internal bool Applied;
            internal bool Failed;
        }
        private readonly Dictionary<ObjectItem, Contact> contacts = new Dictionary<ObjectItem, Contact>();
        private readonly List<ObjectItem> expired = new List<ObjectItem>();
        private float cleanup;
        private ObjectIdentity identity;
        private ObjectFlag[] flags = Array.Empty<ObjectFlag>();
        private bool refreshFlags = true;
        #endregion
        #region Methods
        #region Sauce Identity

        /// <summary>Tracks source identity changes without allocating a flag snapshot for every droplet.</summary>
        /// <param name="source">Emitter identity supplying the next liquid samples.</param>
        internal void Bind(ObjectIdentity source)
        {
            // Rebinding also covers Play sessions that retain their scene objects.
            Release();
            identity = source;
            refreshFlags = true;
            ObjectIdentity.Changed += IdentityChanged;
        }

        /// <summary>Stops observing a disabled source while emitted samples retain their own immutable flags.</summary>
        internal void Release()
        {
            // Static events must never keep a disabled or destroyed emitter alive.
            ObjectIdentity.Changed -= IdentityChanged;
        }

        /// <summary>Reuses one immutable snapshot until this emitter's flags change.</summary>
        /// <returns>Flags shared by samples emitted with the same source identity.</returns>
        internal ObjectFlag[] CaptureFlags()
        {
            // Enumerate the identity only at change boundaries, never once per sample or contact.
            if (!refreshFlags)
                return flags;
            refreshFlags = false;
            if (identity == null)
                return flags = Array.Empty<ObjectFlag>();
            IReadOnlyCollection<ObjectFlag> current = identity.ActiveFlags;
            flags = current.Count > 0 ? new ObjectFlag[current.Count] : Array.Empty<ObjectFlag>();
            int index = 0;
            foreach (ObjectFlag flag in current)
                flags[index++] = flag;
            return flags;
        }

        /// <summary>Invalidates only this emitter's snapshot after a committed flag change.</summary>
        /// <param name="changed">Identity publishing its new membership.</param>
        private void IdentityChanged(ObjectIdentity changed)
        {
            // Other scene identities do not rebuild this source's cached flags.
            if (changed == identity)
                refreshFlags = true;
        }

        /// <summary>Records a physical sauce application even after its source despawns.</summary>
        /// <param name="collider">Collided surface.</param>
        /// <param name="source">Original emitter item, excluded from its own sauce.</param>
        /// <param name="applied">Immutable identities captured by this liquid sample.</param>
        /// <returns>The active receiving item, or null for an unrelated surface or the source itself.</returns>
        internal static ObjectItem ApplyFlags(Collider collider, ObjectItem source, ObjectFlag[] applied)
        {
            // Compound assembly colliders belong to the product's physical root.
            ObjectItem item = collider.attachedRigidbody != null ? collider.attachedRigidbody.GetComponent<ObjectItem>() : null;
            if (item == null)
                item = collider.GetComponentInParent<ObjectItem>();
            if (item == null || item == source || item.IsConsumed || !item.isActiveAndEnabled)
                return null;
            item.ApplyIngredient(applied);
            return item;
        }

        #endregion
        #region Contact
        /// <summary>Clears previous Play-session exposure without resetting ordinary disable and enable transitions.</summary>
        internal void Clear()
        {
            contacts.Clear();
            expired.Clear();
            cleanup = 0f;
        }

        /// <summary>Starts or advances a matching target's exposure at a physical hit boundary.</summary>
        /// <param name="source">Emitter owning the short appearance transaction.</param>
        /// <param name="item">Receiving item already resolved by the physical sample.</param>
        /// <returns>The exposed rule, or null while exposure is incomplete or no filter matches.</returns>
        internal SpraySurfaceRule Hit(ObjectSpraySauce source, ObjectItem item)
        {
            // Recipe applications have already committed independently of this visual transaction.
            if (item == null || item == source.Item || item.IsConsumed || !item.isActiveAndEnabled)
                return null;
            if (!source.Settings.ChangeAppearance)
                return null;
            // Keep tracking bounded and remove inactive targets only at contact boundaries.
            if (Time.time >= cleanup)
            {
                expired.Clear();
                foreach (KeyValuePair<ObjectItem, Contact> pair in contacts)
                    if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.IsConsumed)
                        expired.Add(pair.Key);
                foreach (ObjectItem key in expired)
                    contacts.Remove(key);
                cleanup = Time.time + 1f;
            }
            if (!contacts.TryGetValue(item, out Contact contact))
            {
                if (contacts.Count >= 128)
                    return null;
                foreach (SpraySurfaceRule rule in source.Settings.Rules)
                    if (item.Identity.Matches(rule.Flags, rule.Match))
                    {
                        contact = new Contact { Rule = rule, Started = Time.time, Last = Time.time };
                        contacts.Add(item, contact);
                        break;
                    }
                if (contact == null)
                    return null;
            }
            if (!item.Identity.Matches(contact.Rule.Flags, contact.Rule.Match))
            {
                contacts.Remove(item);
                return null;
            }
            if (contact.Applied || contact.Failed)
                return contact.Applied ? contact.Rule : null;
            if (Time.time - contact.Last > contact.Rule.ContactGap)
                contact.Started = Time.time;
            contact.Last = Time.time;
            if (Time.time - contact.Started < contact.Rule.Exposure || !item.Acquire(source, InteractionChannels.None))
                return null;
            try
            {
                if (ItemAppearanceChanges.TryPrepare(item, contact.Rule.Appearance, out ItemAppearanceChanges changes, out string warning))
                {
                    changes.Commit();
                    // Bind after replacement so the multiplier uses the receiving material's new base color.
                    if (ContactEffectRun.TryPrepare(item, contact.Rule.Tint, out ContactEffectRun tint, out warning))
                    {
                        tint.Animate(1f);
                        tint.Commit();
                        contact.Applied = true;
                    }
                    else
                        changes.Restore();
                }
                if (!contact.Applied)
                {
                    Debug.LogWarning("Spray Sauce: " + warning, item);
                    contact.Failed = true;
                }
            }
            finally
            {
                item.Release(source);
            }
            return contact.Applied ? contact.Rule : null;
        }
        #endregion
        #endregion
    }
}
