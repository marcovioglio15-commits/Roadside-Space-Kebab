using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a spherical or oriented box region around the generator.</summary>
    public enum GravityAreaShape { Sphere, Box }

    /// <summary>Restricts pulse admission by position, with optional late entry and exit restoration.</summary>
    [Serializable]
    public sealed class GravityArea
    {
        #region Fields

        [Header("Effect Area")]
        [Tooltip("Restrict both objects and affected players to this generator's area.")]
        public bool Enabled;
        [Tooltip("Shape tested against each rigidbody centre of mass or the player's transform position.")]
        public GravityAreaShape Shape;
        [Tooltip("Area centre relative to the generator pivot.")]
        public Vector3 Offset;
        [Tooltip("Sphere radius in world metres, independent of generator scale.")]
        [Range(0.1f, 50f)]
        public float Radius = 5f;
        [Tooltip("Box dimensions in world metres, independent of generator scale.")]
        public Vector3 Size = Vector3.one * 10f;
        [Tooltip("Box rotation in degrees relative to the generator.")]
        public Vector3 Rotation;
        [Tooltip("Admit compatible colliders entering the area during an active pulse.")]
        public bool TrackEntries = true;
        [Tooltip("Scaled seconds between nonallocating area overlap queries during a pulse.")]
        [Range(0.02f, 1f)]
        public float QueryInterval = 0.2f;
        [Tooltip("Restore this generator's contribution when a participant leaves the area. Other generators keep their own suspension.")]
        public bool RestoreOutside = true;

        #endregion
        #region Methods
        #region Geometry

        /// <summary>Tests the same local region used by scene gizmos and late-entry queries.</summary>
        /// <param name="owner">Generator defining the region's pose.</param>
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
