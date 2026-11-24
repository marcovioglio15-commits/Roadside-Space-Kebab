using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Measures contact with static concave surfaces through virtual convex poses.</summary>
    internal static class ContactMeshProximity
    {
        #region State

        private static readonly Vector3[] directions = CreateDirections();

        #endregion
        #region Methods
        #region Geometry

        /// <summary>Checks concave contact without calling unsupported closest-point queries or moving colliders.</summary>
        /// <param name="first">First contact surface.</param>
        /// <param name="second">Second contact surface.</param>
        /// <param name="tolerance">Maximum virtual translation allowed to establish contact.</param>
        /// <returns>True when a supported convex participant overlaps the surface within tolerance.</returns>
        internal static bool Touches(Collider first, Collider second, float tolerance)
        {
            // PhysX requires at least one convex participant for penetration queries.
            if (first is MeshCollider { convex: false })
                (first, second) = (second, first);
            if (first is MeshCollider { convex: false })
                return false;
            if (Overlaps(first, second, Vector3.zero))
                return true;
            if (tolerance <= 0f)
                return false;
            // All probes remain inside the allowed distance and never modify live transforms.
            foreach (Vector3 direction in directions)
                if (Overlaps(first, second, direction * tolerance))
                    return true;
            return false;
        }

        /// <summary>Tests one virtual translation of the convex participant.</summary>
        /// <param name="first">Convex participant.</param>
        /// <param name="second">Static or kinematic concave surface.</param>
        /// <param name="offset">Virtual world translation.</param>
        /// <returns>True when the two exact shapes overlap.</returns>
        private static bool Overlaps(Collider first, Collider second, Vector3 offset)
        {
            return Physics.ComputePenetration(first, first.transform.position + offset, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out _);
        }

        /// <summary>Caches axis, edge and corner directions once for tolerance probes.</summary>
        /// <returns>Unit directions covering the surrounding cube.</returns>
        private static Vector3[] CreateDirections()
        {
            Vector3[] result = new Vector3[26];
            int index = 0;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0)
                            result[index++] = new Vector3(x, y, z).normalized;
            return result;
        }

        #endregion
        #endregion
    }
}
