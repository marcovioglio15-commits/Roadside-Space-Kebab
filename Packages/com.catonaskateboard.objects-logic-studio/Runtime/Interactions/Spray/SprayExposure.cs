using System.Collections.Generic;
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
        }
        private readonly Dictionary<ObjectItem, Contact> contacts = new Dictionary<ObjectItem, Contact>();
        private readonly List<ObjectItem> expired = new List<ObjectItem>();
        private float cleanup;
        #endregion
        #region Methods
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
        /// <param name="collider">Exact receiving collider.</param>
        internal void Hit(ObjectSpraySauce source, Collider collider)
        {
            ObjectItem item = collider.GetComponentInParent<ObjectItem>();
            if (item == null || item == source.Item || item.IsConsumed || !item.isActiveAndEnabled)
                return;
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
                    return;
                foreach (SpraySurfaceRule rule in source.Settings.Rules)
                    if (item.Identity.Matches(rule.Flags, rule.Match))
                    {
                        contact = new Contact { Rule = rule, Started = Time.time, Last = Time.time };
                        contacts.Add(item, contact);
                        break;
                    }
                if (contact == null)
                    return;
            }
            if (contact.Applied)
                return;
            if (!item.Identity.Matches(contact.Rule.Flags, contact.Rule.Match))
            {
                contacts.Remove(item);
                return;
            }
            if (Time.time - contact.Last > contact.Rule.ContactGap)
                contact.Started = Time.time;
            contact.Last = Time.time;
            if (Time.time - contact.Started < contact.Rule.Exposure || !item.Acquire(source, InteractionChannels.None))
                return;
            try
            {
                if (ItemAppearanceChanges.TryPrepare(item, contact.Rule.Appearance, out ItemAppearanceChanges changes, out string warning))
                {
                    changes.Commit();
                    contact.Applied = true;
                }
                else
                {
                    Debug.LogWarning("Spray Sauce: " + warning, item);
                    contact.Applied = true;
                }
            }
            finally
            {
                item.Release(source);
            }
        }
        #endregion
        #endregion
    }
}
