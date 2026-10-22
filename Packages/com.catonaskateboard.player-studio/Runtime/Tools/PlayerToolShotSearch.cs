using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Finds a visible shooting pose whose fitted impact is closest to the cursor's physical target.</summary>
    internal static class PlayerToolShotSearch
    {
        #region Types

        /// <summary>Ranks target identity first, then impact distance, then arm movement cost.</summary>
        private readonly struct Candidate
        {
            internal readonly Vector2 Offset;
            internal readonly int Priority;
            internal readonly float Error;
            internal readonly float Cost;

            #region Methods

            /// <summary>Retains one allocation-free comparison result.</summary>
            /// <param name="offset">Horizontal pose displacement.</param>
            /// <param name="priority">Target and impact availability rank.</param>
            /// <param name="error">Squared distance from the fitted visual to the cursor target.</param>
            /// <param name="cost">Visibility and movement tie breaker.</param>
            internal Candidate(Vector2 offset, int priority, float error, float cost)
            {
                Offset = offset;
                Priority = priority;
                Error = error;
                Cost = cost;
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Search

        /// <summary>Tests the current pose before sampling and refining other allowed horizontal positions.</summary>
        /// <param name="query">Current visibility, clearance and cursor aim constraints.</param>
        /// <param name="projectiles">The same forecaster that will simulate the emitted bullet.</param>
        /// <param name="settings">Validated distance and sampling limits.</param>
        /// <param name="current">Currently displayed horizontal displacement.</param>
        /// <param name="target">Physical cursor target position.</param>
        /// <param name="collider">Collider selected by the cursor ray.</param>
        /// <param name="destination">Receives the best feasible displacement.</param>
        /// <returns>True when at least one visible clear shooting pose exists.</returns>
        internal static bool TryFind(PlayerToolAimQuery query, PlayerToolProjectiles projectiles, PlayerToolWeaponSettings settings,
            Vector2 current, Vector3 target, Collider collider, out Vector2 destination)
        {
            Candidate best = Evaluate(query, projectiles, settings.Distance, current, target, collider);
            Consider(Evaluate(query, projectiles, settings.Distance, Vector2.zero, target, collider), ref best);
            // A bounded disc search runs once per accepted shot, never during ordinary idle aiming.
            for (int sample = 0; sample < settings.ShotAlignmentSamples; sample++)
            {
                float angle = sample * 2.39996323f;
                Vector2 candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * (settings.RepositionDistance * Mathf.Sqrt((sample + 0.5f) / settings.ShotAlignmentSamples));
                Consider(Evaluate(query, projectiles, settings.Distance, candidate, target, collider), ref best);
            }
            // Refine near the winner without losing the current pose as a valid baseline.
            float step = settings.RepositionDistance * 0.1f;
            for (int pass = 0; pass < 3; pass++, step *= 0.5f)
            {
                Vector2 centre = best.Offset;
                for (int side = 0; side < 4; side++)
                {
                    Vector2 candidate = centre + PlayerToolAimPose.RefinementOffset(side, step);
                    if (candidate.sqrMagnitude <= settings.RepositionDistance * settings.RepositionDistance)
                        Consider(Evaluate(query, projectiles, settings.Distance, candidate, target, collider), ref best);
                }
            }
            destination = best.Offset;
            return best.Priority < int.MaxValue;
        }

        /// <summary>Uses the real impact fit, including selected orientation, scale fallback and penetration allowance.</summary>
        /// <param name="query">Current feasible-pose query.</param>
        /// <param name="projectiles">Shared physical trajectory forecaster.</param>
        /// <param name="range">Maximum bullet distance.</param>
        /// <param name="candidate">Horizontal pose being tested.</param>
        /// <param name="target">Cursor target position.</param>
        /// <param name="collider">Expected physical target.</param>
        /// <returns>A ranked pose; infeasible geometry receives the lowest priority.</returns>
        private static Candidate Evaluate(PlayerToolAimQuery query, PlayerToolProjectiles projectiles, float range,
            Vector2 candidate, Vector3 target, Collider collider)
        {
            float cost = query.Score(candidate, out Vector3 muzzle, out Vector3 direction);
            if (!float.IsFinite(cost))
                return new Candidate(candidate, int.MaxValue, float.PositiveInfinity, cost);
            PlayerToolShotPrediction prediction = projectiles.Preview(muzzle, direction, range);
            // Hitting the requested object always outranks an attractive VFX on a different obstruction.
            int priority = prediction.Hit ? (prediction.Contact.collider == collider ? 0 : 2) : 4;
            if (!prediction.HasImpact)
                priority++;
            Vector3 point = prediction.HasImpact ? prediction.Position : prediction.Hit ? prediction.Contact.point : prediction.Position;
            return new Candidate(candidate, priority, (point - target).sqrMagnitude, cost);
        }

        /// <summary>Keeps smaller impact error, using arm cost only for effectively equal outcomes.</summary>
        /// <param name="candidate">New forecast result.</param>
        /// <param name="best">Receives a better feasible result.</param>
        private static void Consider(Candidate candidate, ref Candidate best)
        {
            if (candidate.Priority < best.Priority || candidate.Priority == best.Priority
                && (candidate.Error < best.Error - 0.000001f || Mathf.Abs(candidate.Error - best.Error) <= 0.000001f && candidate.Cost < best.Cost))
                best = candidate;
        }

        #endregion

        #endregion
    }
}
