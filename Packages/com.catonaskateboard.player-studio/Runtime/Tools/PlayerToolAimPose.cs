using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Solves muzzle alignment analytically and searches horizontal arm positions before rotating.</summary>
    internal static class PlayerToolAimPose
    {
        #region Methods

        #region Geometry

        /// <summary>Finds a forward ray from an offset muzzle through the target without iterative rotation flips.</summary>
        /// <param name="position">Proposed world pivot position.</param>
        /// <param name="offset">Clean world muzzle displacement from the pivot.</param>
        /// <param name="forward">Clean world muzzle forward.</param>
        /// <param name="target">Exact cursor hit or range endpoint.</param>
        /// <param name="clearance">Minimum forward distance beyond the muzzle.</param>
        /// <param name="correction">Receives the world rotation applied to the clean pivot pose.</param>
        /// <returns>True when the target lies on the forward half of a realizable muzzle ray.</returns>
        internal static bool Solve(Vector3 position, Vector3 offset, Vector3 forward, Vector3 target, float clearance, out Quaternion correction)
        {
            // Rotation preserves length: solve the distance travelled along the muzzle's forward axis first.
            correction = Quaternion.identity;
            Vector3 direction = target - position;
            float projection = Vector3.Dot(offset, forward);
            float discriminant = projection * projection + direction.sqrMagnitude - offset.sqrMagnitude;
            if (discriminant < 0f || direction.sqrMagnitude < 0.000001f)
                return false;
            float travel = Mathf.Sqrt(discriminant) - projection;
            if (travel < clearance)
                return false;
            correction = Quaternion.FromToRotation(offset + forward * travel, direction);
            return true;
        }

        /// <summary>Finds a visible clear pose, then refines it continuously around the best horizontal candidate.</summary>
        /// <param name="query">Current camera, geometry and exact target.</param>
        /// <param name="preferred">Previous horizontal goal, retained to discourage side switching.</param>
        /// <param name="distance">Maximum horizontal displacement.</param>
        /// <returns>A stable feasible displacement, or the previous goal when temporarily enclosed.</returns>
        internal static Vector2 Find(PlayerToolAimQuery query, Vector2 preferred, float distance)
        {
            // The usual unobstructed pose needs no neighbourhood search.
            if (query.Score(Vector2.zero, out _, out _) < float.PositiveInfinity)
                return Vector2.zero;
            Vector2 best = preferred;
            float score = query.Score(preferred, out _, out _);
            // Search the wider horizontal space only when the retained route is blocked or hidden.
            if (!float.IsFinite(score))
                for (int ring = 1; ring <= 5; ring++)
                    for (int direction = 0; direction < 8; direction++)
                    {
                        float angle = direction * Mathf.PI * 0.25f;
                        Consider(query, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (distance * ring / 5f), ref best, ref score);
                    }
            // A shrinking neighbourhood removes visible grid steps as a wall or carried object moves.
            float step = distance * 0.1f;
            for (int pass = 0; pass < 5; pass++, step *= 0.5f)
            {
                Vector2 centre = best;
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2 candidate = centre + RefinementOffset(direction, step);
                    if (candidate.sqrMagnitude <= distance * distance)
                        Consider(query, candidate, ref best, ref score);
                }
            }
            return best;
        }

        /// <summary>Shares the cardinal neighbourhood used to refine avoidance and prepared shooting poses.</summary>
        /// <param name="side">Neighbour index from zero to three.</param>
        /// <param name="step">Current horizontal refinement distance.</param>
        /// <returns>A right, left, forward or backward displacement.</returns>
        internal static Vector2 RefinementOffset(int side, float step)
        {
            // Both searches shrink the same neighbourhood around their current best pose.
            return (side switch
            {
                0 => Vector2.right,
                1 => Vector2.left,
                2 => Vector2.up,
                _ => Vector2.down
            }) * step;
        }

        /// <summary>Retains a candidate only when its visibility and clearance improve the current score.</summary>
        /// <param name="query">Current immutable scene query.</param>
        /// <param name="candidate">Horizontal displacement to evaluate.</param>
        /// <param name="best">Receives a better candidate.</param>
        /// <param name="score">Current lowest cost.</param>
        private static void Consider(PlayerToolAimQuery query, Vector2 candidate, ref Vector2 best, ref float score)
        {
            float cost = query.Score(candidate, out _, out _);
            if (cost >= score)
                return;
            best = candidate;
            score = cost;
        }

        #endregion

        #endregion
    }
}
