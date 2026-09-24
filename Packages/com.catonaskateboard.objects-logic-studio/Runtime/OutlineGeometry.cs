using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds one outline shell during prefab authoring or a composite ingredient insertion.</summary>
    public static class OutlineGeometry
    {
        #region Methods

        #region Creation

        /// <summary>Creates a dedicated shell while sharing source mesh data and preserving the original renderer.</summary>
        /// <param name="source">Mesh or skinned renderer requiring an outline.</param>
        /// <param name="material">Shared outline material assigned to all mesh submeshes.</param>
        /// <returns>The new shell binding, or null for unsupported or missing geometry.</returns>
        public static OutlineBinding Create(Renderer source, Material material)
        {
            // Allocation occurs only at explicit geometry boundaries, never during outline animation.
            MeshFilter filter = source.GetComponent<MeshFilter>();
            Mesh mesh = source is SkinnedMeshRenderer skin ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (mesh == null || source is not (MeshRenderer or SkinnedMeshRenderer))
                return null;
            GameObject child = new GameObject("Outline Shell");
            child.layer = source.gameObject.layer;
            child.transform.SetParent(source.transform, false);
            OutlineBinding binding = new OutlineBinding { Source = source, SourceMesh = filter };
            switch (source)
            {
                case SkinnedMeshRenderer skinned:
                    SkinnedMeshRenderer shell = child.AddComponent<SkinnedMeshRenderer>();
                    shell.sharedMesh = mesh;
                    shell.bones = skinned.bones;
                    shell.rootBone = skinned.rootBone;
                    shell.localBounds = skinned.localBounds;
                    shell.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    shell.quality = skinned.quality;
                    binding.Shell = shell;
                    break;
                default:
                    binding.ShellMesh = child.AddComponent<MeshFilter>();
                    binding.ShellMesh.sharedMesh = mesh;
                    binding.Shell = child.AddComponent<MeshRenderer>();
                    break;
            }
            Material[] materials = new Material[mesh.subMeshCount];
            for (int index = 0; index < materials.Length; index++)
                materials[index] = material;
            binding.Shell.sharedMaterials = materials;
            binding.Shell.shadowCastingMode = ShadowCastingMode.Off;
            binding.Shell.receiveShadows = false;
            binding.Shell.lightProbeUsage = LightProbeUsage.Off;
            binding.Shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            binding.Shell.enabled = false;
            return binding;
        }

        #endregion

        #region LOD Membership

        /// <summary>Keeps outline shells in the same LOD as their source at explicit geometry changes.</summary>
        /// <param name="root">Hierarchy containing the affected LOD groups.</param>
        /// <param name="bindings">Source and shell pairs to add or remove.</param>
        /// <param name="add">True when attaching shells; false before their destruction.</param>
        /// <param name="beforeChange">Optional editor callback recording Undo before a group changes.</param>
        public static void UpdateLods(GameObject root, IReadOnlyList<OutlineBinding> bindings, bool add,
            Action<LODGroup> beforeChange = null)
        {
            // LOD culling leaves Renderer.enabled unchanged, so visibility synchronization alone is insufficient.
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                bool changed = false;
                for (int index = 0; index < levels.Length; index++)
                {
                    List<Renderer> renderers = new List<Renderer>();
                    foreach (Renderer renderer in levels[index].renderers)
                        if (renderer != null)
                            renderers.Add(renderer);
                    foreach (OutlineBinding binding in bindings)
                    {
                        if (binding?.Shell == null)
                            continue;
                        if (add && renderers.Contains(binding.Source) && !renderers.Contains(binding.Shell))
                        {
                            renderers.Add(binding.Shell);
                            changed = true;
                        }
                        else if (!add)
                            changed |= renderers.Remove(binding.Shell);
                    }
                    changed |= renderers.Count != levels[index].renderers.Length;
                    levels[index].renderers = renderers.ToArray();
                }
                if (!changed)
                    continue;
                beforeChange?.Invoke(group);
                group.SetLODs(levels);
            }
        }

        #endregion

        #endregion
    }
}
