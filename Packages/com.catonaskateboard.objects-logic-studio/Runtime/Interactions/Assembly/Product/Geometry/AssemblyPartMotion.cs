using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Interpolates an accepted ingredient and its existing collision proxy in product-local space.</summary>
    internal sealed class AssemblyPartMotion
    {
        #region State

        private readonly Transform item;
        private readonly Transform geometry;
        private readonly Pose start;
        private readonly Pose target;
        private readonly Vector3 startScale;
        private readonly Vector3 targetScale;
        private readonly float started;
        private readonly float duration;

        #endregion
        #region Methods
        #region Placement

        /// <summary>Captures the settled local pose before restoring the ingredient's incoming world pose.</summary>
        /// <param name="item">Ingredient already parented to its product.</param>
        /// <param name="geometry">Existing product-owned collision branch.</param>
        /// <param name="world">Incoming world pose before parenting.</param>
        /// <param name="scale">Incoming scale converted to the new parent.</param>
        /// <param name="duration">Positive station placement duration.</param>
        internal AssemblyPartMotion(Transform item, Transform geometry, Pose world, Vector3 scale, float duration)
        {
            this.item = item;
            this.geometry = geometry;
            this.duration = duration;
            target = new Pose(item.localPosition, item.localRotation);
            targetScale = item.localScale;
            start = new Pose(item.parent.InverseTransformPoint(world.position), Quaternion.Inverse(item.parent.rotation) * world.rotation);
            startScale = scale;
            started = Time.time;
            Tick();
        }

        /// <summary>Keeps the collision branch aligned while the ingredient approaches its authored magnet.</summary>
        /// <returns>True after both visible and physical geometry reach the final pose.</returns>
        internal bool Tick()
        {
            float progress = Mathf.Clamp01((Time.time - started) / duration);
            float amount = Mathf.SmoothStep(0f, 1f, progress);
            item.SetLocalPositionAndRotation(Vector3.Lerp(start.position, target.position, amount), Quaternion.Slerp(start.rotation, target.rotation, amount));
            item.localScale = Vector3.Lerp(startScale, targetScale, amount);
            geometry.SetLocalPositionAndRotation(item.localPosition, item.localRotation);
            geometry.localScale = item.localScale;
            return progress >= 1f;
        }

        #endregion
        #endregion
    }
}
