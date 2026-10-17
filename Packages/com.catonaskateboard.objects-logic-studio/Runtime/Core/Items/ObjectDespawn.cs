using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Removes a physical object's owning root without leaving active interactions until end of frame.</summary>
    internal static class ObjectDespawn
    {
        #region Methods
        #region Removal

        /// <summary>Finds the independent object represented by a contacted collider.</summary>
        /// <param name="shape">Contacted collider or trigger.</param>
        /// <returns>The item, identity or body root; otherwise the contacted GameObject.</returns>
        internal static GameObject Root(Collider shape)
        {
            Transform root = shape.attachedRigidbody != null ? shape.attachedRigidbody.transform : shape.transform;
            ObjectItem item = root.GetComponentInParent<ObjectItem>();
            if (item != null)
                return item.gameObject;
            CatOnASkateboard.StudioIdentity.ObjectIdentity identity = root.GetComponentInParent<CatOnASkateboard.StudioIdentity.ObjectIdentity>();
            return identity != null ? identity.gameObject : root.gameObject;
        }

        /// <summary>Withdraws an object immediately and schedules normal Unity destruction.</summary>
        /// <param name="target">Independent object being removed from gameplay.</param>
        internal static void Remove(GameObject target)
        {
            if (target == null || !target.activeSelf)
                return;
            // Deathboxes and respawn cleanup return pooled liquid instead of destroying its reusable body.
            if (target.TryGetComponent(out LiquidDroplet droplet))
            {
                droplet.Recycle();
                return;
            }
            target.SetActive(false);
            Object.Destroy(target);
        }

        #endregion
        #endregion
    }
}
