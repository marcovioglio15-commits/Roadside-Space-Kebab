using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one body's original physics while it approaches a black hole destination.</summary>
    internal sealed class BlackHoleBody
    {
        #region State

        internal readonly Rigidbody Body;
        internal readonly ObjectItem Item;
        private readonly ObjectEject owner;
        private readonly Collider collector;
        private readonly CarryBodyState original;
        private readonly int excludedLayers;
        private readonly Collider[] shapes;
        private readonly PhysicsMaterial[] surfaces;
        private readonly PhysicsMaterial surface;
        private readonly BlackHoleObstacleMotion obstacles;
        private readonly int[] priorities;
        private readonly CarryCollisions destinationCollisions = new CarryCollisions();
        private Vector3 previous;
        private Vector3 position;
        private bool entering;
        private float entranceCheck;
        /// <summary>Remaining distance through the approach point, or directly to the collector after entry.</summary>
        internal float Distance { get; set; }
        /// <summary>Stable tie breaker for bodies at the same remaining distance.</summary>
        internal EntityId Identity { get; }
        /// <summary>Whether this entry still owns attraction motion.</summary>
        internal bool Attracting { get; private set; } = true;
        /// <summary>Whether the final arrival impulse has already been applied.</summary>
        internal bool Launched { get; private set; }
        /// <summary>Whether this participant may still pass peers without interrupting a later object transaction.</summary>
        internal bool InStream => (Attracting || Launched)
            && (Item == null || !Item.IsCarried && !Item.IsReserved && !Item.IsConsumed);
        /// <summary>Cached compound geometry used for temporary participant collision pairs.</summary>
        internal Collider[] Shapes => shapes;

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Acquires the body's motion without discarding another generator's suspension policy.</summary>
        /// <param name="body">Eligible physical root.</param>
        /// <param name="item">Optional object interaction owner.</param>
        /// <param name="ejector">Black hole acquiring this participant.</param>
        /// <param name="destination">Authored destination collider.</param>
        /// <param name="surface">Optional shared low-friction material owned by the active run.</param>
        /// <param name="run">Stream ownership used to exclude peers from route queries.</param>
        internal BlackHoleBody(Rigidbody body, ObjectItem item, ObjectEject ejector, Collider destination, PhysicsMaterial surface, BlackHoleRun run)
        {
            Body = body;
            Item = item;
            owner = ejector;
            collector = destination;
            original = new CarryBodyState(body);
            Identity = body.GetEntityId();
            shapes = body.GetComponentsInChildren<Collider>();
            priorities = new int[shapes.Length];
            surfaces = surface != null ? new PhysicsMaterial[shapes.Length] : null;
            this.surface = surface;
            if (ejector.Settings.BlackHole.Obstacles.Enabled)
                obstacles = new BlackHoleObstacleMotion(body, shapes, ejector.Settings.BlackHole.Obstacles, run);
            destinationCollisions.Ignore(shapes, destination.transform);
            excludedLayers = ejector.Settings.BlackHole.PassThrough ? ejector.Settings.BlackHole.PassageLayers.value & ~body.excludeLayers.value : 0;
            body.excludeLayers = body.excludeLayers.value | excludedLayers;
            for (int index = 0; index < shapes.Length; index++)
            {
                priorities[index] = shapes[index].layerOverridePriority;
                if (surface != null && !shapes[index].isTrigger && shapes[index].attachedRigidbody == body)
                {
                    surfaces[index] = shapes[index].sharedMaterial;
                    shapes[index].sharedMaterial = surface;
                }
                if (shapes[index].attachedRigidbody == body && excludedLayers != 0)
                    shapes[index].layerOverridePriority = 65;
            }
            GravitySuspension.Acquire(body, owner);
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.constraints = RigidbodyConstraints.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0f;
            previous = body.worldCenterOfMass;
            body.WakeUp();
        }

        /// <summary>Restores only this attraction's body overrides while retaining launched momentum.</summary>
        /// <param name="launch">Whether this release hands the body to its final impulse.</param>
        internal void Release(bool launch)
        {
            Attracting = false;
            GravitySuspension.Release(Body, owner);
            if (Body != null)
            {
                Vector3 velocity = Body.isKinematic ? Vector3.zero : Body.linearVelocity;
                original.Restore(Body, launch);
                Body.excludeLayers = Body.excludeLayers.value & ~excludedLayers;
                if (!Body.isKinematic)
                    Body.linearVelocity = launch && owner.Settings.BlackHole.ReplaceVelocity ? Vector3.zero : velocity;
            }
            for (int index = 0; index < shapes.Length; index++)
                if (shapes[index] != null && shapes[index].attachedRigidbody == Body)
                {
                    if (shapes[index].layerOverridePriority == 65)
                        shapes[index].layerOverridePriority = priorities[index];
                    if (surface != null && shapes[index].sharedMaterial == surface)
                        shapes[index].sharedMaterial = surfaces[index];
                }
            destinationCollisions.Restore(!launch);
        }

        /// <summary>Restores remaining destination collision pairs after the launched object clears the collector.</summary>
        /// <param name="immediate">True during teardown, false while waiting for safe separation.</param>
        /// <returns>True when no destination pair remains excluded.</returns>
        internal bool RestoreCollisions(bool immediate)
        {
            destinationCollisions.Restore(immediate);
            return !destinationCollisions.Pending;
        }

        #endregion
        #region Attraction

        /// <summary>Caches progress once per step before distance priority and attraction are evaluated.</summary>
        /// <param name="destination">Current collector destination.</param>
        /// <param name="approach">Current entrance point.</param>
        /// <param name="guided">Whether the entrance point participates in this effect.</param>
        internal void Refresh(Vector3 destination, Vector3 approach, bool guided)
        {
            position = Body.worldCenterOfMass;
            Distance = guided && !entering ? Vector3.Distance(position, approach) + Vector3.Distance(approach, destination)
                : Vector3.Distance(position, destination);
        }

        /// <summary>Steers velocity towards the collector, braking and reducing spiral motion near arrival.</summary>
        /// <param name="destination">Current world collector center plus authored offset.</param>
        /// <param name="axis">World spiral axis.</param>
        /// <param name="settings">Active source settings.</param>
        /// <param name="deltaTime">Physics step duration.</param>
        /// <param name="searches">Shared route-search budget for the current physics tick.</param>
        /// <returns>True once the center of mass reaches or crosses the arrival sphere.</returns>
        internal bool Advance(Vector3 destination, Vector3 axis, BlackHoleSettings settings, float deltaTime, ref int searches)
        {
            Vector3 path = position - previous;
            float fraction = path.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(destination - previous, path) / path.sqrMagnitude) : 0f;
            bool arrived = (previous + path * fraction - destination).sqrMagnitude <= settings.ArrivalRadius * settings.ArrivalRadius;
            if (arrived)
                return true;
            // Approach from outside the mouth before aiming behind its surrounding wall.
            Vector3 approach = destination + collector.transform.TransformVector(settings.EntranceOffset);
            if (settings.GuideEntrance && !entering)
            {
                fraction = path.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(approach - previous, path) / path.sqrMagnitude) : 0f;
                entering = (previous + path * fraction - approach).sqrMagnitude <= settings.ArrivalRadius * settings.ArrivalRadius;
                entranceCheck -= deltaTime;
                // Skip the artificial stop at the approach point once the complete body has a clear route inside.
                float handoff = Mathf.Min(1f, Vector3.Distance(approach, destination) * 0.5f);
                if (!entering && obstacles != null && entranceCheck <= 0f && (position - approach).sqrMagnitude <= handoff * handoff)
                {
                    entranceCheck = settings.Performance.RouteRefreshInterval;
                    entering = obstacles.Clear(new Pose(Body.position, Body.rotation), destination - position);
                }
            }
            previous = position;
            Vector3 target = settings.GuideEntrance && !entering ? approach : destination;
            Vector3 difference = target - position;
            float distance = difference.magnitude;
            Vector3 direction = difference.normalized;
            float speed = Mathf.Min(settings.MaximumSpeed, Mathf.Min(Mathf.Sqrt(2f * settings.Acceleration * distance), distance / deltaTime));
            Vector3 velocity = direction * speed;
            if (settings.Spiral)
            {
                float fade = settings.GuideEntrance ? (entering ? 0f : Mathf.Clamp01((distance - 2f) * 0.5f))
                    : Mathf.Clamp01((distance - settings.ArrivalRadius) / Mathf.Max(1f, settings.ArrivalRadius * 4f));
                velocity += Vector3.Cross(axis, direction).normalized * (settings.SpiralSpeed * fade);
            }
            // Acceleration and speed caps prevent high-force tunnelling and unbounded orbit velocities.
            velocity = Vector3.ClampMagnitude(velocity, settings.MaximumSpeed);
            velocity = Vector3.MoveTowards(Body.linearVelocity, velocity, settings.Acceleration * deltaTime);
            Body.linearVelocity = obstacles != null ? obstacles.Steer(target, velocity, settings, deltaTime, ref searches) : velocity;
            destinationCollisions.Maintain();
            return false;
        }

        /// <summary>Applies the final impulse once and starts the existing optional lifetime at this boundary.</summary>
        internal void Launch()
        {
            Release(true);
            Launched = true;
            Body.constraints = RigidbodyConstraints.None;
            Body.detectCollisions = true;
            BlackHoleSettings settings = owner.Settings.BlackHole;
            Body.AddForce(settings.ImpulseSpace == Space.Self ? owner.transform.rotation * settings.Impulse : settings.Impulse,
                owner.Settings.Mode == EjectForceMode.Impulse ? ForceMode.Impulse : ForceMode.VelocityChange);
            if (owner.Settings.IgnoreCollisions)
                EjectCollisionGrace.Apply(Body, owner.Settings.IgnoredLayers, owner.Settings.IgnoreDuration);
            if (owner.Settings.Despawn)
                Object.Destroy(Item != null ? Item.gameObject : Body.gameObject, owner.Settings.DespawnDelay);
        }

        #endregion
        #endregion
    }
}
