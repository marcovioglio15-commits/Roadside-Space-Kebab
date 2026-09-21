using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Captures only body properties temporarily changed while carrying.</summary>
    internal readonly struct CarryBodyState
    {
        #region Fields

        private readonly bool kinematic;
        private readonly bool gravity;
        private readonly bool collisions;
        private readonly RigidbodyConstraints constraints;
        private readonly CollisionDetectionMode detection;
        private readonly RigidbodyInterpolation interpolation;
        private readonly float linearDamping;
        private readonly float angularDamping;
        private readonly float maxAngularSpeed;

        #endregion

        #region Methods

        #region Snapshot

        /// <summary>Remembers the pre-grab motion policy without storing stale velocities.</summary>
        /// <param name="body">Body about to be carried.</param>
        internal CarryBodyState(Rigidbody body)
        {
            // Mass and material are not changed by carrying itself.
            kinematic = body.isKinematic;
            gravity = body.useGravity;
            collisions = body.detectCollisions;
            constraints = body.constraints;
            detection = body.collisionDetectionMode;
            interpolation = body.interpolation;
            linearDamping = body.linearDamping;
            angularDamping = body.angularDamping;
            maxAngularSpeed = body.maxAngularVelocity;
        }

        /// <summary>Restores carry overrides before a release profile is applied.</summary>
        /// <param name="body">Carried body being restored.</param>
        /// <param name="release">Make the body dynamic for a deliberate Drop or Throw.</param>
        internal void Restore(Rigidbody body, bool release)
        {
            // Set a compatible mode before toggling kinematic state.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = !release && kinematic;
            body.useGravity = gravity;
            body.detectCollisions = collisions;
            body.constraints = constraints;
            body.collisionDetectionMode = detection;
            body.interpolation = interpolation;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.maxAngularVelocity = maxAngularSpeed;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        #endregion

        #endregion
    }
}
