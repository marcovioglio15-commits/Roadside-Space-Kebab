using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a spherical or oriented box region around an effect source.</summary>
    public enum GravityAreaShape { Sphere, Box }

    /// <summary>Restricts effect admission by position, with optional late entry and exit restoration.</summary>
    [Serializable]
    public sealed class GravityArea
    {
        #region Fields

        [Header("Effect Area")]
        [Tooltip("Admit only participants inside this area.")]
        public bool Enabled;
        [Tooltip("Shape tested against each rigidbody centre of mass or the player's transform position.")]
        public GravityAreaShape Shape;
        [Tooltip("Area centre relative to the source pivot.")]
        public Vector3 Offset;
        [Tooltip("Sphere radius in world metres, independent of source scale.")]
        [Range(0.1f, 50f)]
        public float Radius = 5f;
        [Tooltip("Box dimensions in world metres, independent of source scale.")]
        public Vector3 Size = Vector3.one * 10f;
        [Tooltip("Box rotation in degrees relative to the source.")]
        public Vector3 Rotation;
        [Tooltip("Admit compatible participants entering the area while the effect is active.")]
        public bool TrackEntries = true;
        [Tooltip("Scaled seconds between nonallocating area overlap queries while the effect is active.")]
        [Range(0.02f, 1f)]
        public float QueryInterval = 0.2f;
        [Tooltip("Release this effect when a participant leaves the area. Other effects retain their own suspension.")]
        public bool RestoreOutside = true;

        #endregion
        #region Methods
        #region Geometry

        /// <summary>Collects potential participants using the same pose and world dimensions as containment.</summary>
        /// <param name="owner">Effect transform defining the area's offset and orientation.</param>
        /// <param name="hits">Retained overlap buffer supplied by the caller.</param>
        /// <returns>Overlap count; a full buffer requires a larger retry to avoid omissions.</returns>
        internal int Overlap(Transform owner, Collider[] hits)
        {
            // Dimensions remain independent of scale, matching the gravity controller's existing area policy.
            Vector3 center = owner.TransformPoint(Offset);
            return Shape == GravityAreaShape.Sphere
                ? Physics.OverlapSphereNonAlloc(center, Radius, hits, ~0, QueryTriggerInteraction.Collide)
                : Physics.OverlapBoxNonAlloc(center, Size * 0.5f, hits, owner.rotation * Quaternion.Euler(Rotation), ~0, QueryTriggerInteraction.Collide);
        }

        /// <summary>Tests the same local region used by scene gizmos and late-entry queries.</summary>
        /// <param name="owner">Source defining the region's pose.</param>
        /// <param name="point">Body centre of mass or player position.</param>
        /// <returns>True for a disabled area or a point within its inclusive boundary.</returns>
        internal bool Contains(Transform owner, Vector3 point)
        {
            if (!Enabled)
                return true;
            Vector3 delta = point - owner.TransformPoint(Offset);
            if (Shape == GravityAreaShape.Sphere)
                return delta.sqrMagnitude <= Radius * Radius;
            Vector3 local = Quaternion.Inverse(owner.rotation * Quaternion.Euler(Rotation)) * delta;
            return Mathf.Abs(local.x) <= Size.x * 0.5f && Mathf.Abs(local.y) <= Size.y * 0.5f && Mathf.Abs(local.z) <= Size.z * 0.5f;
        }

        /// <summary>Rejects invalid enabled dimensions without changing authored values.</summary>
        /// <param name="warning">Receives the invalid area setting.</param>
        /// <returns>True when the region can be queried.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Gravity area needs finite offsets/rotation, positive dimensions and a positive entry query interval.";
            if (Enabled && (!InteractionValues.Finite(Offset)
                || Shape is not (GravityAreaShape.Sphere or GravityAreaShape.Box)
                || (Shape == GravityAreaShape.Sphere ? !InteractionValues.Positive(Radius)
                    : !InteractionValues.Finite(Rotation) || !InteractionValues.Finite(Size) || Size.x <= 0f || Size.y <= 0f || Size.z <= 0f)
                || TrackEntries && !InteractionValues.Positive(QueryInterval)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
