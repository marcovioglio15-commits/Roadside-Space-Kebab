using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Caches one collider's geometry relative to the grab body for allocation-free carry queries.</summary>
    internal readonly struct CarryShape
    {
        #region Fields

        internal readonly Collider Collider;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 center;
        private readonly Vector3 extents;
        private readonly Vector3 segment;
        private readonly float radius;
        private readonly float inset;

        #endregion

        #region Properties

        /// <summary>Whether the cached shape currently contributes solid collision geometry.</summary>
        internal bool Active => Collider != null && Collider.enabled && !Collider.isTrigger && Collider.gameObject.activeInHierarchy;

        /// <summary>Conservative distance from the body origin to this shape's farthest point.</summary>
        internal float Reach => position.magnitude + center.magnitude + extents.magnitude;

        #endregion

        #region Methods

        #region Geometry

        /// <summary>Captures scale and compound offsets once when pickup begins.</summary>
        /// <param name="collider">Supported solid collider belonging to the body.</param>
        /// <param name="body">Root rigidbody owning the carry pose.</param>
        internal CarryShape(Collider collider, Rigidbody body)
        {
            // All cached offsets use metres; parenting and authored scale remain unchanged.
            Collider = collider;
            position = Quaternion.Inverse(body.rotation) * (collider.transform.position - body.position);
            rotation = Quaternion.Inverse(body.rotation) * collider.transform.rotation;
            Vector3 scale = collider.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 localCenter = Vector3.zero;
            extents = segment = Vector3.zero;
            radius = 0f;
            switch (collider)
            {
                case BoxCollider box:
                    localCenter = box.center;
                    extents = Vector3.Scale(box.size * 0.5f, scale);
                    break;
                case SphereCollider sphere:
                    localCenter = sphere.center;
                    radius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);
                    extents = Vector3.one * radius;
                    break;
                case CapsuleCollider capsule:
                    localCenter = capsule.center;
                    radius = capsule.radius * Mathf.Max(scale[(capsule.direction + 1) % 3], scale[(capsule.direction + 2) % 3]);
                    segment = (capsule.direction switch { 0 => Vector3.right, 1 => Vector3.up, _ => Vector3.forward })
                        * Mathf.Max(0f, capsule.height * scale[capsule.direction] * 0.5f - radius);
                    extents = Vector3.one * radius + segment;
                    break;
                case MeshCollider { sharedMesh: not null } mesh:
                    // A convex mesh uses a conservative oriented box for continuous translation casts.
                    localCenter = mesh.sharedMesh.bounds.center;
                    extents = Vector3.Scale(mesh.sharedMesh.bounds.extents, scale);
                    break;
            }
            center = Quaternion.Inverse(collider.transform.rotation)
                * (collider.transform.TransformPoint(localCenter) - collider.transform.position);
            // A small query core avoids PhysX's synthetic initial-overlap normals at resting contact.
            inset = Mathf.Min(0.001f, Mathf.Min(extents.x, extents.y, extents.z) * 0.01f);
        }

        /// <summary>Tests a translated shape without moving its actual collider.</summary>
        /// <param name="pose">Proposed body pose.</param>
        /// <param name="direction">Normalized travel direction.</param>
        /// <param name="distance">Requested sweep distance including clearance.</param>
        /// <param name="hits">Reusable result buffer.</param>
        /// <returns>The number of hits, or the buffer length when results may be truncated.</returns>
        internal int Cast(Pose pose, Vector3 direction, float distance, RaycastHit[] hits)
        {
            // Sweep a slightly inset core; the solver restores its support distance along the hit normal.
            Quaternion orientation = pose.rotation * rotation;
            Vector3 origin = pose.position + pose.rotation * position + orientation * center;
            return Collider switch
            {
                SphereCollider => Physics.SphereCastNonAlloc(origin, radius - inset, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore),
                CapsuleCollider => Physics.CapsuleCastNonAlloc(origin + orientation * segment, origin - orientation * segment,
                    radius - inset, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore),
                _ => Physics.BoxCastNonAlloc(origin, extents - Vector3.one * inset, direction, hits, orientation, distance, ~0, QueryTriggerInteraction.Ignore)
            };
        }

        /// <summary>Restores the support distance removed from a sweep to keep its origin outside numerical contact.</summary>
        /// <param name="pose">Body orientation used by the sweep.</param>
        /// <param name="normal">Surface normal returned by the sweep.</param>
        /// <returns>Support distance removed along the contact normal.</returns>
        internal float Inset(Pose pose, Vector3 normal)
        {
            // Boxes lose support on each local axis; round shapes lose only their radius inset.
            if (Collider is SphereCollider or CapsuleCollider)
                return inset;
            Vector3 local = Quaternion.Inverse(pose.rotation * rotation) * normal;
            return inset * (Mathf.Abs(local.x) + Mathf.Abs(local.y) + Mathf.Abs(local.z));
        }

        /// <summary>Finds potential penetrations for a proposed rotation or external moving obstacle.</summary>
        /// <param name="pose">Proposed body pose.</param>
        /// <param name="overlaps">Reusable collider result buffer.</param>
        /// <returns>The number of nearby colliders.</returns>
        internal int Overlap(Pose pose, Collider[] overlaps)
        {
            // A sphere encloses every supported shape regardless of its rotation.
            return Physics.OverlapSphereNonAlloc(pose.position + pose.rotation * (position + rotation * center),
                extents.magnitude + 0.002f, overlaps, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Uses the exact collider shape to reject or separate a proposed pose.</summary>
        /// <param name="pose">Proposed body pose.</param>
        /// <param name="other">Potential world contact.</param>
        /// <param name="direction">Receives the minimum separation direction.</param>
        /// <param name="distance">Receives penetration depth in metres.</param>
        /// <returns>True when the shapes overlap.</returns>
        internal bool Penetrates(Pose pose, Collider other, out Vector3 direction, out float distance)
        {
            // ComputePenetration accepts virtual poses and does not depend on moving the body first.
            return Physics.ComputePenetration(Collider, pose.position + pose.rotation * position, pose.rotation * rotation,
                other, other.transform.position, other.transform.rotation, out direction, out distance);
        }

        #endregion

        #endregion
    }
}
