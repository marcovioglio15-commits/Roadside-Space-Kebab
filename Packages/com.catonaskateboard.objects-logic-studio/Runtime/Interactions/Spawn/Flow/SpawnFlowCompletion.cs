using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks distinct completion events from one spawned visitor's selected interactions.</summary>
    internal sealed class SpawnFlowCompletion
    {
        #region State

        private readonly HashSet<ObjectInteraction> pending = new HashSet<ObjectInteraction>();
        private bool requireAll;

        #endregion

        #region Methods

        #region Progress

        /// <summary>Maps prefab references to their exact generated components before activating the visitor.</summary>
        /// <param name="step">Authored sources and any/all policy.</param>
        /// <param name="original">Prefab components in hierarchy order.</param>
        /// <param name="generated">Corresponding components on the inactive clone.</param>
        /// <returns>True when every selected interaction maps to a distinct clone component.</returns>
        internal bool Bind(SpawnFlowStep step, ObjectInteraction[] original, ObjectInteraction[] generated)
        {
            // No progress or component reference survives a visitor change.
            Clear();
            requireAll = step.RequireAll;
            foreach (ObjectInteraction source in step.Completions)
            {
                int index = Array.IndexOf(original, source);
                if (index < 0 || index >= generated.Length || !pending.Add(generated[index]))
                    return false;
            }
            return pending.Count > 0;
        }

        /// <summary>Counts each selected component once, regardless of repeated dialogue completions.</summary>
        /// <param name="source">Component that completed on the current visitor.</param>
        /// <returns>True when this completion satisfies the visit's any/all policy.</returns>
        internal bool Observe(ObjectInteraction source)
        {
            // Other visitors and repeated notifications cannot satisfy outstanding conditions.
            return pending.Remove(source) && (!requireAll || pending.Count == 0);
        }

        /// <summary>Releases component references when the visitor leaves or the run stops.</summary>
        internal void Clear()
        {
            // Keep collection capacity for the next spawn.
            pending.Clear();
        }

        #endregion

        #endregion
    }
}
