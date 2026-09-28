using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds product-owned collision shapes once per ingredient while preserving its original components.</summary>
    internal static class AssemblyGeometry
    {
        #region Methods

        #region Compound Geometry

        /// <summary>Copies supported shapes into an independent hierarchy with no nested rigidbodies or scripts.</summary>
        /// <param name="ingredient">Ingredient already placed in its product-local pose.</param>
        /// <param name="colliders">Original owned shapes captured before suspension.</param>
        /// <param name="shapes">Receives source-to-product collider associations for later mesh effects.</param>
        /// <returns>An inactive product-local collision branch ready to activate after original shapes are suspended.</returns>
        internal static GameObject Create(Transform ingredient, Collider[] colliders, Dictionary<Collider, Collider> shapes)
        {
            // Mirroring transform paths preserves compound scale and rotation without flattening shear.
            GameObject root = new GameObject("Assembly Physics · " + ingredient.name);
            root.SetActive(false);
            root.transform.SetParent(ingredient.parent, false);
            CopyPose(ingredient, root.transform);
            Dictionary<Transform, Transform> paths = new Dictionary<Transform, Transform> { { ingredient, root.transform } };
            foreach (Collider source in colliders)
            {
                if (source == null || !source.enabled)
                    continue;
                Transform target = Resolve(source.transform, paths);
                Collider shape = source switch
                {
                    BoxCollider box => CopyBox(target.gameObject.AddComponent<BoxCollider>(), box),
                    SphereCollider sphere => CopySphere(target.gameObject.AddComponent<SphereCollider>(), sphere),
                    CapsuleCollider capsule => CopyCapsule(target.gameObject.AddComponent<CapsuleCollider>(), capsule),
                    MeshCollider mesh => CopyMesh(target.gameObject.AddComponent<MeshCollider>(), mesh),
                    _ => null
                };
                if (shape == null)
                    continue;
                shape.sharedMaterial = source.sharedMaterial;
                shape.isTrigger = source.isTrigger;
                shape.contactOffset = source.contactOffset;
                shape.includeLayers = source.includeLayers;
                shape.excludeLayers = source.excludeLayers;
                shape.layerOverridePriority = source.layerOverridePriority;
                shape.providesContacts = source.providesContacts;
                shapes.Add(source, shape);
            }
            return root;
        }

        /// <summary>Creates only the transform paths needed by an ingredient's colliders.</summary>
        /// <param name="source">Source collider transform or one of its ancestors.</param>
        /// <param name="paths">Already mirrored source-to-product paths.</param>
        /// <returns>The corresponding transform under the product's collision branch.</returns>
        private static Transform Resolve(Transform source, Dictionary<Transform, Transform> paths)
        {
            // Shared ancestors are allocated once even when several colliders use them.
            if (paths.TryGetValue(source, out Transform target))
                return target;
            Transform parent = Resolve(source.parent, paths);
            target = new GameObject(source.name).transform;
            target.SetParent(parent, false);
            CopyPose(source, target);
            target.gameObject.SetActive(source.gameObject.activeSelf);
            paths.Add(source, target);
            return target;
        }

        /// <summary>Copies local placement and collision layer without instantiating source behaviours.</summary>
        /// <param name="source">Authored ingredient transform.</param>
        /// <param name="target">New collision-only transform.</param>
        private static void CopyPose(Transform source, Transform target)
        {
            // Physics follows the exact same transform chain as the visible ingredient.
            target.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            target.localScale = source.localScale;
            target.gameObject.layer = source.gameObject.layer;
        }

        /// <summary>Copies box dimensions without touching its source collider.</summary>
        /// <param name="target">New shape.</param>
        /// <param name="source">Original shape.</param>
        /// <returns>The configured shape.</returns>
        private static Collider CopyBox(BoxCollider target, BoxCollider source)
        {
            // Centre and dimensions remain local to the mirrored transform.
            target.center = source.center;
            target.size = source.size;
            return target;
        }

        /// <summary>Copies sphere dimensions without baking transform scale.</summary>
        /// <param name="target">New shape.</param>
        /// <param name="source">Original shape.</param>
        /// <returns>The configured shape.</returns>
        private static Collider CopySphere(SphereCollider target, SphereCollider source)
        {
            // Unity applies scale through the copied hierarchy.
            target.center = source.center;
            target.radius = source.radius;
            return target;
        }

        /// <summary>Copies capsule dimensions and its local axis.</summary>
        /// <param name="target">New shape.</param>
        /// <param name="source">Original shape.</param>
        /// <returns>The configured shape.</returns>
        private static Collider CopyCapsule(CapsuleCollider target, CapsuleCollider source)
        {
            // The axis is retained independently of the magnet rotation.
            target.center = source.center;
            target.radius = source.radius;
            target.height = source.height;
            target.direction = source.direction;
            return target;
        }

        /// <summary>Shares validated convex mesh geometry instead of duplicating mesh assets.</summary>
        /// <param name="target">New shape.</param>
        /// <param name="source">Original shape.</param>
        /// <returns>The configured shape.</returns>
        private static Collider CopyMesh(MeshCollider target, MeshCollider source)
        {
            // Cooking settings precede mesh assignment so Unity cooks the intended configuration once.
            target.cookingOptions = source.cookingOptions;
            target.convex = source.convex;
            target.sharedMesh = source.sharedMesh;
            return target;
        }

        #endregion

        #endregion
    }
}
