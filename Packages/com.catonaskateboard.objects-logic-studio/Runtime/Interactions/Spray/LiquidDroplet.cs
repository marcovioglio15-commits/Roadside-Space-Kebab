using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Simulates one pooled sample of a liquid column while retaining safe links to adjacent samples.</summary>
    [AddComponentMenu("")]
    internal sealed class LiquidDroplet : MonoBehaviour
    {
        #region State
        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly Collider[] overlaps = new Collider[64];
        private Collider[] ignored;
        private Collider[] ignoredPlayer;
        private SphereCollider shape;
        private SurfaceTrailSettings trail;
        private SurfacePalette palette;
        private LiquidDroplet previous;
        private uint previousGeneration;
        private float born;
        private float breakup;
        private float launchSpeed;
        private float impactSpeed;
        private float stretch;
        private float spacing;
        private float lifetime;
        private bool leavesTrail;
        private int layers;
        private Transform receiverTransform;
        private Vector3 receiverPoint;
        #endregion
        #region Properties
        /// <summary>Native body participating in collisions and gravity-generator leases.</summary>
        internal Rigidbody Body { get; private set; }
        /// <summary>Emitter whose identity and interaction filters apply to this liquid.</summary>
        internal ObjectSpraySauce Source { get; private set; }
        /// <summary>Pool generation preventing links to a recycled sample.</summary>
        internal uint Generation { get; private set; }
        /// <summary>Simulation time when this slot becomes reusable.</summary>
        internal float Expires { get; private set; }
        /// <summary>World-space liquid column radius.</summary>
        internal float Radius { get; private set; }
        /// <summary>Sample colour shared with the connected column.</summary>
        internal Color Tint { get; private set; }
        /// <summary>Wet specular response inherited from surface appearance.</summary>
        internal float Gloss { get; private set; }
        /// <summary>Whether this sample terminates at a contacted surface.</summary>
        internal bool Impacted { get; private set; }
        /// <summary>Gravity-generator participation captured at emission, including after the source despawns.</summary>
        internal bool AllowGravitySuspension { get; private set; }
        /// <summary>Interpolated physical point, or the retained exact contact position.</summary>
        internal Vector3 Position => Impacted && receiverTransform != null ? receiverTransform.TransformPoint(receiverPoint) : transform.position;
        /// <summary>Continuous fade applied only near the original airborne lifetime limit.</summary>
        internal float Opacity => Mathf.Clamp01((born + lifetime - Time.time) / Mathf.Min(0.25f, lifetime));
        /// <summary>Surviving preceding sample within the column's breakup and stretch limits.</summary>
        internal LiquidDroplet Previous => previous != null && previous.Generation == previousGeneration
            && previous.gameObject.activeSelf && CanConnect(previous) ? previous : null;
        /// <summary>Whether the newest sample may still connect to the moving outlet.</summary>
        internal bool JoinsOutlet => Source != null && Source.IsEmitting && Source.Latest == this
            && breakup > 0f && Time.time - born <= Mathf.Min(breakup, Mathf.Max(spacing, Time.fixedDeltaTime) * 2f);
        /// <summary>Travel speed retained at impact so the final section still reaches the receiving surface.</summary>
        private float FlowSpeed => Impacted ? impactSpeed : Body.linearVelocity.magnitude;
        #endregion
        #region Methods
        #region Pooling
        /// <summary>Creates native components once when the shared pool initializes.</summary>
        internal void Initialize()
        {
            gameObject.layer = 2;
            shape = gameObject.AddComponent<SphereCollider>();
            shape.radius = 1f;
            shape.isTrigger = true;
            Body = gameObject.AddComponent<Rigidbody>();
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Initializes a physical sample before joining active gravity pulses and linking its predecessor.</summary>
        /// <param name="source">Emitter providing collision filters and appearance.</param>
        /// <param name="position">World outlet position.</param>
        /// <param name="velocity">Initial world velocity.</param>
        /// <param name="color">Burst colour with optional small sample variation.</param>
        /// <param name="radius">Resolved world radius.</param>
        /// <param name="age">Substep delay integrated with a collision sweep.</param>
        /// <param name="older">Preceding sample in this burst, or null after an interruption.</param>
        internal void Launch(ObjectSpraySauce source, Vector3 position, Vector3 velocity, Color color,
            float radius, float age, LiquidDroplet older)
        {
            Source = source;
            SpraySauceSettings settings = source.Settings;
            Generation++;
            previous = older;
            previousGeneration = older != null ? older.Generation : 0;
            born = Time.time - age;
            lifetime = settings.Lifetime;
            breakup = settings.Cohesion > 0f ? settings.BreakupTime * settings.Cohesion : 0f;
            spacing = 1f / settings.Rate;
            launchSpeed = velocity.magnitude;
            impactSpeed = 0f;
            stretch = settings.StretchLimit;
            Radius = radius;
            Tint = new Color(color.r, color.g, color.b, color.a * settings.Trail.Opacity);
            Gloss = settings.Trail.Gloss * settings.Trail.Liquidity;
            Impacted = false;
            receiverTransform = null;
            AllowGravitySuspension = settings.AllowGravitySuspension;
            trail = settings.Trail;
            palette = source.Palette;
            leavesTrail = settings.LeaveTrail;
            layers = settings.Layers.value & ~(1 << 2);
            Expires = born + lifetime;

            // Restore physical ownership before reuse, including samples previously frozen at an impact.
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one * radius;
            Body.isKinematic = false;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Body.mass = settings.Mass;
            Body.linearDamping = settings.Damping;
            Body.angularDamping = settings.Damping;
            Body.useGravity = true;
            Body.excludeLayers = ~settings.Layers.value | 1 << 2;
            shape.enabled = true;
            gameObject.SetActive(true);
            ignored = source.SourceColliders;
            ignoredPlayer = source.PlayerColliders;
            Ignore(ignored, true);
            Ignore(ignoredPlayer, true);
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            ObjectGravityGenerator.IncludeSpawn(gameObject);
            if (!CheckOverlap())
                Advance(age, true);
        }

        /// <summary>Disconnects stale generations and releases gravity ownership before a sample is reused.</summary>
        internal void Recycle()
        {
            if (!gameObject.activeSelf)
                return;
            ObjectGravityGenerator.Forget(Body);
            Ignore(ignored, false);
            Ignore(ignoredPlayer, false);
            ignored = ignoredPlayer = null;
            Source = null;
            previous = null;
            trail = null;
            palette = null;
            receiverTransform = null;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            gameObject.SetActive(false);
        }
        #endregion
        #region Motion
        /// <summary>Allows natural gravitational spacing while rejecting stretched, stale or unrelated fragments.</summary>
        /// <param name="older">Surviving preceding sample in this emission.</param>
        /// <returns>True while the column can remain connected.</returns>
        private bool CanConnect(LiquidDroplet older)
        {
            if (Impacted || breakup <= 0f || Time.time - born > breakup)
                return false;
            // Falling liquid accelerates: compare separation with current travel, not only launch velocity.
            float speed = Mathf.Max(launchSpeed, Mathf.Max(FlowSpeed, older.FlowSpeed));
            float distance = Mathf.Max(Radius * 4f, speed * Mathf.Max(spacing, born - older.born) * stretch);
            return (older.Position - Position).sqrMagnitude <= distance * distance;
        }

        /// <summary>Absorbs a sample before the physics step crosses a surface, without solver bounce or depenetration impulses.</summary>
        private void FixedUpdate()
        {
            if (!Impacted)
                Advance(Time.fixedDeltaTime, false);
        }

        /// <summary>Sweeps the next motion segment; only substep emission advances a clear sample manually.</summary>
        /// <param name="duration">Elapsed part of this sample's emission step.</param>
        /// <param name="move">Whether to integrate a newly emitted substep rather than leave motion to physics.</param>
        private void Advance(float duration, bool move)
        {
            if (duration <= 0f)
                return;
            Vector3 gravity = Body.useGravity ? Physics.gravity : Vector3.zero;
            Vector3 displacement = Body.linearVelocity * duration + gravity * (duration * duration * 0.5f);
            float distance = displacement.magnitude;
            if (distance < 0.000001f)
                return;
            int count = Physics.SphereCastNonAlloc(Body.position, Radius, displacement / distance, hits,
                distance, layers, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int index = 0; index < count; index++)
                if (!Excluded(hits[index].collider) && (nearest < 0 || hits[index].distance < hits[nearest].distance))
                    nearest = index;
            if (nearest >= 0)
            {
                if (hits[nearest].distance > 0f)
                    Hit(hits[nearest].collider, hits[nearest].point, hits[nearest].normal, Body.linearVelocity);
                else
                    Touch(hits[nearest].collider);
            }
            else if (move)
            {
                Body.position += displacement;
                Body.linearVelocity += gravity * duration;
                transform.position = Body.position;
            }
        }

        /// <summary>Handles a nozzle already touching geometry before its first nonzero sweep.</summary>
        /// <returns>True when emission begins inside a receiving surface.</returns>
        private bool CheckOverlap()
        {
            int count = Physics.OverlapSphereNonAlloc(Body.position, Radius, overlaps, layers, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
                if (!Excluded(overlaps[index]) && Touch(overlaps[index]))
                    return true;
            return false;
        }

        /// <summary>Resolves a genuine surface point when motion begins inside a collider or a moving surface reaches the sample.</summary>
        /// <param name="receiver">Non-trigger surface overlapping this sample.</param>
        /// <returns>True when a physical overlap was absorbed.</returns>
        private bool Touch(Collider receiver)
        {
            if (!Physics.ComputePenetration(shape, Body.position, Body.rotation, receiver,
                receiver.transform.position, receiver.transform.rotation, out Vector3 normal, out float depth))
                return false;
            Hit(receiver, Body.position + normal * (depth - Radius), normal, Body.linearVelocity);
            return true;
        }

        /// <summary>Matches cached source and player exclusions during the substep sweep.</summary>
        /// <param name="collider">Candidate receiving collider.</param>
        /// <returns>True for geometry this emitted liquid must ignore.</returns>
        private bool Excluded(Collider collider)
        {
            return collider == shape || Contains(ignored, collider) || Contains(ignoredPlayer, collider);
        }

        /// <summary>Searches a cached small collision set without allocating a query result.</summary>
        /// <param name="colliders">Cached source or player geometry.</param>
        /// <param name="target">Collider to locate.</param>
        /// <returns>True when the collider belongs to this set.</returns>
        private static bool Contains(Collider[] colliders, Collider target)
        {
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider == target)
                        return true;
            return false;
        }

        /// <summary>Applies or releases native collision exclusions only at sample lifetime boundaries.</summary>
        /// <param name="colliders">Cached source or player geometry.</param>
        /// <param name="ignore">Whether collisions should be excluded.</param>
        private void Ignore(Collider[] colliders, bool ignore)
        {
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                        Physics.IgnoreCollision(shape, collider, ignore);
        }
        #endregion
        #region Contact
        /// <summary>Retains a short-lived surface endpoint so the column meets the deposit without a visible gap.</summary>
        /// <param name="receiver">Exact receiving surface.</param>
        /// <param name="point">World contact point.</param>
        /// <param name="normal">Outward receiver normal.</param>
        /// <param name="velocity">Incoming liquid velocity.</param>
        private void Hit(Collider receiver, Vector3 point, Vector3 normal, Vector3 velocity)
        {
            if (Impacted)
                return;
            impactSpeed = velocity.magnitude;
            Impacted = true;
            receiverTransform = receiver.transform;
            receiverPoint = receiverTransform.InverseTransformPoint(point + normal * Radius * 0.25f);
            if (Source != null)
                Source.Hit(receiver, point, normal, velocity);
            else if (leavesTrail && trail != null && palette != null)
                SurfaceMarkPool.Deposit(receiver, point, normal, velocity, trail, palette);
            ObjectGravityGenerator.Forget(Body);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.isKinematic = true;
            shape.enabled = false;
            Body.position = point + normal * Radius * 0.25f;
            transform.position = Body.position;
            Expires = Mathf.Min(Expires, Time.time + spacing * 2.5f);
        }

        /// <summary>Catches moving receivers entering a sample between predictive sweeps.</summary>
        /// <param name="receiver">Overlapping surface; liquid and source geometry are ignored.</param>
        private void OnTriggerEnter(Collider receiver)
        {
            if (!gameObject.activeSelf || Impacted || receiver.isTrigger || (layers & (1 << receiver.gameObject.layer)) == 0 || Excluded(receiver))
                return;
            Touch(receiver);
        }
        #endregion
        #endregion
    }
}
