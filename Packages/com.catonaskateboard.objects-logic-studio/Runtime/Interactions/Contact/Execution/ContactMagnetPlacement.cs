using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Finds close disjoint docking poses with reusable conservative collider bounds.</summary>
    internal sealed class ContactMagnetPlacement
    {
        #region State

        private const float Clearance = 0.005f;
        private const int SearchLimit = 128;
        private readonly List<Vector3> candidates = new List<Vector3>(SearchLimit);

        #endregion
        #region Methods
        #region Geometry

        /// <summary>Caches an item-local volume enclosing all active owned collision shapes.</summary>
        /// <param name="item">Item about to be docked.</param>
        /// <returns>Conservative collision volume in item-local coordinates.</returns>
        internal static Bounds Capture(ObjectItem item)
        {
            // The cache is built once per acquisition, including compound items.
            Bounds bounds = new Bounds();
            bool found = false;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>())
                if (collider.enabled && item.Owns(collider.transform))
                {
                    Bounds local = TransformBounds(collider.bounds, item.transform.worldToLocalMatrix);
                    if (found)
                        bounds.Encapsulate(local);
                    else
                    {
                        bounds = local;
                        found = true;
                    }
                }
            return found ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.01f);
        }

        /// <summary>Projects all bounds corners so rotation and nonuniform scale cannot reduce clearance.</summary>
        /// <param name="bounds">Original enclosing volume.</param>
        /// <param name="matrix">Transformation into the destination space.</param>
        /// <returns>Axis-aligned destination volume enclosing every transformed corner.</returns>
        internal static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            return result;
        }

        /// <summary>Searches nearest candidate offsets around occupied target and current volumes.</summary>
        /// <param name="shape">Item-local volume.</param>
        /// <param name="position">Preferred world pivot.</param>
        /// <param name="rotation">Proposed world orientation.</param>
        /// <param name="scale">Unchanged world scale.</param>
        /// <param name="held">Existing docking operations.</param>
        /// <param name="self">Lease excluded while correcting its own motion.</param>
        /// <param name="placed">Receives the closest tested disjoint world pivot.</param>
        /// <returns>True when a disjoint placement was found within the bounded search.</returns>
        internal bool TryPlace(Bounds shape, Vector3 position, Quaternion rotation, Vector3 scale,
            List<ContactMagnetLease> held, ContactMagnetLease self, out Vector3 placed)
        {
            // Best-first expansion stays allocation-free after the fixed buffers are created.
            candidates.Clear();
            candidates.Add(position);
            for (int head = 0; head < candidates.Count && head < SearchLimit; head++)
            {
                int nearest = head;
                for (int index = head + 1; index < candidates.Count; index++)
                    if ((candidates[index] - position).sqrMagnitude < (candidates[nearest] - position).sqrMagnitude)
                        nearest = index;
                (candidates[head], candidates[nearest]) = (candidates[nearest], candidates[head]);
                Vector3 candidate = candidates[head];
                Bounds bounds = TransformBounds(shape, Matrix4x4.TRS(candidate, rotation, scale));
                bool clear = true;
                foreach (ContactMagnetLease lease in held)
                {
                    if (lease == self || !lease.Alive)
                        continue;
                    Bounds obstacle = lease.TargetBounds;
                    if (!bounds.Intersects(obstacle))
                        obstacle = lease.CurrentBounds;
                    if (!bounds.Intersects(obstacle))
                        continue;
                    clear = false;
                    Expand(candidate, bounds, obstacle);
                }
                if (!clear)
                    continue;
                placed = candidate;
                return true;
            }
            placed = position;
            return false;
        }

        /// <summary>Proposes adjacent faces without exceeding the fixed search budget.</summary>
        /// <param name="position">Current candidate pivot.</param>
        /// <param name="bounds">Candidate collision volume.</param>
        /// <param name="obstacle">Intersecting occupied volume.</param>
        private void Expand(Vector3 position, Bounds bounds, Bounds obstacle)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 candidate = position;
                    candidate[axis] += side < 0 ? obstacle.min[axis] - bounds.max[axis] - Clearance
                        : obstacle.max[axis] - bounds.min[axis] + Clearance;
                    if (candidates.Count < SearchLimit && !candidates.Contains(candidate))
                        candidates.Add(candidate);
                }
        }

        #endregion
        #endregion
    }
}
