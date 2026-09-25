using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Binds one slice step before changing its existing geometry or generating output objects.</summary>
    internal sealed class SliceStepRun
    {
        #region State

        private readonly ItemMeshChanges meshes;
        private readonly Renderer[] renderers;
        private readonly SliceStep step;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Keeps resolved components until this immediate transaction commits.</summary>
        /// <param name="geometry">Prepared mesh changes.</param>
        /// <param name="targets">Renderers receiving material arrays.</param>
        /// <param name="configuration">Step supplying material and prefab references.</param>
        private SliceStepRun(ItemMeshChanges geometry, Renderer[] targets, SliceStep configuration)
        {
            // These bindings are created on input boundaries, never per frame.
            meshes = geometry;
            renderers = targets;
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
            if (!ItemMeshChanges.TryPrepare(item, step.Meshes, out ItemMeshChanges meshes, out warning))
                return false;
            Renderer[] renderers = new Renderer[step.Materials.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                Transform branch = ItemMeshChanges.Resolve(item.transform, step.Materials[index].Path);
                if (branch == null || !item.Owns(branch) || !branch.TryGetComponent(out renderers[index]))
                {
                    warning = "A Slice material path is missing, ambiguous, or has no owned Renderer.";
                    return false;
                }
            }
            run = new SliceStepRun(meshes, renderers, step);
            return true;
        }

        #endregion

        #region Commit

        /// <summary>Applies the step once at its owner's current world pose.</summary>
        /// <param name="owner">Root supplying output positions and the destination scene.</param>
        internal void Commit(Transform owner)
        {
            // Material arrays use shared references; no source asset or material instance is modified.
            meshes.Commit();
            for (int index = 0; index < renderers.Length; index++)
                renderers[index].sharedMaterials = step.Materials[index].Materials;
            foreach (SliceSpawn spawn in step.Spawns)
            {
                GameObject created = Object.Instantiate(spawn.Prefab, owner.TransformPoint(spawn.Position),
                    owner.rotation * Quaternion.Euler(spawn.Rotation));
                SceneManager.MoveGameObjectToScene(created, owner.gameObject.scene);
            }
        }

        #endregion

        #endregion
    }
}
