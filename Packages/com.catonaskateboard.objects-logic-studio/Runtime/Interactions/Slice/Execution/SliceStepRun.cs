using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Binds one slice step before changing its existing geometry or generating output objects.</summary>
    internal sealed class SliceStepRun
    {
        #region State

        private readonly ItemAppearanceChanges appearance;
        private readonly SliceStep step;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Keeps resolved components until this immediate transaction commits.</summary>
        /// <param name="changes">Prepared appearance changes, or null for consumption.</param>
        /// <param name="configuration">Step supplying material and prefab references.</param>
        private SliceStepRun(ItemAppearanceChanges changes, SliceStep configuration)
        {
            // These bindings are created on input boundaries, never per frame.
            appearance = changes;
            step = configuration;
        }

        /// <summary>Resolves all owned branches before making any visible change.</summary>
        /// <param name="item">Object whose appearance changes.</param>
        /// <param name="step">Validated reusable step settings.</param>
        /// <param name="run">Receives the prepared step.</param>
        /// <param name="warning">Receives an invalid renderer or mesh binding.</param>
        /// <returns>True when this step can commit to existing components.</returns>
        internal static bool TryPrepare(ObjectItem item, SliceStep step, out SliceStepRun run, out string warning)
        {
            // Revalidate paths at the input boundary so an externally removed child cannot redirect an edit.
            run = null;
            warning = string.Empty;
            ItemAppearanceChanges changes = null;
            if (!step.Consume && !ItemAppearanceChanges.TryPrepare(item, step.Meshes, step.Materials, out changes, out warning))
                return false;
            run = new SliceStepRun(changes, step);
            return true;
        }

        #endregion

        #region Commit

        /// <summary>Applies the step once at its owner's current world pose.</summary>
        /// <param name="owner">Root supplying output positions and the destination scene.</param>
        internal void Commit(Transform owner)
        {
            // Material arrays use shared references; no source asset or material instance is modified.
            appearance?.Commit();
            foreach (SliceSpawn spawn in step.Spawns)
            {
                GameObject created = Object.Instantiate(spawn.Prefab, owner.TransformPoint(spawn.Position),
                    owner.rotation * Quaternion.Euler(spawn.Rotation));
                SceneManager.MoveGameObjectToScene(created, owner.gameObject.scene);
                ProtectCollisions(created);
                ObjectGravityGenerator.IncludeSpawn(created);
            }
        }

        /// <summary>Protects thin falling outputs from crossing a floor between discrete physics steps.</summary>
        /// <param name="created">Independent output whose dynamic bodies have just been instantiated.</param>
        private static void ProtectCollisions(GameObject created)
        {
            // Keep existing continuous modes; sweep CCD covers primitives and speculative CCD covers convex meshes.
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
