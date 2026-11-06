using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Prepares spawned and restored bodies for independent physics on release.</summary>
    internal static class ObjectSpawnPhysics
    {
        #region Methods
        #region Collision

        /// <summary>Prepares dynamic bodies for world output and applies any one-time launch velocity.</summary>
        /// <param name="created">Spawned or restored root about to begin independent simulation.</param>
        /// <param name="launchVelocity">Optional velocity change in world space.</param>
        /// <param name="tumble">Optional angular velocity change in world space.</param>
        internal static void Protect(GameObject created, Vector3 launchVelocity = default, Vector3 tumble = default)
        {
            // Keep compound outputs moving together and preserve any stronger collision mode.
            bool launch = launchVelocity.sqrMagnitude > 0f;
            foreach (Rigidbody body in created.GetComponentsInChildren<Rigidbody>())
            {
                if (body.isKinematic)
                    continue;
                if (launch)
                    body.AddForce(launchVelocity, ForceMode.VelocityChange);
                if (tumble.sqrMagnitude > 0f)
                    body.AddTorque(tumble, ForceMode.VelocityChange);
                if (body.collisionDetectionMode != CollisionDetectionMode.Discrete)
                    continue;
                bool primitive = true;
                foreach (Collider collider in body.GetComponentsInChildren<Collider>())
                    if (collider.attachedRigidbody == body && collider.enabled && !collider.isTrigger)
                        primitive &= collider is BoxCollider or SphereCollider or CapsuleCollider;
                body.collisionDetectionMode = primitive ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        #endregion
        #endregion
    }
}
