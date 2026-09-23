using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Carries one authored rigidbody without parenting it to the player or creating runtime objects.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Objects Logic Studio/Object Grab")]
    public sealed class ObjectGrab : ObjectSingleInteraction
    {
        #region Serialized Fields

        [Header("Grab")]
        [Tooltip("Targeting, carry pose, pickup transition and held collision behaviour.")]
        [SerializeField]
        private GrabSettings settings = new GrabSettings();
        [Header("Debug")]
        [Tooltip("Show the target anchor and grab range when this object is selected.")]
        [SerializeField]
        private bool drawGizmos = true;

        #endregion

        #region State

        private readonly CarryCollisions collisions = new CarryCollisions();
        private readonly CarryMotion motion = new CarryMotion();
        private Rigidbody body;
        private Collider[] colliders;
        private PhysicsMaterial[] surfaces;
        private ObjectHover[] hovers;
        private CarryBodyState original;
        private Transform carryFrame;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float started;
        private bool firstFollow;

        #endregion

        #region Properties

        /// <summary>Settings used for targeting and carrying.</summary>
        public GrabSettings Settings => settings;
        /// <summary>Identifies the Grab feature in the tool.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Grab;
        /// <summary>Whether this object currently belongs to the observer's carry slot.</summary>
        public bool IsHeld { get; private set; }
        /// <summary>Owned rigidbody used by release actions.</summary>
        public Rigidbody Body => body;
        /// <summary>Whether selected-object debug geometry is enabled.</summary>
        public bool DrawGizmos => drawGizmos;
        /// <summary>Current unobstructed carry anchor for selected-object diagnostics.</summary>
        public Vector3 CarryTarget => carryFrame != null ? CarryPosition() : transform.position;
        /// <summary>World point tested against grab distance and camera targeting.</summary>
        public Vector3 WorldTarget => transform.TransformPoint(settings.TargetOffset);
        /// <summary>Cached solid and trigger colliders owned by this body.</summary>
        internal Collider[] Colliders => colliders;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches the authored body, colliders and hover labels at activation.</summary>
        protected override void OnEnable()
        {
            // Compound colliders belonging to another rigidbody must never inherit these overrides.
            body = GetComponent<Rigidbody>();
            CacheGeometry();
            hovers = GetComponentsInChildren<ObjectHover>(true);
            base.OnEnable();
        }

        /// <summary>Reads current collider ownership at activation and pickup boundaries.</summary>
        private void CacheGeometry()
        {
            // Restore release-owned materials before remembering authored references again.
            RestoreSurface(null);
            List<Collider> owned = new List<Collider>();
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                if (collider.attachedRigidbody == body)
                    owned.Add(collider);
            colliders = owned.ToArray();
            surfaces = new PhysicsMaterial[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
                surfaces[index] = colliders[index].sharedMaterial;
        }

        /// <summary>Releases temporary physics and visibility ownership before pooling or teardown.</summary>
        protected override void OnDisable()
        {
            // Disabling a carried component must not leave a kinematic or collision-free object behind.
            Cancel();
            collisions.Restore(true);
            RestoreSurface(null);
            base.OnDisable();
        }

        /// <summary>Checks outstanding player separation only after an object has been released.</summary>
        private void FixedUpdate()
        {
            // Held presentation is aligned to the camera's render frame, not to physics interpolation.
            if (!IsHeld && collisions.Pending)
                collisions.Restore(false);
        }

        /// <summary>Follows after the player camera has produced its final pose for this render frame.</summary>
        private void LateUpdate()
        {
            // Idle objects never query world geometry or rebuild hierarchy caches.
            if (!IsHeld || Time.deltaTime <= 0f)
                return;
            if (body == null || carryFrame == null || !carryFrame.gameObject.activeInHierarchy)
            {
                Cancel();
                return;
            }
            collisions.Maintain();
            Follow();
        }

        #endregion

        #region Validation

        /// <summary>Requires one physical body and supported solid colliders before pickup.</summary>
        /// <param name="warning">Receives the first invalid setting or hierarchy issue.</param>
        /// <returns>True when a single body can safely own this object.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // Validation is used at editor Apply and runtime binding, never in the carry loop.
            warning = "Grab settings are missing.";
            return settings != null && settings.TryValidate(out warning) && ValidateBody(gameObject, out warning);
        }

        /// <summary>Checks authored rigidbody ownership without adding or modifying components.</summary>
        /// <param name="target">Object receiving Grab.</param>
        /// <param name="warning">Receives a missing or unsupported physics setup.</param>
        /// <returns>True for a non-static object with one rigidbody and at least one solid collider.</returns>
        public static bool ValidateBody(GameObject target, out string warning)
        {
            // Separate physical children require separate grab roots instead of being silently detached.
            warning = string.Empty;
            Rigidbody rigidbody = target.GetComponent<Rigidbody>();
            if (target.isStatic || rigidbody == null || target.GetComponentsInChildren<Rigidbody>(true).Length != 1
                || target.transform.parent != null && target.transform.parent.GetComponentInParent<Rigidbody>() != null)
                warning = "Grab needs a non-static root with one Rigidbody and no nested grabbed bodies.";
            else
            {
                bool solid = false;
                foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled || collider.isTrigger)
                        continue;
                    if (collider is not (BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true, sharedMesh: not null }))
                    {
                        warning = "Grab supports Box, Sphere, Capsule and convex Mesh Colliders.";
                        break;
                    }
                    solid |= collider.GetComponentInParent<Rigidbody>(true) == rigidbody;
                }
                if (warning.Length == 0 && !solid)
                    warning = "Add at least one enabled solid 3D collider before using Grab.";
            }
            return warning.Length == 0;
        }

        #endregion

        #region Carrying

        /// <summary>Claims this body after the shared observer has chosen a single eligible target.</summary>
        /// <param name="observer">Player and camera controlling the carry pose.</param>
        /// <returns>True when this object acquired the previously empty carry slot.</returns>
        internal bool Begin(HoverObserver observer)
        {
            // Capture current body policy so release never assumes the prefab's original defaults.
            if (IsHeld || observer == null || observer.HeldObject != null || observer.Player == null || observer.View == null)
                return false;
            if (!ValidateBody(gameObject, out string warning))
            {
                Debug.LogWarning(warning, this);
                return false;
            }
            CacheGeometry();
            original = new CarryBodyState(body);
            carryFrame = settings.Space == CarrySpace.Camera ? observer.View.transform : observer.Player;
            startPosition = body.position;
            startRotation = body.rotation;
            started = Time.time;
            firstFollow = true;
            collisions.Ignore(colliders, observer.Player);
            motion.Bind(body, colliders, observer.Player);
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.detectCollisions = settings.WorldCollisions;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            body.interpolation = RigidbodyInterpolation.None;
            IsHeld = true;
            SetHoverSuppression(!settings.ShowHover);
            if (settings.Instant)
                Follow();
            return true;
        }

        /// <summary>Computes a world pose without inheriting the player's scale or hierarchy.</summary>
        /// <returns>The desired world-space carry position.</returns>
        private Vector3 CarryPosition()
        {
            // Carry offsets are metres, even when the player or camera hierarchy has authored scale.
            return carryFrame.position + carryFrame.rotation * settings.Offset;
        }

        /// <summary>Applies pickup easing and constrained motion on the same frame as the camera.</summary>
        private void Follow()
        {
            // Queries constrain the kinematic pose before committing it; release restores dynamic simulation.
            float progress = settings.Instant ? 1f : Mathf.SmoothStep(0f, 1f, (Time.time - started) / settings.TransitionDuration);
            Vector3 position = Vector3.Lerp(startPosition, CarryPosition(), progress);
            Quaternion rotation = Quaternion.Slerp(startRotation, carryFrame.rotation * Quaternion.Euler(settings.Rotation), progress);
            Pose pose = new Pose(position, rotation);
            if (settings.WorldCollisions)
                pose = motion.Resolve(pose, settings, Time.deltaTime, firstFollow && settings.Instant);
            body.position = pose.position;
            body.rotation = pose.rotation;
            // Rigidbody setters update the physics pose first; commit the visible Transform in this same render frame.
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            firstFollow = false;
        }

        /// <summary>Restores authored carry overrides when context or component ownership disappears.</summary>
        internal void Cancel()
        {
            // Cancellation preserves the pre-grab kinematic policy rather than inventing a release profile.
            if (!IsHeld)
                return;
            IsHeld = false;
            carryFrame = null;
            if (body != null)
                original.Restore(body, false);
            SetHoverSuppression(false);
        }

        /// <summary>Releases in place, discarding carry velocity before optional launch velocity is added.</summary>
        /// <param name="profile">Validated Drop or Throw body settings.</param>
        /// <param name="surface">Cached contact material, or null to restore authored materials.</param>
        internal void Release(ReleaseSettings profile, PhysicsMaterial surface)
        {
            // Drop starts from rest; Throw adds its deliberate launch after this common transition.
            if (!IsHeld || body == null)
                return;
            IsHeld = false;
            carryFrame = null;
            original.Restore(body, true);
            profile.Apply(body);
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null && !colliders[index].isTrigger)
                    colliders[index].sharedMaterial = surface != null ? surface : surfaces[index];
            SetHoverSuppression(false);
            body.WakeUp();
        }

        /// <summary>Restores material references before a runtime surface is destroyed.</summary>
        /// <param name="surface">Owned material to remove, or null to restore all captured materials.</param>
        internal void RestoreSurface(PhysicsMaterial surface)
        {
            // Teardown may reach a component that never completed activation.
            if (colliders == null)
                return;
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null && (surface == null || colliders[index].sharedMaterial == surface))
                    colliders[index].sharedMaterial = surfaces[index];
        }

        /// <summary>Changes only carry-owned visibility, preserving each hover component's enabled state.</summary>
        /// <param name="suppressed">Whether held labels must remain hidden.</param>
        private void SetHoverSuppression(bool suppressed)
        {
            // Each label reevaluates immediately when carry suppression ends.
            if (hovers == null)
                return;
            foreach (ObjectHover hover in hovers)
                if (hover != null)
                    hover.SetCarrySuppressed(suppressed);
        }

        #endregion

        #endregion
    }
}
