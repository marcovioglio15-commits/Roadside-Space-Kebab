using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Drives the pickup body through the physics solver until it reaches its moving carry anchor.</summary>
    internal sealed class PhysicalPickup
    {
        #region State

        private Rigidbody body;
        private GrabSettings settings;
        private Vector3 velocity;
        /// <summary>Whether the solver currently owns the pickup transition.</summary>
        internal bool Active { get; private set; }

        #endregion
        #region Methods
        #region Motion

        /// <summary>Enables dynamic collision response for the bounded pickup transition.</summary>
        /// <param name="owner">Body whose original state was captured by Grab.</param>
        /// <param name="configuration">Validated pickup tuning.</param>
        internal void Begin(Rigidbody owner, GrabSettings configuration)
        {
            body = owner;
            settings = configuration;
            Active = true;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            body.linearDamping = body.angularDamping = 0f;
            body.maxAngularVelocity = 100f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.WakeUp();
        }

        /// <summary>Uses solver velocities rather than teleporting through contacts.</summary>
        /// <param name="target">Next eased world pose along the pickup route.</param>
        /// <param name="deltaTime">Physics step duration.</param>
        internal void Move(Pose target, float deltaTime)
        {
            // Recompute from the actual pose so impacts cannot accumulate hidden trajectory error.
            velocity = Vector3.ClampMagnitude((target.position - body.position) / deltaTime, settings.PickupSpeed);
            body.linearVelocity = velocity;
            Quaternion delta = target.rotation * Quaternion.Inverse(body.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
                angle -= 360f;
            body.angularVelocity = angle == 0f ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / deltaTime);
        }

        /// <summary>Gives other loose Grab bodies a configurable share of the requested movement.</summary>
        /// <param name="collision">Solver contact with another physical object.</param>
        internal void Contact(Collision collision)
        {
            Rigidbody other = collision.rigidbody;
            if (!Active || other == null || other.isKinematic || !other.TryGetComponent(out ObjectGrab grab)
                || !grab.isActiveAndEnabled || grab.IsHeld || collision.contactCount == 0)
                return;
            // Only push into the contact, never pull a separating object back toward the pickup.
            Vector3 direction = -collision.GetContact(0).normal;
            float speed = Vector3.Dot(velocity - other.linearVelocity, direction);
            if (speed > 0f)
                other.AddForceAtPosition(direction * (speed * settings.PickupPush), collision.GetContact(0).point, ForceMode.VelocityChange);
        }

        /// <summary>Stops the temporary driver before Grab restores or changes body ownership.</summary>
        internal void Stop()
        {
            Active = false;
            body = null;
            velocity = Vector3.zero;
        }

        #endregion
        #endregion
    }
}
