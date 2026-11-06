using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Exposes carry state separately from whether a collider can receive a weapon hit.</summary>
    public interface IPlayerToolShotOwner
    {
        /// <summary>Whether a Grab currently owns the body, including a physical pickup in progress.</summary>
        bool IsCarried { get; }
    }

    /// <summary>Shares non-allocating swept queries between cursor targeting and straight projectiles.</summary>
    internal sealed class PlayerToolPhysics
    {
        #region State

        private readonly Transform owner;
        private readonly PhysicsScene scene;
        private RaycastHit[] hits;
        private Collider[] overlaps;
        private PlayerToolImpactForce impactForce;
        private List<PlayerToolAreaContact> areaContacts;

        #endregion

        #region Properties

        /// <summary>Player root defining self exclusion and the horizontal repositioning plane.</summary>
        internal Transform Owner => owner;

        #endregion

        #region Methods

        #region Queries

        /// <summary>Applies force and sends all contacts from one impact in distance order.</summary>
        /// <param name="settings">Weapon collision, area and force settings.</param>
        /// <param name="hit">Primary contact that stopped the shot.</param>
        /// <param name="layers">Layers used by the shot and its area.</param>
        internal void DispatchImpact(PlayerToolWeaponSettings settings, PlayerToolHit hit, LayerMask layers)
        {
            // Collect before callbacks can move or replace colliders. Disabled features need no new buffers.
            bool area = settings.ImpactAreaEnabled;
            if (area)
                CollectArea(hit.Contact, settings.ImpactAreaRadius, layers, settings.HitTriggers);
            if (settings.SimulatedImpactForce is { Enabled: true } force)
            {
                impactForce ??= new PlayerToolImpactForce();
                impactForce.Begin();
                impactForce.Apply(force, hit.Contact.collider, hit.Contact.point, hit.Direction, 1f, false);
                if (area)
                    foreach (PlayerToolAreaContact target in areaContacts)
                    {
                        if (target.Collider == null || !target.Collider.enabled || !target.Collider.gameObject.activeInHierarchy)
                            continue;
                        // Surface distance controls falloff, while the collider centre determines the outward direction.
                        Vector3 away = (target.Collider.bounds.center - hit.Contact.point).normalized;
                        float strength = Mathf.Lerp(1f, 0.35f, target.Distance / settings.ImpactAreaRadius);
                        impactForce.Apply(force, target.Collider, target.Point, away.sqrMagnitude > 0f ? away : hit.Direction, strength, true);
                    }
            }
            PlayerTools.PublishImpact(hit, area ? areaContacts : null);
            areaContacts?.Clear();
        }

        /// <summary>Samples nearby exposed colliders without passing through solid cover.</summary>
        /// <param name="primary">Direct hit supplying the centre and outward surface normal.</param>
        /// <param name="radius">Maximum distance from the primary impact point.</param>
        /// <param name="layers">Collision layers used by this shot.</param>
        /// <param name="triggers">Whether trigger volumes can receive nearby contacts.</param>
        private void CollectArea(RaycastHit primary, float radius, LayerMask layers, bool triggers)
        {
            areaContacts ??= new List<PlayerToolAreaContact>(16);
            areaContacts.Clear();
            QueryTriggerInteraction query = triggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            int count;
            // Keep a larger buffer only after an overlap query saturates it.
            while ((count = scene.OverlapSphere(primary.point, radius, overlaps, layers, query)) == overlaps.Length)
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            Vector3 start = primary.point + primary.normal * 0.025f;
            // Start just outside the impact surface so the same wall can still shield its far side.
            for (int index = 0; index < count; index++)
            {
                Collider candidate = overlaps[index];
                if (candidate == null || candidate == primary.collider || candidate.transform.IsChildOf(owner))
                    continue;
                Vector3 point = ClosestAreaPoint(candidate, primary.point);
                if (Covered(start, point, layers, candidate))
                {
                    // A partially exposed shape can still be reached on the near side of the blast.
                    point = ClosestAreaPoint(candidate, primary.point + primary.normal * (radius * 0.6f));
                    if ((point - primary.point).sqrMagnitude > radius * radius || Covered(start, point, layers, candidate))
                        continue;
                }
                areaContacts.Add(new PlayerToolAreaContact(candidate, point, Vector3.Distance(primary.point, point)));
            }
            areaContacts.Sort(CompareAreaContacts);
        }

        /// <summary>Samples the actual shape where supported, and uses bounds for concave scene geometry.</summary>
        /// <param name="candidate">Collider from the overlap query.</param>
        /// <param name="origin">Blast sample seeking the nearest surface.</param>
        /// <returns>World point to check for exposure.</returns>
        private static Vector3 ClosestAreaPoint(Collider candidate, Vector3 origin)
        {
            return candidate is BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true }
                ? candidate.ClosestPoint(origin) : candidate.bounds.ClosestPoint(origin);
        }

        /// <summary>Checks for another solid between the impact and the sampled target surface.</summary>
        /// <param name="start">World point just outside the struck surface.</param>
        /// <param name="point">Sampled point on the nearby target.</param>
        /// <param name="layers">Solid layers the weapon can hit.</param>
        /// <param name="candidate">Target allowed to stop its own exposure ray.</param>
        /// <returns>True when an unrelated collider shields this point.</returns>
        private bool Covered(Vector3 start, Vector3 point, LayerMask layers, Collider candidate)
        {
            Vector3 segment = point - start;
            float length = segment.magnitude;
            return length > 0.015f && Cast(start, segment / length, length - 0.005f, 0f, layers, false, out RaycastHit cover)
                && cover.collider != candidate && (candidate.attachedRigidbody == null || cover.rigidbody != candidate.attachedRigidbody);
        }

        /// <summary>Orders the nearest exposed surfaces first, with stable ties for compound objects.</summary>
        /// <param name="first">First candidate surface.</param>
        /// <param name="second">Second candidate surface.</param>
        /// <returns>Comparison by distance, then by collider identity.</returns>
        private static int CompareAreaContacts(PlayerToolAreaContact first, PlayerToolAreaContact second)
        {
            int distance = first.Distance.CompareTo(second.Distance);
            return distance != 0 ? distance : EntityId.ToULong(first.Collider.GetEntityId()).CompareTo(EntityId.ToULong(second.Collider.GetEntityId()));
        }

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

        /// <summary>Finds the closest surface outside the player hierarchy, including carried targets.</summary>
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
                if (hits[index].distance >= closest || hits[index].collider.transform.IsChildOf(owner))
                    continue;
                closest = hits[index].distance;
                hit = hits[index];
            }
            return closest < float.PositiveInfinity;
        }

        /// <summary>Finds the closest cursor contact, including the held shape when it has no world collisions.</summary>
        /// <param name="ray">Current camera cursor ray.</param>
        /// <param name="distance">Maximum distance along the ray.</param>
        /// <param name="layers">Layers shared by the camera and the weapon.</param>
        /// <param name="triggers">Whether trigger contacts are eligible.</param>
        /// <param name="hit">Receives the nearest contact.</param>
        /// <returns>True when a world or carried surface was hit.</returns>
        internal bool CastPointer(Ray ray, float distance, LayerMask layers, bool triggers, out RaycastHit hit)
        {
            bool found = Cast(ray.origin, ray.direction, distance, 0f, layers, triggers, out hit);
            Collider[] carried = PlayerTools.QueryCarriedTargetShapes(owner);
            if (carried == null)
                return found;
            // An existing world hit caps the direct shape query so the fallback respects solid cover.
            float closest = found ? hit.distance : distance;
            foreach (Collider shape in carried)
                if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy && (triggers || !shape.isTrigger)
                    && (layers.value & (1 << shape.gameObject.layer)) != 0 && shape.Raycast(ray, out RaycastHit contact, closest))
                {
                    hit = contact;
                    closest = contact.distance;
                    found = true;
                }
            return found;
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
