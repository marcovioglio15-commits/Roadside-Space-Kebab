using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Steers along verified free-space routes and resolves nearby contact without chasing mesh bounds.</summary>
    internal sealed class BlackHoleObstacleMotion
    {
        #region State

        private readonly Rigidbody body;
        private readonly CarryShape[] shapes;
        private readonly BlackHoleObstacleSettings settings;
        private readonly BlackHolePath route;
        private readonly BlackHoleRun run;
        private RaycastHit[] hits = new RaycastHit[32];
        private Collider[] overlaps = new Collider[32];
        private Vector3 previous;
        private Vector3 goal;
        private float stalled;
        private float retry;
        private int failures;
        private float refresh;
        private bool direct;

        #endregion
        #region Methods
        #region Setup

        /// <summary>Caches owned geometry once and retains pathfinding storage for this attraction.</summary>
        /// <param name="body">Attracted physical root.</param>
        /// <param name="colliders">Colliders collected by its attraction owner.</param>
        /// <param name="settings">Validated obstacle response.</param>
        /// <param name="run">Active stream whose peers do not obstruct route queries.</param>
        internal BlackHoleObstacleMotion(Rigidbody body, Collider[] colliders, BlackHoleObstacleSettings settings, BlackHoleRun run)
        {
            this.body = body;
            this.settings = settings;
            this.run = run;
            route = new BlackHolePath(this);
            shapes = new CarryShape[colliders.Length];
            // Nested independent bodies must not contribute to this participant's shape.
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index].attachedRigidbody == body)
                    shapes[index] = new CarryShape(colliders[index], body);
            previous = body.position;
        }

        #endregion
        #region Steering

        /// <summary>Follows a clear route, braking for corners and retaining physical contacts with the environment.</summary>
        /// <param name="destination">Desired center-of-mass destination.</param>
        /// <param name="velocity">Free-space attraction velocity.</param>
        /// <param name="attraction">Speed and acceleration policy.</param>
        /// <param name="deltaTime">Physics step duration.</param>
        /// <param name="searches">Shared remaining route searches for this physics tick.</param>
        /// <returns>Collision-aware velocity for the next physics step.</returns>
        internal Vector3 Steer(Vector3 destination, Vector3 velocity, BlackHoleSettings attraction, float deltaTime, ref int searches)
        {
            Pose pose = new Pose(body.position, body.rotation);
            Vector3 target = destination + body.position - body.worldCenterOfMass;
            retry -= deltaTime;
            refresh -= deltaTime;
            stalled = (body.position - previous).sqrMagnitude < 0.000004f ? stalled + deltaTime : 0f;
            previous = body.position;
            if ((goal - target).sqrMagnitude > 0.01f)
            {
                route.Clear();
                retry = 0f;
                failures = 0;
                goal = target;
                refresh = 0f;
            }
            bool inspect = refresh <= 0f;
            if (inspect)
            {
                // Full visibility and shortcut sweeps run less often; immediate contacts still run every step.
                direct = Clear(pose, target - pose.position);
                refresh = attraction.Performance.RouteRefreshInterval;
            }
            if (direct)
                route.Clear();
            else
            {
                Vector3 next = route.Target(pose, target, inspect);
                if ((!route.Pending || inspect && !Clear(pose, next - pose.position) || stalled >= settings.RecoveryDelay)
                    && retry <= 0f && searches > 0)
                {
                    // Limit searches across all participants; failed routes wait before inspecting moving geometry again.
                    searches--;
                    failures = route.Build(pose, target, settings.PathSpacing) ? 0 : Mathf.Min(failures + 1, 3);
                    retry = settings.RecoveryDelay * (1 << failures);
                }
                if (route.Pending)
                {
                    Vector3 difference = route.Target(pose, target, false) - pose.position;
                    float distance = difference.magnitude;
                    float speed = Mathf.Min(attraction.MaximumSpeed * settings.SlideSpeed,
                        Mathf.Min(Mathf.Sqrt(2f * attraction.Acceleration * distance), distance / deltaTime));
                    velocity = Vector3.MoveTowards(body.linearVelocity, difference.normalized * speed,
                        attraction.Acceleration * deltaTime);
                }
                else
                    velocity = Vector3.MoveTowards(body.linearVelocity, Vector3.zero, attraction.Acceleration * deltaTime);
            }
            // Constrain the actual next step, not the complete look-ahead interval: early wall deflection misses doorways.
            float travel = velocity.magnitude * deltaTime;
            if (Sweep(pose, velocity.normalized, travel, out RaycastHit contact))
            {
                refresh = 0f;
                float into = Mathf.Max(0f, -Vector3.Dot(velocity, contact.normal));
                float allowed = Mathf.Max(0f, contact.distance - settings.Padding) / deltaTime
                    * Mathf.Max(0f, -Vector3.Dot(velocity.normalized, contact.normal));
                if (into > allowed)
                    velocity += contact.normal * (into - allowed);
                if (stalled >= settings.RecoveryDelay)
                    velocity += contact.normal * (settings.Bounce * settings.EscapeSpeed);
            }
            if (stalled >= settings.RecoveryDelay)
            {
                velocity += Separation(pose) * settings.EscapeSpeed;
                stalled = 0f;
            }
            return Vector3.ClampMagnitude(velocity, attraction.MaximumSpeed);
        }

        /// <summary>Checks one complete translated body segment against collision-compatible geometry.</summary>
        /// <param name="pose">Virtual start pose without moving the real body.</param>
        /// <param name="difference">Requested world translation.</param>
        /// <returns>True when all active compound shapes can follow this segment.</returns>
        internal bool Clear(Pose pose, Vector3 difference)
        {
            return !Sweep(pose, difference.normalized, difference.magnitude, out _);
        }

        #endregion
        #region Collision Queries

        /// <summary>Sweeps cached compound shapes and keeps the nearest physically blocking contact.</summary>
        /// <param name="pose">Current body pose.</param>
        /// <param name="direction">Normalized requested motion.</param>
        /// <param name="distance">Distance to inspect.</param>
        /// <param name="nearest">Receives the earliest blocking contact.</param>
        /// <returns>True when a solid collider blocks this direction.</returns>
        private bool Sweep(Pose pose, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            if (direction.sqrMagnitude < 0.5f || distance <= 0f)
                return false;
            foreach (CarryShape shape in shapes)
            {
                if (!shape.Active)
                    continue;
                int count;
                // Saturation grows retained storage only when necessary, so crowded queries stay complete.
                while ((count = shape.Cast(pose, direction, distance + settings.Padding, hits)) == hits.Length)
                    Array.Resize(ref hits, checked(hits.Length * 2));
                for (int index = 0; index < count; index++)
                    if (hits[index].distance < closest && Vector3.Dot(direction, hits[index].normal) < -0.001f
                        && Blocks(shape.Collider, hits[index].collider))
                    {
                        nearest = hits[index];
                        closest = nearest.distance;
                    }
            }
            return closest < float.PositiveInfinity;
        }

        /// <summary>Finds an outward direction for rare initial penetrations or externally moved obstacles.</summary>
        /// <param name="pose">Current body pose.</param>
        /// <returns>Combined finite separation direction, or zero when the body is not embedded.</returns>
        private Vector3 Separation(Pose pose)
        {
            Vector3 direction = Vector3.zero;
            foreach (CarryShape shape in shapes)
            {
                if (!shape.Active)
                    continue;
                int count;
                while ((count = shape.Overlap(pose, overlaps)) == overlaps.Length)
                    Array.Resize(ref overlaps, checked(overlaps.Length * 2));
                for (int index = 0; index < count; index++)
                    if (Blocks(shape.Collider, overlaps[index]) && shape.Penetrates(pose, overlaps[index], out Vector3 normal, out float depth))
                        direction += normal * (depth + settings.Padding);
            }
            return direction.normalized;
        }

        /// <summary>Honors self ownership, trigger exclusion, collision pairs and local layer exclusions.</summary>
        /// <param name="shape">Attracted body's collider.</param>
        /// <param name="other">Potential obstacle.</param>
        /// <returns>True when the obstacle can physically block this shape.</returns>
        private bool Blocks(Collider shape, Collider other)
        {
            return other != null && other.attachedRigidbody != body
                && !run.IsStreamBody(other.attachedRigidbody)
                && ((body.excludeLayers.value | shape.excludeLayers.value) & 1 << other.gameObject.layer) == 0
                && (other.excludeLayers.value & 1 << shape.gameObject.layer) == 0
                && !Physics.GetIgnoreLayerCollision(shape.gameObject.layer, other.gameObject.layer)
                && !Physics.GetIgnoreCollision(shape, other);
        }

        #endregion
        #endregion
    }
}
