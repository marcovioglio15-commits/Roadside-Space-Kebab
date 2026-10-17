using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains rejected offerings until a subsequent player pickup and release, even after separation.</summary>
    internal sealed class ContactConsumptionRejections
    {
        #region State

        /// <summary>Separates the pickup that permits another dialogue from the release required for consumption.</summary>
        private readonly struct Offering
        {
            internal readonly uint Pickup;
            internal readonly uint Release;

            /// <summary>Records the current player interaction generations at rejection.</summary>
            /// <param name="item">Offering whose contact was refused.</param>
            internal Offering(ObjectItem item)
            {
                Pickup = item.PickupRevision;
                Release = item.ReleaseRevision;
            }
        }

        private readonly Dictionary<ObjectItem, Offering> rejected = new Dictionary<ObjectItem, Offering>();
        private readonly List<ObjectItem> expired = new List<ObjectItem>();

        #endregion

        #region Methods

        #region Eligibility

        /// <summary>Records the release generation of an item refused by a closed dialogue gate.</summary>
        /// <param name="item">Qualifying contact that could otherwise be consumed.</param>
        /// <returns>True only for the first rejection or a new successful player pickup.</returns>
        internal bool Reject(ObjectItem item)
        {
            // Contact separation and a release alone cannot repeat the awaiting-order dialogue.
            bool known = rejected.TryGetValue(item, out Offering previous);
            bool announce = !known || previous.Pickup != item.PickupRevision;
            if (!announce && previous.Release == item.ReleaseRevision)
                return false;
            // Prune destroyed offerings at rejection boundaries, without scene searches or per-frame allocations.
            expired.Clear();
            foreach (ObjectItem candidate in rejected.Keys)
                if (candidate == null)
                    expired.Add(candidate);
            foreach (ObjectItem candidate in expired)
                rejected.Remove(candidate);
            rejected[item] = new Offering(item);
            return announce;
        }

        /// <summary>Rearms only after a deliberate Drop or Throw newer than the rejection.</summary>
        /// <param name="item">Offering considered after the consumption gate opens.</param>
        /// <returns>True while the rejected item has not been picked up and released again.</returns>
        internal bool Blocks(ObjectItem item)
        {
            // Moving, bouncing or toggling the modifier cannot count as a new player offering.
            if (!rejected.TryGetValue(item, out Offering offering))
                return false;
            if (item.IsCarried || item.ReleaseRevision == offering.Release)
                return true;
            rejected.Remove(item);
            return false;
        }

        /// <summary>Starts fresh rejection history only when the owning modifier begins a new spawn.</summary>
        internal void Clear()
        {
            rejected.Clear();
            expired.Clear();
        }

        #endregion

        #endregion
    }
}
