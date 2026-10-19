using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains brief collision offerings until the modifier's next update without keeping Unity collision data.</summary>
    internal sealed class ContactImpactBuffer
    {
        #region State

        /// <summary>Invalidates a queued impact when a later pickup or release changes the offering.</summary>
        private readonly struct Offering
        {
            internal readonly ObjectItem Item;
            internal readonly uint Pickup;
            internal readonly uint Release;
            internal readonly int Frame;

            /// <summary>Captures the item lifetime relevant to one pending physics impact.</summary>
            /// <param name="item">Counterpart accepted by the rule during its collision callback.</param>
            internal Offering(ObjectItem item)
            {
                Item = item;
                Pickup = item.PickupRevision;
                Release = item.ReleaseRevision;
                Frame = Time.frameCount;
            }
        }

        private readonly Queue<Offering> pending = new Queue<Offering>();
        private readonly HashSet<ObjectItem> recorded = new HashSet<ObjectItem>();

        #endregion
        #region Methods
        #region Buffering

        /// <summary>Coalesces callbacks from compound colliders and both collision participants.</summary>
        /// <param name="item">Eligible consuming counterpart whose impact must survive until LateUpdate.</param>
        internal void Record(ObjectItem item)
        {
            // A single physical offering can produce several callbacks before the modifier ticks.
            if (recorded.Add(item))
                pending.Enqueue(new Offering(item));
        }

        /// <summary>Removes the next offering while discarding stale, consumed or newly handled items.</summary>
        /// <param name="item">Receives one still-valid counterpart, or null when the buffer is empty.</param>
        /// <returns>True when an impact belongs to the current frame and player release.</returns>
        internal bool TryTake(out ObjectItem item)
        {
            // Never replay an old hit after a pause, deactivation or subsequent pickup.
            while (pending.Count > 0)
            {
                Offering offering = pending.Dequeue();
                recorded.Remove(offering.Item);
                if (offering.Frame != Time.frameCount || offering.Item == null || !offering.Item.isActiveAndEnabled
                    || offering.Item.IsConsumed || offering.Pickup != offering.Item.PickupRevision
                    || offering.Release != offering.Item.ReleaseRevision)
                    continue;
                item = offering.Item;
                return true;
            }
            item = null;
            return false;
        }

        /// <summary>Discards collision history when its rule leaves gameplay.</summary>
        internal void Clear()
        {
            pending.Clear();
            recorded.Clear();
        }

        #endregion
        #endregion
    }
}
