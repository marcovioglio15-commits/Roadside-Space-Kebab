using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Requires the player's centered cursor to intersect the dialogue's actual collider hierarchy.</summary>
    internal sealed class DialogueSightGeometry
    {
        #region State

        private Collider[] colliders;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Caches target colliders at activation or an explicit geometry refresh.</summary>
        /// <param name="root">Dialogue root whose child colliders may receive the cursor.</param>
        internal void Bind(Transform root)
        {
            // Moving colliders retain their references; inactive children become eligible when enabled.
            colliders = root.GetComponentsInChildren<Collider>(true);
        }

        #endregion

        #region Queries

        /// <summary>Combines a centered surface hit with camera clipping and the dialogue's obstruction mask.</summary>
        /// <param name="observer">Camera and player supplying the fake cursor's viewport center.</param>
        /// <param name="dialogue">Root excluded from its own obstruction check.</param>
        /// <param name="physics">Shared nonallocating sight queries.</param>
        /// <returns>True only while an enabled collider is under the fake cursor and unobstructed.</returns>
        internal bool Visible(HoverObserver observer, ObjectDialogue dialogue, HoverPhysics physics)
        {
            // Use the same collider test as Center Collider Hover; renderer bounds cannot imply a hit.
            Camera view = observer.View;
            if (colliders == null || !HoverPhysics.TryCursorHit(view.ScreenPointToRay(view.pixelRect.center), colliders,
                view.cullingMask, view.farClipPlane, out Vector3 point))
                return false;
            float depth = view.WorldToViewportPoint(point).z;
            return depth >= view.nearClipPlane && depth <= view.farClipPlane
                && physics.HasSight(observer, dialogue.transform, point, dialogue.Settings.ObstacleMask);
        }

        #endregion

        #endregion
    }
}
