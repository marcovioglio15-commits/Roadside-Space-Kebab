using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Animates one docking pose without reparenting and retains its body policy until release.</summary>
    internal sealed class ContactMagnetLease
    {
        #region State

        internal readonly ObjectItem Item;
        internal readonly int Index;
        internal readonly ContactModificationRun Rule;
        private readonly Transform owner;
        private readonly Transform parent;
        private readonly Rigidbody body;
        private readonly CarryBodyState physics;
        private readonly ContactMagnetSettings settings;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly int spawn;
        private readonly Bounds shape;
        private readonly Vector3 scale;
        private readonly Vector3 offset;
        private readonly ContactMagnetPlacement placement;
        private readonly System.Collections.Generic.List<ContactMagnetLease> held;
        private Matrix4x4 frame;
        private Matrix4x4 parentFrame;
        private float elapsed;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Whether this lease still belongs to the same active item lifetime.</summary>
        internal bool Alive => Item != null && Item.isActiveAndEnabled && !Item.IsConsumed && Item.Spawn.Index == spawn
            && owner != null && Item.transform.parent == parent;
        /// <summary>Whether attraction reached the docking pose.</summary>
        internal bool Ready => ready;
        /// <summary>Local collision volume cached at acquisition.</summary>
        internal Bounds Shape => shape;
        /// <summary>Reserved final collision volume used by other docking operations.</summary>
        internal Bounds TargetBounds => ContactMagnetPlacement.TransformBounds(shape, Matrix4x4.TRS(TargetPosition, TargetRotation, scale));
        /// <summary>Current collision volume used to keep attraction paths disjoint.</summary>
        internal Bounds CurrentBounds => ContactMagnetPlacement.TransformBounds(shape, Item.transform.localToWorldMatrix);
        /// <summary>Estimated seconds before this slot's current item finishes.</summary>
        internal float Remaining => (ready || settings.Instant ? 0f : Mathf.Max(0f, settings.Duration - elapsed)) + Rule.Remaining(Item);
        /// <summary>Reserved item pivot following the owner's current frame.</summary>
        private Vector3 TargetPosition => owner.TransformPoint(settings.Magnets[Index].Position + offset);
        /// <summary>Item orientation following the selected slot.</summary>
        private Quaternion TargetRotation => owner.rotation * Quaternion.Euler(settings.Magnets[Index].Rotation);

        #endregion
        #region Methods
        #region Motion

        /// <summary>Captures body state once and starts attraction without creating runtime components.</summary>
        /// <param name="item">Accepted contact participant.</param>
        /// <param name="root">Owner defining the local slot pose.</param>
        /// <param name="configuration">Attraction timing and configured slots.</param>
        /// <param name="index">Claimed free slot.</param>
        /// <param name="rule">Modification whose end releases this slot.</param>
        /// <param name="shape">Cached item-local collision volume.</param>
        /// <param name="offset">Owner-local offset keeping shared occupants separate.</param>
        /// <param name="placement">Reusable collision-free placement search.</param>
        /// <param name="held">Other active docking operations.</param>
        internal ContactMagnetLease(ObjectItem item, Transform root, ContactMagnetSettings configuration, int index, ContactModificationRun rule, Bounds shape, Vector3 offset, ContactMagnetPlacement placement, System.Collections.Generic.List<ContactMagnetLease> held)
        {
            // Cache the original frame and body policy once at acquisition.
            this.shape = shape;
            this.offset = offset;
            this.placement = placement;
            this.held = held;
            scale = item.transform.lossyScale;
            Rule = rule;
            Item = item;
            Index = index;
            owner = root;
            settings = configuration;
            parent = item.transform.parent;
            position = item.transform.position;
            rotation = item.transform.rotation;
            spawn = item.Spawn.Index;
            body = item.GetComponent<Rigidbody>();
            if (body != null)
            {
                physics = new CarryBodyState(body);
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
                body.useGravity = false;
            }
            Tick(0f);
        }

        /// <summary>Follows a moving owner and skips settled pose writes while its frame remains unchanged.</summary>
        /// <param name="delta">Scaled elapsed time since the previous update.</param>
        internal void Tick(float delta)
        {
            // Settled items require no transform writes until the owner moves.
            if (ready && frame == owner.localToWorldMatrix && parentFrame == (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity))
                return;
            elapsed += delta;
            float progress = settings.Instant ? 1f : Mathf.Clamp01(elapsed / settings.Duration);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            Quaternion orientation = Quaternion.Slerp(rotation, TargetRotation, blend);
            if (!placement.TryPlace(shape, Vector3.Lerp(position, TargetPosition, blend), orientation, scale, held, this, out Vector3 placed))
                return;
            Item.transform.SetPositionAndRotation(placed, orientation);
            if (progress < 1f || (placed - TargetPosition).sqrMagnitude > 0.000001f)
                return;
            frame = owner.localToWorldMatrix;
            parentFrame = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
            ready = true;
        }

        /// <summary>Restores original physics without changing hierarchy during disable callbacks.</summary>
        internal void Release()
        {
            // Despawn can destroy the native body before its owner clears the lease.
            if (Item == null)
                return;
            if (body != null && Item.Spawn.Index == spawn)
                physics.Restore(body, false);
        }

        #endregion
        #endregion
    }
}
