using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains only the meshes and materials needed to draw one stored ingredient.</summary>
    internal sealed class InventoryPortionLook
    {
        #region State

        /// <summary>One static render part in ingredient-local space.</summary>
        private readonly struct Part
        {
            internal readonly Mesh Mesh;
            internal readonly Material[] Materials;
            internal readonly Matrix4x4 Pose;

            #region Methods

            /// <summary>Captures the geometry and pose before the ingredient leaves the transfer.</summary>
            /// <param name="mesh">Mesh displayed by this part.</param>
            /// <param name="materials">Shared material slots used by the mesh.</param>
            /// <param name="pose">Transform relative to the ingredient root.</param>
            internal Part(Mesh mesh, Material[] materials, Matrix4x4 pose)
            {
                Mesh = mesh;
                Materials = materials;
                Pose = pose;
            }

            #endregion
        }

        private readonly List<Part> parts = new List<Part>();

        #endregion

        #region Methods

        #region Capture

        /// <summary>Copies render descriptors before the original is consumed or hidden in storage.</summary>
        /// <param name="source">Original item or the optional visual model.</param>
        internal InventoryPortionLook(GameObject source)
        {
            // A stored root may be inactive while its original child visibility remains valid.
            Transform root = source.transform;
            Matrix4x4 basis = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !VisibleBranch(renderer.transform, root))
                    continue;
                Mesh mesh = renderer switch
                {
                    MeshRenderer regular when regular.TryGetComponent(out MeshFilter filter) => filter.sharedMesh,
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    _ => null
                };
                if (mesh != null)
                    parts.Add(new Part(mesh, renderer.sharedMaterials, basis * renderer.transform.localToWorldMatrix));
            }
        }

        /// <summary>Honors hidden render branches even when their stored root is inactive.</summary>
        /// <param name="branch">Renderer transform being captured.</param>
        /// <param name="root">Ingredient whose activity is ignored during capture.</param>
        /// <returns>True when all intermediate branches are visible.</returns>
        private static bool VisibleBranch(Transform branch, Transform root)
        {
            while (branch != root)
            {
                if (!branch.gameObject.activeSelf)
                    return false;
                branch = branch.parent;
            }
            return true;
        }

        #endregion

        #region Rendering

        /// <summary>Creates one reusable visual unit without copying the ingredient hierarchy or its behaviours.</summary>
        /// <param name="container">Parent that moves with the box.</param>
        /// <returns>Root containing only render components and transforms.</returns>
        internal Transform Build(Transform container)
        {
            Transform unit = new GameObject("Fill Unit").transform;
            unit.SetParent(container, false);
            // Mesh and material references stay shared with the original ingredient.
            foreach (Part part in parts)
            {
                GameObject renderPart = new GameObject("Ingredient Mesh", typeof(MeshFilter), typeof(MeshRenderer));
                renderPart.layer = container.gameObject.layer;
                Transform pose = renderPart.transform;
                pose.SetParent(unit, false);
                pose.SetLocalPositionAndRotation(part.Pose.GetColumn(3), part.Pose.rotation);
                pose.localScale = part.Pose.lossyScale;
                renderPart.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
                renderPart.GetComponent<MeshRenderer>().sharedMaterials = part.Materials;
            }
            return unit;
        }

        #endregion

        #endregion
    }
}
