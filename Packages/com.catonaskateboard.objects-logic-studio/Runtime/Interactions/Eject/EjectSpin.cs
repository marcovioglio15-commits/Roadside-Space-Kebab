using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Maintains a self-ejected body's roll around its own forward until disable or despawn.</summary>
    internal sealed class EjectSpin
    {
        #region State

        private Rigidbody body;
        private RigidbodyConstraints constraints;
        private float maximumSpeed;
        private Vector3 originalVelocity;
        private float speed;

        #endregion
        #region Properties

        /// <summary>Whether a launched body still owns this continuous roll.</summary>
        internal bool Active => body != null;

        #endregion
        #region Methods
        #region Motion

        /// <summary>Temporarily releases rotation constraints and starts the requested forward-axis roll.</summary>
        /// <param name="target">Dynamic body that successfully received the self-ejection impulse.</param>
        /// <param name="degreesPerSecond">Signed local-forward rotation speed.</param>
        internal void Begin(Rigidbody target, float degreesPerSecond)
        {
            // Capture after optional launch physics so restoration can unwind in reverse order.
            Restore();
            body = target;
            constraints = body.constraints;
            maximumSpeed = body.maxAngularVelocity;
            originalVelocity = body.angularVelocity;
            speed = degreesPerSecond * Mathf.Deg2Rad;
            body.constraints &= ~RigidbodyConstraints.FreezeRotation;
            body.maxAngularVelocity = Mathf.Max(maximumSpeed, Mathf.Abs(speed));
            Tick();
        }

        /// <summary>Maintains the axial speed while allowing collisions to change the other rotation axes.</summary>
        internal void Tick()
        {
            // Only active launches perform physics work; no transforms or components are searched here.
            if (body == null || body.isKinematic)
                return;
            Vector3 forward = body.rotation * Vector3.forward;
            body.angularVelocity += forward * (speed - Vector3.Dot(body.angularVelocity, forward));
        }

        /// <summary>Restores the pre-spin limits and rotation policy when this launch ends.</summary>
        internal void Restore()
        {
            // Temporary launch physics is restored by its owner after these narrower overrides.
            if (body == null)
                return;
            body.constraints = constraints;
            body.maxAngularVelocity = maximumSpeed;
            if (!body.isKinematic)
                body.angularVelocity = originalVelocity;
            body = null;
        }

        #endregion
        #endregion
    }
}
