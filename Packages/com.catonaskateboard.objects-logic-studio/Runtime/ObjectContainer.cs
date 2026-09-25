using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Deposits carried instances by tag and exposes recovery only through a linked Dispenser.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Object Container")]
    public sealed class ObjectContainer : ObjectTransferInteraction
    {
        #region Serialized Fields

        [Header("Container")]
        [Tooltip("Allowed tags, capacity and appearance of stored objects.")]
        [SerializeField]
        private ContainerSettings settings = new ContainerSettings();

        #endregion

        #region State

        private readonly List<StoredObject> stored = new List<StoredObject>();
        private bool transferring;

        #endregion

        #region Properties

        /// <summary>Configuration used by each successful deposit.</summary>
        public ContainerSettings Settings => settings;
        /// <summary>Number of retained inventory entries.</summary>
        public int StoredCount => stored.Count;
        /// <summary>Identifies this single interaction card.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Container;
        /// <summary>Selection settings shared by deposits and normal object targeting.</summary>
        public override TransferTargetSettings Target => settings.Target;

        #endregion

        #region Methods

        #region Session

        /// <summary>Clears transient inventory counters when a new Play session retains managed scene fields.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetSession()
        {
            // Ordinary pooling keeps stock; only a new Play session resets these instance counters.
            foreach (ObjectContainer container in FindObjectsByType<ObjectContainer>(FindObjectsInactive.Include))
            {
                container.stored.Clear();
                container.transferring = false;
            }
        }

        #endregion

        #region Validation

        /// <summary>Checks configuration before input is bound.</summary>
        /// <param name="warning">Receives an invalid tag or setting.</param>
        /// <returns>True when the container can accept configured items.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // The same validation path is used by the detached editor draft.
            return TryValidate(settings, out warning);
        }

        /// <summary>Checks project tag existence without rewriting authored values.</summary>
        /// <param name="configuration">Proposed settings.</param>
        /// <param name="warning">Receives an invalid tag or setting.</param>
        /// <returns>True for a complete storage configuration.</returns>
        public bool TryValidate(ContainerSettings configuration, out string warning)
        {
            // CompareTag validates tag registration once, outside ordinary input dispatch.
            warning = "Container settings are missing.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            try
            {
                foreach (string tag in configuration.AllowedTags)
                    gameObject.CompareTag(tag);
            }
            catch (UnityException)
            {
                warning = "One of the Container's allowed tags is no longer defined in this project.";
            }
            return warning.Length == 0;
        }

        #endregion

        #region Inventory

        /// <summary>Recognizes suspended inventory bodies when the container itself is grabbable.</summary>
        /// <param name="body">Nested body encountered during compound-grab validation.</param>
        /// <returns>True when this body's item is retained in this inventory.</returns>
        internal bool OwnsStoredBody(Rigidbody body)
        {
            // Stored contents move with their container but never participate in independent physics.
            foreach (StoredObject entry in stored)
                if (entry.Grab != null && body.transform.IsChildOf(entry.Grab.transform))
                    return true;
            return false;
        }

        /// <summary>Checks capacity, ownership and the currently carried root's tag before changing any state.</summary>
        /// <param name="grab">Object occupying the observer's carry slot.</param>
        /// <returns>True when a deposit can be committed.</returns>
        internal bool CanStore(ObjectGrab grab)
        {
            // Reserved objects must finish contact changes before their interaction hierarchy is suspended.
            Prune();
            if (transferring || !Available(InteractionChannels.Transfer) || Item.IsReserved || grab == null || !grab.IsHeld
                || transform.IsChildOf(grab.transform) || grab.transform.IsChildOf(transform)
                || !settings.Unlimited && stored.Count >= settings.Capacity
                || Array.IndexOf(settings.AllowedTags, grab.gameObject.tag) < 0)
                return false;
            foreach (ObjectItem item in grab.GetComponentsInChildren<ObjectItem>(true))
                if (item.IsReserved || item.IsBlocked(InteractionChannels.Transfer))
                    return false;
            return true;
        }

        /// <summary>Transfers the occupied carry slot into inventory without cloning or consuming its contents.</summary>
        /// <param name="grab">Eligible carried item.</param>
        /// <returns>True after the original instance is safely stored.</returns>
        internal bool Store(ObjectGrab grab)
        {
            // Commit before publishing events so dependent spawn/unlock rules observe the updated inventory.
            if (!CanStore(grab))
                return false;
            transferring = true;
            try
            {
                grab.Cancel();
                StoredObject entry = new StoredObject(grab);
                entry.Suspend(this, stored.Count);
                stored.Add(entry);
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
                return true;
            }
            finally
            {
                transferring = false;
            }
        }

        /// <summary>Checks stock while preserving the Container's independent deposit lock.</summary>
        /// <returns>True when a linked Dispenser can attempt recovery.</returns>
        internal bool HasStoredItem()
        {
            // Deposit and recovery have separate interaction locks; both respect shared item restrictions.
            Prune();
            return !transferring && gameObject.activeInHierarchy && !Item.IsReserved
                && !Item.IsBlocked(InteractionChannels.Transfer) && stored.Count > 0;
        }

        /// <summary>Returns the most recently deposited instance and rolls back if its Grab cannot start.</summary>
        /// <param name="observer">Player receiving the stored object.</param>
        /// <param name="position">World output position.</param>
        /// <param name="rotation">World output orientation.</param>
        /// <param name="grab">Receives the recovered carried instance on success.</param>
        /// <param name="source">Dispenser root ignored during the pickup transition.</param>
        /// <param name="duration">Seconds spent travelling from the dispenser to the carry pose.</param>
        /// <returns>True when recovery acquired the empty carry slot.</returns>
        internal bool TryTake(HoverObserver observer, Vector3 position, Quaternion rotation, out ObjectGrab grab, Transform source, float duration)
        {
            // A failed pickup never drops an item out of the retained inventory.
            grab = null;
            if (!HasStoredItem() || observer.HeldObject != null)
                return false;
            transferring = true;
            StoredObject entry = stored[stored.Count - 1];
            try
            {
                entry.Restore(position, rotation);
                if (entry.Grab.Begin(observer, source, duration) && entry.Grab.IsHeld)
                {
                    grab = entry.Grab;
                    stored.RemoveAt(stored.Count - 1);
                    return true;
                }
                entry.Suspend(this, stored.Count - 1);
                return false;
            }
            finally
            {
                transferring = false;
            }
        }

        /// <summary>Removes externally destroyed contents only when inventory is queried or changed.</summary>
        private void Prune()
        {
            // Destruction outside this system cannot permanently occupy a limited storage slot.
            for (int index = stored.Count - 1; index >= 0; index--)
                if (stored[index].Grab == null)
                    stored.RemoveAt(index);
        }

        #endregion

        #endregion
    }
}
