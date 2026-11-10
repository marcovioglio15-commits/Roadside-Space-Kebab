using CatOnASkateboard.PlayerStudio;
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
            bool held = owner.TryGetComponent(out ObjectGrab carried) && carried.IsHeld;
            SliceCarryGroup group = step.Consume && step.KeepOutputsHeld && held
                ? SliceCarryGroup.Create(carried, step.Spawns.Length, step.ThrowAsShuriken) : null;
            PlayerToolImpactForceSettings force = group == null && step.Spawns.Length > 0 && InteractionProjectileInput.Active
                && InteractionProjectileInput.Allows(owner) ? InteractionProjectileInput.Tool?.Weapon?.SimulatedImpactForce : null;
            bool shatter = force is { Enabled: true, SliceShattering: true } && (!held || !force.IgnoreCarriedObjects);
            Vector3 shotBias = shatter ? Vector3.ProjectOnPlane(InteractionProjectileInput.Velocity, Vector3.up).normalized * 0.28f : Vector3.zero;
            float phase = shatter ? Random.Range(0f, 360f) : 0f;
            // Scaling both speed components by the square root scales free-flight range at equal heights.
            float launchScale = shatter ? Mathf.Sqrt(force.SliceDistanceMultiplier) : 1f;
            // Each loose piece gets its own heading. The carry group keeps its original assembly path.
            for (int index = 0; index < step.Spawns.Length; index++)
            {
                SliceSpawn spawn = step.Spawns[index];
                GameObject created = group != null ? ObjectSpawnHistory.Create(spawn.Prefab, group.transform, false)
                    : ObjectSpawnHistory.Create(spawn.Prefab, owner.TransformPoint(spawn.Position), owner.rotation * Quaternion.Euler(spawn.Rotation));
                if (group != null)
                    created.transform.SetPositionAndRotation(owner.TransformPoint(spawn.Position), owner.rotation * Quaternion.Euler(spawn.Rotation));
                else
                    SceneManager.MoveGameObjectToScene(created, owner.gameObject.scene);
                Vector3 velocity = Vector3.zero;
                Vector3 tumble = Vector3.zero;
                if (shatter)
                {
                    Vector3 heading = Quaternion.AngleAxis(phase + 360f * index / step.Spawns.Length, Vector3.up) * Vector3.forward;
                    force.SampleBurst((heading + shotBias).normalized, 1f, out velocity, out tumble);
                    velocity *= launchScale;
                    ShotBurstCollisions.Add(created.TryGetComponent(out ObjectGrab piece) ? piece.Colliders : created.GetComponentsInChildren<Collider>());
                }
                ObjectSpawnPhysics.Protect(created, velocity, tumble);
                ObjectGravityGenerator.IncludeSpawn(created);
                group?.Add(created);
            }
            group?.Take(carried);
        }

        #endregion

        #endregion
    }
}
