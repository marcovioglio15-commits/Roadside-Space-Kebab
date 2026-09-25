using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Interaction families that a running modification can temporarily suspend.</summary>
    [Flags]
    public enum InteractionChannels { None = 0, Grab = 1, Release = 2, Hover = 4, Dialogue = 8, Passive = 16, Assembly = 32, Transfer = 64, Spawn = 128, Slice = 256 }

    /// <summary>Owns runtime consumption receipts and independent temporary locks for one object.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Object Item")]
    public sealed class ObjectItem : MonoBehaviour
    {
        #region State

        private readonly Dictionary<string, int> consumedTags = new Dictionary<string, int>();
        private readonly Dictionary<UnityEngine.Object, InteractionChannels> locks = new Dictionary<UnityEngine.Object, InteractionChannels>();
        private InteractionChannels blocked;
        private UnityEngine.Object reservation;
        private ObjectGrab grab;

        #endregion

        #region Properties

        /// <summary>Counts of consumed item tags retained for this runtime instance.</summary>
        public IReadOnlyDictionary<string, int> ConsumedTags => consumedTags;
        /// <summary>Changes only when consumption receipts change.</summary>
        public int ConsumptionRevision { get; private set; }
        /// <summary>Whether this item has already been consumed, including after accidental reactivation.</summary>
        public bool IsConsumed { get; private set; }
        /// <summary>Whether this item's cached grab component currently owns the player's carry slot.</summary>
        public bool IsCarried => grab != null && grab.IsHeld;
        /// <summary>Whether a modification currently owns this item's appearance or consumption state.</summary>
        public bool IsReserved => reservation != null;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Refreshes the optional carry dependency when this item becomes active.</summary>
        private void OnEnable()
        {
            // All new features share this root; no component search is needed during lock checks.
            grab = GetComponent<ObjectGrab>();
        }

        /// <summary>Clears transient ownership and receipts before a Play session that retains scene instances.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetSession()
        {
            // Ordinary disable/enable retains receipts; only a new Play session resets all runtime state.
            foreach (ObjectItem item in FindObjectsByType<ObjectItem>(FindObjectsInactive.Include))
            {
                item.locks.Clear();
                item.blocked = InteractionChannels.None;
                item.reservation = null;
                item.consumedTags.Clear();
                item.ConsumptionRevision++;
                item.IsConsumed = false;
                item.grab = item.GetComponent<ObjectGrab>();
            }
        }

        /// <summary>Releases transient ownership when an item leaves the active scene.</summary>
        private void OnDisable()
        {
            // Receipts survive disable/enable; resetting a pooled item is an explicit operation.
            locks.Clear();
            blocked = InteractionChannels.None;
            reservation = null;
        }

        #endregion

        #region Ownership

        /// <summary>Checks visual ownership including ingredients suspended inside this assembled product.</summary>
        /// <param name="branch">Renderer or mesh branch being modified.</param>
        /// <returns>True when this item owns the branch directly or through an attached ingredient.</returns>
        internal bool Owns(Transform branch)
        {
            // Disabled ingredient items retain their receipts while their product owns visual effects.
            if (branch.GetComponentInParent<InteractionVfxInstance>(true) != null)
                return false;
            ObjectItem nearest = branch.GetComponentInParent<ObjectItem>();
            if (nearest == this)
                return true;
            ObjectAssemblyPart part = branch.GetComponentInParent<ObjectAssemblyPart>();
            return part != null && part.Product != null && part.Product.Item == this;
        }

        /// <summary>Checks a temporary block without changing a component's authored enabled state.</summary>
        /// <param name="channels">Interaction families being attempted.</param>
        /// <returns>True for consumed items or any blocked family.</returns>
        public bool IsBlocked(InteractionChannels channels)
        {
            // Flags let independent modifications suspend overlapping families safely.
            return IsConsumed || (blocked & channels) != 0;
        }

        /// <summary>Reserves this item against overlapping modification transactions.</summary>
        /// <param name="owner">Running modification requesting exclusive visual and consumption ownership.</param>
        /// <param name="channels">Additional interactions suspended until release.</param>
        /// <returns>True when this owner acquired or already holds the item.</returns>
        internal bool Acquire(UnityEngine.Object owner, InteractionChannels channels)
        {
            // Both participants must be reserved before either one receives effects.
            if (IsConsumed || !isActiveAndEnabled || reservation != null && reservation != owner)
                return false;
            reservation = owner;
            locks[owner] = channels;
            blocked = InteractionChannels.None;
            foreach (InteractionChannels restriction in locks.Values)
                blocked |= restriction;
            if ((channels & InteractionChannels.Grab) != 0 && grab != null)
                grab.Cancel();
            return true;
        }

        /// <summary>Removes only the caller's temporary interaction restrictions.</summary>
        /// <param name="owner">Modification finishing or being cancelled.</param>
        internal void Release(UnityEngine.Object owner)
        {
            // Recompute only at ownership boundaries, never in ordinary frame queries.
            if (reservation == owner)
                reservation = null;
            if (!locks.Remove(owner))
                return;
            blocked = InteractionChannels.None;
            foreach (InteractionChannels channels in locks.Values)
                blocked |= channels;
        }

        /// <summary>Checks retained ownership before a paused modification resumes.</summary>
        /// <param name="owner">Modification that originally reserved this participant.</param>
        /// <returns>True when activation changes have not invalidated the reservation.</returns>
        internal bool IsOwnedBy(UnityEngine.Object owner)
        {
            // A disable/enable cycle must not silently reuse a stale visual transaction.
            return reservation == owner && isActiveAndEnabled && !IsConsumed;
        }

        #endregion

        #region Consumption

        /// <summary>Records one reserved victim before deactivating its complete item root.</summary>
        /// <param name="victim">Other participant that disappears.</param>
        /// <param name="owner">Modification holding both participants.</param>
        /// <returns>True only for the first successful consumption of this victim.</returns>
        internal bool Consume(ObjectItem victim, UnityEngine.Object owner)
        {
            // Mark before callbacks can re-enter another interaction; the survivor keeps the receipt.
            if (victim == null || victim == this || IsConsumed || victim.IsConsumed
                || reservation != owner || victim.reservation != owner)
                return false;
            string tag = victim.gameObject.tag;
            consumedTags.TryGetValue(tag, out int count);
            int units = victim.TryGetComponent(out ObjectGrab grab) ? grab.Units : 1;
            if (units <= 0 || units > int.MaxValue - count)
            {
                Debug.LogWarning("Consumption requires positive units that fit the remaining tag counter.", victim);
                return false;
            }
            consumedTags[tag] = count + units;
            ConsumptionRevision++;
            victim.IsConsumed = true;
            victim.gameObject.SetActive(false);
            return true;
        }

        /// <summary>Reads receipts without exposing a mutable inventory to dialogue conditions.</summary>
        /// <param name="tag">Exact project tag recorded at consumption time.</param>
        /// <returns>The number of consumed items bearing that tag.</returns>
        public int CountConsumed(string tag)
        {
            // A missing tag is an unmet condition, never an implicit wildcard.
            return !string.IsNullOrEmpty(tag) && consumedTags.TryGetValue(tag, out int count) ? count : 0;
        }

        /// <summary>Clears receipts and consumed status before explicitly reusing an inactive pooled item.</summary>
        public void ResetConsumption()
        {
            // Active transactions must finish before their ownership history can be reset.
            if (reservation != null)
            {
                Debug.LogWarning("Finish the active modification before resetting consumption.", this);
                return;
            }
            consumedTags.Clear();
            IsConsumed = false;
            ConsumptionRevision++;
        }

        #endregion

        #endregion
    }
}
