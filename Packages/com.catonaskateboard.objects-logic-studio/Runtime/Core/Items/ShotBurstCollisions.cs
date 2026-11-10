using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Lets a single burst separate before its pieces collide with each other.</summary>
    internal static class ShotBurstCollisions
    {
        #region State

        private static readonly List<(Collider First, Collider Second, float Deadline)> pairs = new List<(Collider, Collider, float)>(16);
        private static readonly List<Collider[]> earlier = new List<Collider[]>(16);
        private static int impact;
        private static float checkedStep = float.NegativeInfinity;

        #endregion

        #region Methods

        #region Burst Pairs

        /// <summary>Ignores close solid pairs from the same shot, including outputs from separate area targets.</summary>
        /// <param name="output">Shapes on the newly released piece.</param>
        internal static void Add(Collider[] output)
        {
            if (output == null)
                return;
            // All direct and area pulses share an impact ID and are processed before the next physics step.
            if (impact != InteractionProjectileInput.Impact)
            {
                earlier.Clear();
                impact = InteractionProjectileInput.Impact;
            }
            foreach (Collider[] other in earlier)
                IgnoreBriefly(output, other);
            earlier.Add(output);
            checkedStep = Time.fixedTime;
        }

        /// <summary>Gives a released piece time to clear nearby carry geometry without changing existing exclusions.</summary>
        /// <param name="output">Shapes on the released piece.</param>
        /// <param name="others">Shapes it needs to clear during launch.</param>
        internal static void IgnoreBriefly(Collider[] output, Collider[] others)
        {
            // Restore the pairs on the usual physics cadence, even if the original carrier disappears.
            checkedStep = Time.fixedTime;
            float deadline = checkedStep + 0.3f;
            foreach (Collider first in output)
                foreach (Collider second in others)
                    if (first != null && second != null && first != second && !first.isTrigger && !second.isTrigger
                        && first.enabled && second.enabled && first.gameObject.activeInHierarchy && second.gameObject.activeInHierarchy
                        && Close(first, second) && !Physics.GetIgnoreCollision(first, second))
                    {
                        Physics.IgnoreCollision(first, second);
                        pairs.Add((first, second, deadline));
                    }
        }

        /// <summary>Skips distant output pairs that cannot meet during the initial separation.</summary>
        /// <param name="first">New output shape.</param>
        /// <param name="second">Earlier output shape.</param>
        /// <returns>True when their starting bounds are close.</returns>
        private static bool Close(Collider first, Collider second)
        {
            Bounds bounds = first.bounds;
            bounds.Expand(0.9f);
            return bounds.Intersects(second.bounds);
        }

        /// <summary>Releases the shot's temporary collider lists after all of its targets have run.</summary>
        internal static void EndImpacts() => earlier.Clear();

        /// <summary>Restores pairs after a physics step separates the pieces or the short grace period ends.</summary>
        /// <param name="immediate">Restores every pair when the observer or Play session ends.</param>
        internal static void Restore(bool immediate = false)
        {
            if (immediate)
                EndImpacts();
            // Empty or repeated render passes perform no collider queries.
            if (pairs.Count == 0 || !immediate && checkedStep == Time.fixedTime)
                return;
            checkedStep = Time.fixedTime;
            for (int index = pairs.Count - 1; index >= 0; index--)
            {
                (Collider first, Collider second, float deadline) = pairs[index];
                if (first != null && second != null)
                {
                    if (!immediate && checkedStep < deadline && (checkedStep < deadline - 0.2f || first.bounds.Intersects(second.bounds)))
                        continue;
                    Physics.IgnoreCollision(first, second, false);
                }
                pairs[index] = pairs[pairs.Count - 1];
                pairs.RemoveAt(pairs.Count - 1);
            }
        }

        #endregion

        #endregion
    }
}
