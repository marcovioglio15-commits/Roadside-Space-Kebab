using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Scores horizontal arm poses against solids, camera visibility and exact cursor alignment.</summary>
    internal readonly struct PlayerToolAimQuery
    {
        #region State

        private readonly PlayerToolPhysics physics;
        private readonly Camera view;
        private readonly Vector3 position;
        private readonly Vector3 offset;
        private readonly Vector3 forward;
        private readonly Vector3 target;
        private readonly Vector3 heading;
        private readonly Vector3 right;
        private readonly Vector2 previous;
        private readonly float radius;
        private readonly float distance;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures one clean frame without allocating a search object or changing transforms.</summary>
        /// <param name="queries">Player-filtered physics scene.</param>
        /// <param name="camera">Current render camera.</param>
        /// <param name="pivot">Clean world pivot position.</param>
        /// <param name="muzzleOffset">Clean displacement from pivot to muzzle.</param>
        /// <param name="muzzleForward">Clean muzzle forward direction.</param>
        /// <param name="aimPoint">Exact cursor target.</param>
        /// <param name="horizontal">Camera heading on the player's horizontal plane.</param>
        /// <param name="lateral">Horizontal camera-right direction.</param>
        /// <param name="preferred">Previous displacement in this horizontal frame.</param>
        /// <param name="settings">Validated repositioning limits.</param>
        internal PlayerToolAimQuery(PlayerToolPhysics queries, Camera camera, Vector3 pivot, Vector3 muzzleOffset,
            Vector3 muzzleForward, Vector3 aimPoint, Vector3 horizontal, Vector3 lateral, Vector2 preferred, PlayerToolWeaponSettings settings)
        {
            physics = queries;
            view = camera;
            position = pivot;
            offset = muzzleOffset;
            forward = muzzleForward;
            target = aimPoint;
            heading = horizontal;
            right = lateral;
            previous = preferred;
            radius = settings.RepositionRadius;
            distance = settings.RepositionDistance;
        }

        #endregion

        #region Evaluation

        /// <summary>Balances small motion and stable sides while requiring some visible, unobstructed barrel.</summary>
        /// <param name="candidate">Horizontal right/forward displacement in world units.</param>
        /// <param name="muzzle">Receives the solved world muzzle position for a feasible candidate.</param>
        /// <param name="direction">Receives the exact normalized shooting direction.</param>
        /// <returns>A finite pose cost, or infinity for blocked or fully hidden poses.</returns>
        internal float Score(Vector2 candidate, out Vector3 muzzle, out Vector3 direction)
        {
            Vector3 pivot = position + right * candidate.x + heading * candidate.y;
            muzzle = pivot;
            direction = forward;
            // A small proactive clearance starts recovery before the visible barrel touches a surface.
            if (!PlayerToolAimPose.Solve(pivot, offset, forward, target, radius, out Quaternion correction)
                || Vector3.Dot(correction * forward, view.transform.forward) < 0.1f
                || !physics.Clear(pivot, pivot + correction * offset, radius + 0.025f))
                return float.PositiveInfinity;
            muzzle += correction * offset;
            direction = correction * forward;
            int visible = 0;
            // Test representative points along the arm instead of allowing the pivot to retreat out of view indefinitely.
            for (int sample = 0; sample < 3; sample++)
            {
                Vector3 point = pivot + correction * offset * (0.4f + sample * 0.25f);
                Vector3 viewport = view.WorldToViewportPoint(point);
                if (viewport.z <= view.nearClipPlane + 0.025f || viewport.x < 0.03f || viewport.x > 0.97f
                    || viewport.y < 0.03f || viewport.y > 0.97f)
                    continue;
                Vector3 sight = point - view.transform.position;
                if (!physics.Cast(view.transform.position, sight.normalized, Mathf.Max(0f, sight.magnitude - radius), 0f, ~0, false, out _))
                    visible++;
            }
            if (visible == 0)
                return float.PositiveInfinity;
            // Exact aim is solved geometrically; its visual angle is a soft preference rather than a forced deep retreat.
            return candidate.sqrMagnitude / (distance * distance) + (candidate - previous).sqrMagnitude * 0.12f
                + (1f - Vector3.Dot(correction * forward, view.transform.forward)) * 0.2f - visible * 0.025f;
        }

        #endregion

        #endregion
    }
}
