using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects visible owned surfaces without requiring an assembled object's pivot to remain exposed.</summary>
    internal sealed class InteractionTargeting
    {
        #region State

        private readonly HoverPhysics physics = new HoverPhysics();

        #endregion

        #region Methods

        #region Selection

        /// <summary>Tests an exact aim hit first, then visible anchors within the configured centre radius.</summary>
        /// <param name="observer">Active camera and tagged player.</param>
        /// <param name="root">Hierarchy excluded from its own visibility queries.</param>
        /// <param name="colliders">Cached shapes owned by the interaction.</param>
        /// <param name="anchor">Authored world-space selection offset.</param>
        /// <param name="distance">Maximum reach measured from the player.</param>
        /// <param name="mode">Centre targeting or an exact unlocked cursor hit.</param>
        /// <param name="radius">Centre radius as a fraction of viewport height.</param>
        /// <param name="mask">Solid layers obstructing selection.</param>
        /// <param name="score">Receives the selected surface's squared distance from the player.</param>
        /// <returns>True when at least one owned surface or authored anchor is reachable and visible.</returns>
        internal bool Eligible(HoverObserver observer, Transform root, Collider[] colliders, Vector3 anchor,
            float distance, HoverTargetMode mode, float radius, int mask, out float score)
        {
            // Target geometry may have moved since the last physics step or assembly insertion.
            Camera view = observer.View;
            Rect viewport = view.pixelRect;
            score = float.PositiveInfinity;
            Vector2 pixel = viewport.center;
            if (mode == HoverTargetMode.Cursor)
            {
                if (Mouse.current == null || Cursor.lockState == CursorLockMode.Locked || view.targetDisplay != 0)
                    return false;
                pixel = Mouse.current.position.ReadValue();
                if (!viewport.Contains(pixel))
                    return false;
            }
            if (colliders != null && HoverPhysics.TryCursorHit(view.ScreenPointToRay(pixel), colliders, view.cullingMask,
                Vector3.Distance(view.transform.position, observer.Player.position) + distance, out Vector3 hit)
                && Visible(observer, root, hit, distance, radius, mask, out score))
                return true;
            if (mode != HoverTargetMode.ViewCenter)
                return false;
            if ((view.cullingMask & (1 << root.gameObject.layer)) != 0
                && Visible(observer, root, anchor, distance, radius, mask, out score))
                return true;

            // A product pivot can be inside the worktop; each added ingredient contributes its own visible centre.
            if (colliders != null)
                foreach (Collider collider in colliders)
                    if (collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy
                        && (view.cullingMask & (1 << collider.gameObject.layer)) != 0
                        && Visible(observer, root, collider.bounds.center, distance, radius, mask, out score))
                        return true;
            return false;
        }

        /// <summary>Combines player reach, camera clipping, centre tolerance and obstruction checks.</summary>
        /// <param name="observer">Camera and player supplying the viewing context.</param>
        /// <param name="root">Target hierarchy.</param>
        /// <param name="point">Candidate world point.</param>
        /// <param name="distance">Allowed player reach.</param>
        /// <param name="radius">Allowed screen radius; one permits any cursor position.</param>
        /// <param name="mask">Obstacle layers.</param>
        /// <param name="score">Receives squared player distance.</param>
        /// <returns>True for a visible point inside the configured reach and viewport.</returns>
        private bool Visible(HoverObserver observer, Transform root, Vector3 point, float distance, float radius, int mask, out float score)
        {
            // Distance cannot be extended by using a distant third-person camera.
            score = (point - observer.Player.position).sqrMagnitude;
            Vector3 screen = observer.View.WorldToScreenPoint(point);
            Rect viewport = observer.View.pixelRect;
            return score <= distance * distance && screen.z >= observer.View.nearClipPlane && screen.z <= observer.View.farClipPlane
                && viewport.Contains(screen) && ((Vector2)screen - viewport.center).sqrMagnitude <= radius * radius * viewport.height * viewport.height
                && physics.HasSight(observer, root, point, mask);
        }

        #endregion

        #endregion
    }
}
