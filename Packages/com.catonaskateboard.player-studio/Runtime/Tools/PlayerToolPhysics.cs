using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shares non-allocating swept queries between cursor targeting and straight projectiles.</summary>
    internal sealed class PlayerToolPhysics
    {
        #region State

        private readonly Transform owner;
        private readonly PhysicsScene scene;
        private RaycastHit[] hits;
        private Collider[] overlaps;

        #endregion

        #region Properties

        /// <summary>Player root defining self exclusion and the horizontal repositioning plane.</summary>
        internal Transform Owner => owner;

        #endregion

        #region Methods

        #region Queries

        /// <summary>Retains this player's physics scene and a reusable hit buffer.</summary>
        /// <param name="player">Player hierarchy excluded from all weapon queries.</param>
        /// <param name="capacity">Maximum simultaneous pooled bullets per tool.</param>
        internal PlayerToolPhysics(Transform player, int capacity)
        {
            // Reserve room for self colliders as well as ordinary scene contacts.
            owner = player;
            scene = player.gameObject.scene.GetPhysicsScene();
            hits = new RaycastHit[Mathf.Max(64, player.GetComponentsInChildren<Collider>(true).Length + capacity + 32)];
            overlaps = new Collider[hits.Length];
        }

        /// <summary>Finds the closest allowed contact, excluding the shooter and its temporary visuals.</summary>
        /// <param name="origin">World sweep origin.</param>
        /// <param name="direction">Normalized straight travel direction.</param>
        /// <param name="distance">Maximum query distance.</param>
        /// <param name="radius">Swept sphere radius, or zero for a ray.</param>
        /// <param name="layers">Allowed collision layers.</param>
        /// <param name="triggers">Whether trigger contacts are accepted.</param>
        /// <param name="hit">Receives the nearest accepted contact.</param>
        /// <returns>True when an allowed surface was found.</returns>
        internal bool Cast(Vector3 origin, Vector3 direction, float distance, float radius, LayerMask layers,
            bool triggers, out RaycastHit hit)
        {
            // Pooled visuals share the player as an explicit owner without requiring scene-layer changes.
            QueryTriggerInteraction query = triggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            int count;
            // Grow only on saturation, then retain the larger buffer for all later shots.
            while (true)
            {
                count = radius > 0f ? scene.SphereCast(origin, radius, direction, hits, distance, layers, query)
                    : scene.Raycast(origin, direction, hits, distance, layers, query);
                if (count < hits.Length)
                    break;
                System.Array.Resize(ref hits, hits.Length * 2);
            }
            hit = default;
            float closest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                Transform target = hits[index].collider.transform;
                if (hits[index].distance >= closest || target.IsChildOf(owner))
                    continue;
                closest = hits[index].distance;
                hit = hits[index];
            }
            return closest < float.PositiveInfinity;
        }

        /// <summary>Tests a visual clearance sphere while ignoring the firing player and trigger-only geometry.</summary>
        /// <param name="position">World centre.</param>
        /// <param name="radius">World clearance radius.</param>
        /// <returns>True when a solid outside the player overlaps the volume.</returns>
        internal bool Overlaps(Vector3 position, float radius)
        {
            // Retain a larger buffer only when a crowded query saturates it.
            int count;
            while (true)
            {
                count = scene.OverlapSphere(position, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                if (count < overlaps.Length)
                    break;
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            return HasObstacle(count);
        }

        /// <summary>Tests an oriented effect envelope against all solid scene layers.</summary>
        /// <param name="position">World box centre.</param>
        /// <param name="extents">Positive half dimensions.</param>
        /// <param name="rotation">World box orientation.</param>
        /// <returns>True when an unrelated solid overlaps the box.</returns>
        internal bool Overlaps(Vector3 position, Vector3 extents, Quaternion rotation)
        {
            int count;
            // The same cached overlap buffer serves arm and impact placement.
            while (true)
            {
                count = scene.OverlapBox(position, extents, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
                if (count < overlaps.Length)
                    break;
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            return HasObstacle(count);
        }

        /// <summary>Checks both endpoints and the full barrel sweep, including origins already inside a collider.</summary>
        /// <param name="start">World arm pivot.</param>
        /// <param name="end">Predicted world muzzle.</param>
        /// <param name="radius">World clearance radius.</param>
        /// <returns>True when the complete segment is free of unrelated solids.</returns>
        internal bool Clear(Vector3 start, Vector3 end, float radius)
        {
            // Sphere casts alone do not report their initial overlaps.
            Vector3 segment = end - start;
            return !Overlaps(start, radius) && !Overlaps(end, radius)
                && (segment.sqrMagnitude < 0.000001f || !Cast(start, segment.normalized, segment.magnitude, radius, ~0, false, out _));
        }

        /// <summary>Filters the completed overlap query without allocating an enumerable.</summary>
        /// <param name="count">Number of written collider entries.</param>
        /// <returns>True when at least one solid is not owned by the player.</returns>
        private bool HasObstacle(int count)
        {
            // Owned colliders cannot force an arm or its impact effects away from the player.
            for (int index = 0; index < count; index++)
                if (overlaps[index] != null && !overlaps[index].transform.IsChildOf(owner))
                    return true;
            return false;
        }

        #endregion

        #endregion
    }
}
