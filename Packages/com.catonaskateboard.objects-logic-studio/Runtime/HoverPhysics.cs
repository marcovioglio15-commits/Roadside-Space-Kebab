using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Performs bounded visibility queries with reused buffers and conservative overflow handling.</summary>
    internal sealed class HoverPhysics
    {
        #region State

        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[16];
        private bool overflowReported;

        #endregion

        #region Methods

        #region Queries

        /// <summary>Finds the nearest cursor intersection on the cached collider hierarchy.</summary>
        /// <param name="ray">Ray from the current cursor pixel.</param>
        /// <param name="colliders">Object colliders captured when the interaction activated.</param>
        /// <param name="cameraMask">Layers actually rendered by the observing camera.</param>
        /// <param name="distance">Camera-to-player distance plus the permitted player range.</param>
        /// <param name="point">Receives the nearest hit position.</param>
        /// <returns>True when an enabled owned collider was hit.</returns>
        internal static bool TryCursorHit(Ray ray, Collider[] colliders, int cameraMask, float distance, out Vector3 point)
        {
            // Collider.Raycast includes target triggers without making unrelated triggers obstruct sight.
            point = default;
            bool found = false;
            foreach (Collider collider in colliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy
                    && (cameraMask & (1 << collider.gameObject.layer)) != 0
                    && collider.Raycast(ray, out RaycastHit hit, distance))
                {
                    distance = hit.distance;
                    point = hit.point;
                    found = true;
                }
            return found;
        }

        /// <summary>Checks the entire sight segment while ignoring the player and the target's own colliders.</summary>
        /// <param name="observer">Camera, player and warning context.</param>
        /// <param name="target">Object whose hierarchy must not obstruct its own anchor.</param>
        /// <param name="point">Anchor or exact cursor-hit position.</param>
        /// <returns>True only when all relevant colliders are known and none blocks sight.</returns>
        internal bool HasSight(HoverObserver observer, ObjectHover target, Vector3 point)
        {
            // Hover and Grab share the same conservative obstruction policy.
            return HasSight(observer, target.transform, point, target.Settings.ObstacleMask);
        }

        /// <summary>Checks visibility for any object interaction using its own obstacle mask.</summary>
        /// <param name="observer">Camera, player and physics-scene context.</param>
        /// <param name="target">Hierarchy excluded from obstruction tests.</param>
        /// <param name="point">World point that must remain visible.</param>
        /// <param name="mask">Solid obstacle layers.</param>
        /// <returns>True when no external solid collider blocks the complete sight segment.</returns>
        internal bool HasSight(HoverObserver observer, Transform target, Vector3 point, int mask)
        {
            // Query the observer's physics scene, including projects using local physics scenes.
            PhysicsScene scene = observer.View.gameObject.scene.GetPhysicsScene();
            Vector3 origin = observer.View.transform.position;
            Vector3 segment = point - origin;
            int count = scene.OverlapSphere(origin, 0.001f, overlaps, mask, QueryTriggerInteraction.Ignore);
            if (IsFull(count, overlaps.Length, observer))
                return false;

            // Rays do not report a collider containing their origin; check that case explicitly.
            for (int index = 0; index < count; index++)
                if (Blocks(overlaps[index], observer.Player, target))
                    return false;
            if (segment.sqrMagnitude < 0.000001f)
                return true;
            count = scene.Raycast(origin, segment.normalized, hits, segment.magnitude, mask, QueryTriggerInteraction.Ignore);
            if (IsFull(count, hits.Length, observer))
                return false;

            // Results are unordered, so every returned collider must be inspected.
            for (int index = 0; index < count; index++)
                if (Blocks(hits[index].collider, observer.Player, target))
                    return false;
            return true;
        }

        #endregion

        #region Filtering

        /// <summary>Separates external occluders from the player and hovered object's own hierarchy.</summary>
        /// <param name="collider">Collider returned by the physics scene.</param>
        /// <param name="player">Tagged player root.</param>
        /// <param name="target">Hovered object root.</param>
        /// <returns>True for an external solid collider.</returns>
        private static bool Blocks(Collider collider, Transform player, Transform target)
        {
            // Transform ownership avoids runtime reflection and per-query component searches.
            return collider != null && !collider.transform.IsChildOf(player) && !collider.transform.IsChildOf(target);
        }

        /// <summary>Fails closed when a fixed buffer cannot prove that all hits were returned.</summary>
        /// <param name="count">Number of populated entries.</param>
        /// <param name="capacity">Maximum entries in the reusable array.</param>
        /// <param name="context">Observer receiving the one-time diagnostic.</param>
        /// <returns>True when the query may have omitted a blocker.</returns>
        private bool IsFull(int count, int capacity, Object context)
        {
            // No growth or allocating fallback occurs in the frame loop.
            if (count < capacity)
                return false;
            if (!overflowReported)
            {
                Debug.LogWarning("Hover visibility query buffer is full. Narrow Obstacle Mask or reduce overlapping colliders; affected labels stay hidden.", context);
                overflowReported = true;
            }
            return true;
        }

        #endregion

        #endregion
    }
}
