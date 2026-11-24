using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns contact docking during a modification and restores the item's original physics once.</summary>
    internal sealed class ContactMagnetRun
    {
        #region State

        private static readonly Dictionary<ObjectItem, ContactMagnetRun> owners = new Dictionary<ObjectItem, ContactMagnetRun>();
        private readonly List<ContactMagnetLease> held = new List<ContactMagnetLease>();
        private readonly Dictionary<object, int> supplies = new Dictionary<object, int>();
        private readonly Dictionary<ObjectItem, ContactModificationRun> rejected = new Dictionary<ObjectItem, ContactModificationRun>();
        private readonly List<ObjectItem> separated = new List<ObjectItem>();
        private readonly ContactMagnetPlacement placement = new ContactMagnetPlacement();
        private ObjectContactModifier owner;
        private ContactMagnetSettings settings;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Restores retained bodies before clearing static state for a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Domain reload can be disabled while live component instances survive.
            foreach (ContactMagnetRun run in new HashSet<ContactMagnetRun>(owners.Values))
                run.Clear();
            owners.Clear();
        }

        /// <summary>Binds validated local slots when the interaction becomes active.</summary>
        /// <param name="component">Interaction owning these magnets.</param>
        /// <param name="configuration">Validated docking settings.</param>
        internal void Configure(ObjectContactModifier component, ContactMagnetSettings configuration)
        {
            // Activation ends the previous session before binding the new slots.
            Clear();
            owner = component;
            settings = configuration;
        }

        /// <summary>Advances attraction and releases items whose active lifetime ended.</summary>
        /// <param name="delta">Scaled elapsed time since the previous update.</param>
        internal void Tick(float delta)
        {
            // Docked items retain logical contact only for their original lifetime and hierarchy.
            for (int index = held.Count - 1; index >= 0; index--)
                if (!held[index].Alive)
                {
                    ContactMagnetLease lease = held[index];
                    owner.ReleaseMagnetContact(lease.Item);
                    int remaining = held.IndexOf(lease);
                    if (remaining >= 0)
                        ReleaseAt(remaining);
                }
                else
                    held[index].Tick(delta);
        }

        /// <summary>Releases all bodies after contact effects have restored their own snapshots.</summary>
        internal void Clear()
        {
            // Remove backwards so each lease restores its body exactly once.
            for (int index = held.Count - 1; index >= 0; index--)
                ReleaseAt(index);
            rejected.Clear();
        }

        /// <summary>Restores docking physics before Grab captures its carry snapshot.</summary>
        /// <param name="item">Item being picked up or leaving its active lifetime.</param>
        internal static void Release(ObjectItem item)
        {
            if (item == null || !owners.TryGetValue(item, out ContactMagnetRun run))
                return;
            // Cancel preparation first so it cannot restore a stale kinematic snapshot after pickup.
            if (run.owner != null)
                run.owner.ReleaseMagnetContact(item);
            for (int index = run.held.Count - 1; index >= 0; index--)
                if (run.held[index].Item == item)
                    run.ReleaseAt(index);
        }

        /// <summary>Ends docking for one modification without cancelling its completed effects.</summary>
        /// <param name="item">Participant released by the rule.</param>
        /// <param name="rule">Exact owner of its lease.</param>
        internal void ReleaseOwned(ObjectItem item, ContactModificationRun rule)
        {
            // Other modifications may own different slots on this same interaction.
            for (int index = held.Count - 1; index >= 0; index--)
                if (held[index].Item == item && held[index].Rule == rule)
                    ReleaseAt(index);
        }

        /// <summary>Removes one slot lease without allocating a temporary collection.</summary>
        /// <param name="index">Active lease to release.</param>
        private void ReleaseAt(int index)
        {
            // Unregister before restoring the body to keep pickup callbacks idempotent.
            ContactMagnetLease lease = held[index];
            owners.Remove(lease.Item);
            lease.Release();
            held.RemoveAt(index);
        }

        #endregion
        #region Contact

        /// <summary>Retains docked logical contacts between physical overlap polls.</summary>
        /// <param name="contacts">Reusable physical contact set for the current rule.</param>
        /// <param name="rule">Rule accepting these items before docking.</param>
        internal void Collect(HashSet<ObjectItem> contacts, ContactModificationRun rule)
        {
            if (settings is not { Enabled: true })
                return;
            // Rejection applies to this contact episode. Separation permits a fresh attempt.
            separated.Clear();
            foreach (KeyValuePair<ObjectItem, ContactModificationRun> entry in rejected)
                if (entry.Value == rule && (entry.Key == null || !contacts.Contains(entry.Key)))
                    separated.Add(entry.Key);
            foreach (ObjectItem item in separated)
                rejected.Remove(item);
            // A snap may move the item away from its initial collision surface.
            foreach (ContactMagnetLease lease in held)
                if (lease.Alive && lease.Rule == rule)
                    contacts.Add(lease.Item);
        }

        /// <summary>Waits for docking before starting preparation or modification effects.</summary>
        /// <param name="item">Reserved contact participant.</param>
        /// <returns>True when docking is disabled or this participant reached its slot.</returns>
        internal bool Ready(ObjectItem item)
        {
            // Disabled docking preserves the existing contact behaviour.
            return settings is not { Enabled: true }
                || owners.TryGetValue(item, out ContactMagnetRun run) && run == this && Find(item).Ready;
        }

        /// <summary>Prevents a modification from starting when every compatible slot is occupied.</summary>
        /// <param name="item">Contact participant to dock.</param>
        /// <param name="rule">Exact modification owning the docking lifetime.</param>
        /// <returns>True when docking is disabled or the item owns a slot in this interaction.</returns>
        internal bool Accepts(ObjectItem item, ContactModificationRun rule)
        {
            // Disabled docking preserves the existing contact behaviour.
            return settings is not { Enabled: true } || Capture(item, rule);
        }

        /// <summary>Selects the nearest free compatible slot, retaining a stable index for equal distances.</summary>
        /// <param name="item">Physically contacting item already accepted by a rule.</param>
        /// <param name="rule">Modification retaining this slot.</param>
        /// <returns>True when the item owns a slot here.</returns>
        private bool Capture(ObjectItem item, ContactModificationRun rule)
        {
            // A live lease cannot be stolen by another modification or object.
            if (owners.TryGetValue(item, out ContactMagnetRun existing))
                return existing == this && Find(item).Rule == rule;
            if (rejected.ContainsKey(item))
                return false;
            if (item.IsCarried || item.IsReserved || item.IsConsumed || !item.isActiveAndEnabled || item == owner.Item
                || owner.transform.IsChildOf(item.transform))
                return false;
            // Free compatible slots always win, ordered by priority and then world distance.
            int selected = Select(item, false);
            if (selected < 0 && settings.WhenOccupied == ContactMagnetOverflow.ShareNearestCompletion)
                selected = Select(item, true);
            if (selected < 0)
            {
                if (settings.WhenOccupied == ContactMagnetOverflow.Reject && Select(item, true) >= 0)
                    rejected[item] = rule;
                return false;
            }
            ContactMagnet magnet = settings.Magnets[selected];
            Bounds shape = ContactMagnetPlacement.Capture(item);
            Vector3 target = owner.transform.TransformPoint(magnet.Position);
            if (!placement.TryPlace(shape, target, owner.transform.rotation * Quaternion.Euler(magnet.Rotation),
                item.transform.lossyScale, held, null, out Vector3 placed))
                return false;
            Vector3 offset = owner.transform.InverseTransformVector(placed - target);
            held.Add(new ContactMagnetLease(item, owner.transform, settings, selected, rule, shape, offset, placement, held));
            owners.Add(item, this);
            return true;
        }

        /// <summary>Selects a compatible slot without letting an occupied slot outrank a free one.</summary>
        /// <param name="item">Incoming compatible item.</param>
        /// <param name="occupied">Whether this pass considers only occupied magnets.</param>
        /// <returns>Selected slot index, or -1 when none qualify.</returns>
        private int Select(ObjectItem item, bool occupied)
        {
            int selected = -1;
            int priority = int.MinValue;
            float completion = float.PositiveInfinity;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < settings.Magnets.Length; index++)
            {
                ContactMagnet magnet = settings.Magnets[index];
                if (!magnet.AnyItem && !item.Identity.Matches(magnet.Flags))
                    continue;
                bool taken = false;
                float remaining = float.PositiveInfinity;
                foreach (ContactMagnetLease lease in held)
                    if (lease.Index == index)
                    {
                        taken = true;
                        remaining = Mathf.Min(remaining, lease.Remaining);
                    }
                bool supplied = false;
                foreach (int supply in supplies.Values)
                    supplied |= supply == index;
                taken |= supplied;
                if (taken != occupied || occupied && supplied)
                    continue;
                float score = (owner.transform.TransformPoint(magnet.Position) - item.transform.position).sqrMagnitude;
                if (selected >= 0 && (occupied && remaining > completion
                    || (!occupied || remaining == completion) && (magnet.Priority < priority || magnet.Priority == priority && score >= distance)))
                    continue;
                selected = index;
                completion = remaining;
                priority = magnet.Priority;
                distance = score;
            }
            return selected;
        }

        /// <summary>Checks exact rule ownership before reporting a waiting attraction as active.</summary>
        /// <param name="item">Attracting participant.</param>
        /// <param name="rule">Modification asking about its own item.</param>
        /// <returns>True while this rule retains the participant's docking lease.</returns>
        internal bool Owns(ObjectItem item, ContactModificationRun rule)
        {
            return owners.TryGetValue(item, out ContactMagnetRun run) && run == this && Find(item).Rule == rule;
        }

        /// <summary>Resolves the lease already registered for an item.</summary>
        /// <param name="item">Registered participant.</param>
        /// <returns>Its retained docking lease.</returns>
        private ContactMagnetLease Find(ObjectItem item)
        {
            // The static owner lookup has already confirmed this item belongs here.
            foreach (ContactMagnetLease lease in held)
                if (lease.Item == item)
                    return lease;
            return null;
        }

        /// <summary>Resolves a supply origin using the same slot selection as contact docking.</summary>
        /// <param name="item">Trigger being replaced by supply portions.</param>
        /// <param name="frame">Dispenser's receiving item frame.</param>
        /// <param name="requireLease">Whether only this trigger's existing lease may qualify.</param>
        /// <param name="anchor">Receives the slot and its retained local pose.</param>
        /// <returns>True when a compatible contact slot is available.</returns>
        internal bool TrySupplyAnchor(ObjectItem item, Transform frame, bool requireLease, out ScoopDisplayAnchor anchor)
        {
            anchor = default;
            if (settings is not { Enabled: true })
                return false;
            // A lease preserves its exact selection and shared-slot clearance.
            ContactMagnetLease lease = Find(item);
            int index = lease != null ? lease.Index : requireLease ? -1 : Select(item, false);
            if (index < 0)
                return false;
            Pose pose = lease != null ? lease.TargetPose : new Pose(owner.transform.TransformPoint(settings.Magnets[index].Position),
                owner.transform.rotation * Quaternion.Euler(settings.Magnets[index].Rotation));
            anchor = new ScoopDisplayAnchor(this, index, new Pose(frame.InverseTransformPoint(pose.position), Quaternion.Inverse(frame.rotation) * pose.rotation),
                item.CaptureRecipeFlags());
            return true;
        }

        /// <summary>Retains a displayed supply's slot until its last portion leaves.</summary>
        /// <param name="display">Live supply or deferred loading transaction retaining the slot.</param>
        /// <param name="index">Contact magnet represented by this supply.</param>
        /// <param name="occupied">Whether the display still has portions.</param>
        internal void SupplyOccupancy(object display, int index, bool occupied)
        {
            // Cooked supply participates in the existing occupied-slot policy.
            if (occupied)
                supplies[display] = index;
            else
                supplies.Remove(display);
        }

        #endregion
        #endregion
    }
}
