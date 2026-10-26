using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Protects newly released physical outputs from crossing thin surfaces between simulation steps.</summary>
    internal static class ObjectSpawnPhysics
    {
        #region Methods
        #region Collision

        /// <summary>Enables continuous collision detection once, preserving stronger authored modes and kinematic bodies.</summary>
        /// <param name="created">Spawned or restored root about to begin independent simulation.</param>
        internal static void Protect(GameObject created)
        {
            foreach (Rigidbody body in created.GetComponentsInChildren<Rigidbody>())
            {
                if (body.isKinematic || body.collisionDetectionMode != CollisionDetectionMode.Discrete)
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
