using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tests visible portions of cached target bounds against the actual observer camera.</summary>
    internal sealed class DialogueSightGeometry
    {
        #region State

        private Renderer[] renderers;
        private Collider[] colliders;
        private readonly Vector3[] corners = new Vector3[8];
        private readonly Plane[] planes = new Plane[6];

        #endregion

        #region Methods

        #region Binding

        /// <summary>Captures target geometry at activation or an explicit hierarchy refresh.</summary>
        /// <param name="root">Dialogue object whose visible children participate.</param>
        internal void Bind(Transform root)
        {
            // Geometry references remain cached while bounds follow moving objects automatically.
            renderers = root.GetComponentsInChildren<Renderer>(true);
            colliders = root.GetComponentsInChildren<Collider>(true);
        }

        #endregion

        #region Queries

        /// <summary>Accepts any sampled visible portion, including bounds edges clipped by the viewport.</summary>
        /// <param name="observer">Player and gameplay camera.</param>
        /// <param name="dialogue">Object owning the sight requirement.</param>
        /// <param name="physics">Reusable obstruction query buffers.</param>
        /// <returns>True when some target geometry is in frame and has unobstructed sight.</returns>
        internal bool Visible(HoverObserver observer, ObjectDialogue dialogue, HoverPhysics physics)
        {
            // Frustum planes include both near and far clipping; offscreen anchors never bypass them.
            Camera view = observer.View;
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            bool hasGeometry = false;
            foreach (Renderer renderer in renderers)
                if (renderer != null)
                {
                    hasGeometry = true;
                    if (renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy
                        && (view.cullingMask & (1 << renderer.gameObject.layer)) != 0
                        && BoundsVisible(renderer.bounds, observer, dialogue, physics))
                        return true;
                }
            // Invisible interaction volumes may use their colliders when no renderers are authored.
            if (!hasGeometry)
                foreach (Collider collider in colliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    {
                        hasGeometry = true;
                        if ((view.cullingMask & (1 << collider.gameObject.layer)) != 0
                            && BoundsVisible(collider.bounds, observer, dialogue, physics))
                            return true;
                    }
            return !hasGeometry && (view.cullingMask & (1 << dialogue.gameObject.layer)) != 0
                && PointVisible(dialogue.transform.TransformPoint(dialogue.Settings.SightOffset), observer, dialogue, physics);
        }

        /// <summary>Tests the bounds centre, face centres and frustum-clipped edges.</summary>
        /// <param name="bounds">World bounds of one visible child.</param>
        /// <param name="observer">Current camera and player context.</param>
        /// <param name="dialogue">Sight mask and target hierarchy.</param>
        /// <param name="physics">Shared non-allocating physics queries.</param>
        /// <returns>True for a visible sampled portion of the bounds.</returns>
        private bool BoundsVisible(Bounds bounds, HoverObserver observer, ObjectDialogue dialogue, HoverPhysics physics)
        {
            // Reject fully offscreen geometry before casting any rays.
            if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                return false;
            if (PointVisible(bounds.center, observer, dialogue, physics))
                return true;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 offset = Vector3.zero;
                offset[axis] = bounds.extents[axis];
                if (PointVisible(bounds.center + offset, observer, dialogue, physics)
                    || PointVisible(bounds.center - offset, observer, dialogue, physics))
                    return true;
            }
            for (int corner = 0; corner < 8; corner++)
                corners[corner] = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            // Clip all twelve box edges so a narrow visible sliver does not require an onscreen pivot.
            for (int corner = 0; corner < 8; corner++)
                for (int axis = 0; axis < 3; axis++)
                {
                    int other = corner ^ (1 << axis);
                    if (other <= corner || !Clip(corners[corner], corners[other], out Vector3 start, out Vector3 end))
                        continue;
                    if (PointVisible(start, observer, dialogue, physics) || PointVisible(end, observer, dialogue, physics)
                        || PointVisible((start + end) * 0.5f, observer, dialogue, physics))
                        return true;
                }
            return false;
        }

        /// <summary>Clips a segment against every camera plane without allocating geometry.</summary>
        /// <param name="start">Original first endpoint.</param>
        /// <param name="end">Original second endpoint.</param>
        /// <param name="first">Receives the clipped first endpoint.</param>
        /// <param name="last">Receives the clipped second endpoint.</param>
        /// <returns>True when some segment remains inside the frustum.</returns>
        private bool Clip(Vector3 start, Vector3 end, out Vector3 first, out Vector3 last)
        {
            // Plane distances are positive inside Unity's camera frustum.
            first = start;
            last = end;
            foreach (Plane plane in planes)
            {
                float from = plane.GetDistanceToPoint(first);
                float to = plane.GetDistanceToPoint(last);
                if (from < 0f && to < 0f)
                    return false;
                if (from >= 0f && to >= 0f)
                    continue;
                Vector3 intersection = Vector3.LerpUnclamped(first, last, from / (from - to));
                if (from < 0f)
                    first = intersection;
                else
                    last = intersection;
            }
            return true;
        }

        /// <summary>Combines camera clipping and direct sight for one candidate point.</summary>
        /// <param name="point">World-space candidate.</param>
        /// <param name="observer">Gameplay viewing context.</param>
        /// <param name="dialogue">Target hierarchy and obstruction mask.</param>
        /// <param name="physics">Reusable raycast buffers.</param>
        /// <returns>True for an in-frame point unobstructed by external geometry.</returns>
        private static bool PointVisible(Vector3 point, HoverObserver observer, ObjectDialogue dialogue, HoverPhysics physics)
        {
            // Small boundary tolerance absorbs frustum clipping roundoff without accepting offscreen objects.
            Vector3 screen = observer.View.WorldToViewportPoint(point);
            return screen.z >= observer.View.nearClipPlane - 0.0001f && screen.z <= observer.View.farClipPlane
                && screen.x >= -0.00001f && screen.x <= 1.00001f && screen.y >= -0.00001f && screen.y <= 1.00001f
                && physics.HasSight(observer, dialogue.transform, point, dialogue.Settings.ObstacleMask);
        }

        #endregion

        #endregion
    }
}
