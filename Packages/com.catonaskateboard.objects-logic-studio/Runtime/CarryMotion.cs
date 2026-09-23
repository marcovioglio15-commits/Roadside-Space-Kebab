using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves frame-aligned carry motion against the world without injecting solver velocities.</summary>
    internal sealed class CarryMotion
    {
        #region State

        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[64];
        private CarryShape[] shapes;
        private Rigidbody body;
        private Transform player;
        private Vector3 contactRotation;
        private Vector3 contactTarget;
        private Vector3 contactNormal;
        private float contactClearTime;
        private float reach;
        private Vector3 previousTarget;
        private bool tracking;
        private const float separationMargin = 0.0001f;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Rebuilds geometry at pickup so prefab collider replacements need no stored collider binding.</summary>
        /// <param name="owner">Grabbed body.</param>
        /// <param name="colliders">Colliders currently owned by that body.</param>
        /// <param name="observer">Player root excluded from world contact.</param>
        internal void Bind(Rigidbody owner, Collider[] colliders, Transform observer)
        {
            // Geometry allocations belong to pickup, never to the render or physics loop.
            body = owner;
            player = observer;
            contactRotation = Vector3.zero;
            contactTarget = contactNormal = Vector3.zero;
            contactClearTime = 0f;
            reach = 0f;
            tracking = false;
            shapes = new CarryShape[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                shapes[index] = new CarryShape(colliders[index], body);
                reach = Mathf.Max(reach, shapes[index].Reach);
            }
        }

        #endregion

        #region Motion

        /// <summary>Slides along contact planes and admits rotation only while the object stays clear.</summary>
        /// <param name="target">Requested position and orientation after pickup easing.</param>
        /// <param name="settings">Validated carry limits and clearance.</param>
        /// <param name="deltaTime">Elapsed render time.</param>
        /// <param name="instant">Allow the full pickup distance while retaining collision sweeps.</param>
        /// <returns>A collision-constrained world pose.</returns>
        internal Pose Resolve(Pose target, GrabSettings settings, float deltaTime, bool instant)
        {
            // Synchronize scripted obstacles once for the held item before running its virtual-pose queries.
            Physics.SyncTransforms();
            Pose pose = new Pose(body.position, body.rotation);
            // Follow this frame's anchor displacement directly, easing only the error retained from earlier contact.
            Vector3 remaining = tracking && !instant ? target.position - previousTarget
                + (previousTarget - pose.position) * (1f - Mathf.Exp(-settings.RecoveryResponse * deltaTime)) : target.position - pose.position;
            previousTarget = target.position;
            tracking = true;
            float budget = instant ? float.PositiveInfinity : settings.FollowSpeed * deltaTime;
            Vector3 origin = pose.position;
            if (!Separate(ref pose, ref budget))
                return pose;
            remaining = Vector3.ClampMagnitude(remaining - (pose.position - origin), budget);
            contactClearTime -= deltaTime;
            // A bounded slide loop handles floors, walls and corners without repeated velocity impulses.
            for (int pass = 0; pass < 3 && remaining.sqrMagnitude > 0.00000001f; pass++)
            {
                float distance = remaining.magnitude;
                float travel = Sweep(pose, remaining / distance, distance, settings.CollisionPadding,
                    out Vector3 normal, out Vector3 point);
                pose.position += remaining * (travel / distance);
                budget = Mathf.Max(0f, budget - travel);
                if (travel >= distance)
                    break;
                if (settings.ContactRotation && pass == 0)
                {
                    // Keep one response per contact plane; changing sweep hit points must not retrigger an impact every frame.
                    if (contactClearTime <= 0f || Vector3.Dot(normal, contactNormal) < 0.95f)
                        contactTarget = Vector3.ClampMagnitude(Vector3.Cross(point - pose.position, normal) / Mathf.Max(reach, 0.001f)
                            * settings.ContactAngle * Mathf.Clamp01((distance - travel) / Mathf.Max(deltaTime * settings.FollowSpeed, 0.001f)),
                            settings.ContactAngle);
                    contactNormal = normal;
                    contactClearTime = Mathf.Max(0.05f, 1f / settings.ContactResponse);
                }
                remaining = Vector3.ProjectOnPlane(remaining * (1f - travel / distance), normal);
            }
            // Intermediate orientations stop long objects from rotating through thin walls.
            if (contactClearTime <= 0f)
                contactTarget = Vector3.zero;
            contactRotation = settings.ContactRotation
                ? Vector3.Lerp(contactRotation, contactTarget, 1f - Mathf.Exp(-settings.ContactResponse * deltaTime)) : Vector3.zero;
            Quaternion rotation = Quaternion.RotateTowards(pose.rotation,
                Quaternion.Euler(contactRotation) * target.rotation, settings.RotationSpeed * deltaTime);
            float angle = Quaternion.Angle(pose.rotation, rotation);
            if (angle <= 0.001f)
                return pose;
            int steps = Mathf.Max(1, Mathf.CeilToInt(angle / 2f));
            Quaternion start = pose.rotation;
            // One correction budget covers the entire rotation, proportional to the actual surface travel.
            budget = Mathf.Min(budget, reach * angle * Mathf.Deg2Rad + separationMargin * steps);
            for (int step = 1; step <= steps; step++)
            {
                Pose candidate = new Pose(pose.position, Quaternion.Slerp(start, rotation, (float)step / steps));
                // Reapplying the full sweep padding at every tiny rotation causes visible staircase motion.
                if (!Separate(ref candidate, ref budget))
                    break;
                pose = candidate;
            }
            return pose;
        }

        /// <summary>Finds the nearest opposing contact across the compound body.</summary>
        /// <param name="pose">Current virtual body pose.</param>
        /// <param name="direction">Normalized requested travel.</param>
        /// <param name="distance">Requested travel length.</param>
        /// <param name="padding">Clearance kept before contact.</param>
        /// <param name="normal">Receives the nearest blocking surface normal.</param>
        /// <param name="point">Receives the world contact point used for bounded impact rotation.</param>
        /// <returns>Allowed travel distance.</returns>
        private float Sweep(Pose pose, Vector3 direction, float distance, float padding, out Vector3 normal, out Vector3 point)
        {
            // Full buffers stop travel conservatively instead of discarding an unknown nearer obstacle.
            normal = -direction;
            point = pose.position;
            float allowed = distance;
            foreach (CarryShape shape in shapes)
            {
                if (!shape.Active)
                    continue;
                int count = shape.Cast(pose, direction, distance + padding, hits);
                if (count == hits.Length)
                    return 0f;
                for (int index = 0; index < count; index++)
                    if (Blocks(shape.Collider, hits[index].collider) && Vector3.Dot(direction, hits[index].normal) < -0.0001f
                        && hits[index].distance - padding < allowed)
                    {
                        allowed = Mathf.Max(0f, hits[index].distance - padding);
                        normal = hits[index].normal;
                        point = hits[index].point;
                    }
            }
            return allowed;
        }

        /// <summary>Recovers contact caused by an obstacle moving into the stationary held object.</summary>
        /// <param name="pose">Virtual pose receiving bounded separation corrections.</param>
        /// <param name="budget">Remaining translation distance shared by all corrections in this frame.</param>
        /// <returns>True when the corrected pose is clear; false when recovery needs more time or space.</returns>
        private bool Separate(ref Pose pose, ref float budget)
        {
            // Multiple passes account for compounds and corners without an unbounded solver loop.
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                foreach (CarryShape shape in shapes)
                {
                    if (!shape.Active)
                        continue;
                    int count = shape.Overlap(pose, overlaps);
                    if (count == overlaps.Length)
                        return false;
                    for (int index = 0; index < count; index++)
                        if (Blocks(shape.Collider, overlaps[index])
                            && shape.Penetrates(pose, overlaps[index], out Vector3 direction, out float distance))
                        {
                            float correction = Mathf.Min(distance + separationMargin, budget);
                            pose.position += direction * correction;
                            budget -= correction;
                            if (budget <= 0f)
                                return Clear(pose);
                            moved = true;
                        }
                }
                if (!moved)
                    return true;
            }
            return Clear(pose);
        }

        /// <summary>Rejects any orientation that intersects solid world geometry.</summary>
        /// <param name="pose">Candidate position and rotation.</param>
        /// <returns>True when all active shapes are clear.</returns>
        private bool Clear(Pose pose)
        {
            // Exact overlap tests retain usable space around convex meshes after conservative translation.
            foreach (CarryShape shape in shapes)
            {
                if (!shape.Active)
                    continue;
                int count = shape.Overlap(pose, overlaps);
                if (count == overlaps.Length)
                    return false;
                for (int index = 0; index < count; index++)
                    if (Blocks(shape.Collider, overlaps[index]) && shape.Penetrates(pose, overlaps[index], out _, out _))
                        return false;
            }
            return true;
        }

        /// <summary>Respects player ownership, ignored pairs and the project's collision layer policy.</summary>
        /// <param name="owned">Collider on the held body.</param>
        /// <param name="other">World collider returned by a query.</param>
        /// <returns>True when this pair can block the held object.</returns>
        private bool Blocks(Collider owned, Collider other)
        {
            // Triggers are excluded by the query and never prevent carrying through interaction volumes.
            return other != null && other.attachedRigidbody != body && !other.transform.IsChildOf(player)
                && !Physics.GetIgnoreLayerCollision(owned.gameObject.layer, other.gameObject.layer)
                && !Physics.GetIgnoreCollision(owned, other);
        }

        #endregion

        #endregion
    }
}
