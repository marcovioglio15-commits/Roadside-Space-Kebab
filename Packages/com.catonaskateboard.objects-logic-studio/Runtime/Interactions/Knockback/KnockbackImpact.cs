using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Estimates the size of a thrown object's solid collision geometry at impact.</summary>
    internal static class KnockbackImpact
    {
        #region Methods

        #region Measurement

        /// <summary>Compares the compound collider bounds with a reference volume.</summary>
        /// <param name="shapes">Cached shapes owned by the thrown body.</param>
        /// <param name="contact">Contact shape used when the body has no cached solid bounds.</param>
        /// <param name="settings">Influence and neutral volume for the response.</param>
        /// <returns>Bounded size contribution to the effective impact.</returns>
        internal static float VolumeMultiplier(Collider[] shapes, Collider contact, KnockbackSettings settings)
        {
            // One aggregate bound avoids counting overlapping colliders several times.
            Bounds bounds = contact.bounds;
            if (shapes != null)
                foreach (Collider shape in shapes)
                    if (shape != null && shape.enabled && !shape.isTrigger && shape.gameObject.activeInHierarchy)
                        bounds.Encapsulate(shape.bounds);
            Vector3 size = bounds.size;
            float ratio = size.x * size.y * size.z / settings.ReferenceVolume;
            float sizeFactor = Mathf.Clamp(Mathf.Pow(ratio, 1f / 3f), 0.25f, 3f);
            return Mathf.Lerp(1f, sizeFactor, settings.VolumeInfluence);
        }

        #endregion

        #endregion
    }
}
