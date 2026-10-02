using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks ingredient ownership, including completed products supplied to a later recipe.</summary>
    internal static class AssemblyIngredientValidation
    {
        #region Methods

        #region Eligibility

        /// <summary>Rejects incomplete products, ownership cycles and bodies that cannot be suspended safely.</summary>
        /// <param name="product">Destination product or its source prefab.</param>
        /// <param name="grab">Proposed ingredient's root interaction.</param>
        /// <param name="requireHeld">Whether the player must currently carry the ingredient.</param>
        /// <returns>True when the whole ingredient can transfer into one magnet.</returns>
        internal static bool Allows(ObjectAssemblyProduct product, ObjectGrab grab, bool requireHeld)
        {
            // Match only the outer ingredient identity; its retained recipe children never count twice.
            if (grab == null || !grab.isActiveAndEnabled || requireHeld && !grab.IsHeld || grab.Identity == null
                || grab.transform.IsChildOf(product.transform) || product.transform.IsChildOf(grab.transform)
                || !ObjectGrab.ValidateBody(grab.gameObject, out _) || grab.GetComponentsInChildren<Joint>(true).Length > 0)
                return false;
            if (grab.TryGetComponent(out ObjectAssemblyProduct ingredient) && (!ingredient.isActiveAndEnabled || !ingredient.IsComplete)
                || grab.TryGetComponent(out ObjectAssemblyPart attached) && attached.Product != null)
                return false;
            // Retained consumption and reservation state must survive transfer without being bypassed.
            foreach (ObjectItem item in grab.GetComponentsInChildren<ObjectItem>(true))
                if (item.IsConsumed || item.IsReserved || item.IsBlocked(InteractionChannels.Assembly))
                    return false;
            foreach (Collider collider in grab.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && collider.gameObject.activeInHierarchy
                    && collider is not (BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true, sharedMesh: not null }))
                    return false;
            return true;
        }

        #endregion

        #endregion
    }
}
