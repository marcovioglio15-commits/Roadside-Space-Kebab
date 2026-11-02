using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks distinct completion events from one spawned visitor's selected interactions.</summary>
    internal sealed class SpawnFlowCompletion
    {
        #region State

        private readonly List<(ObjectInteraction Source, DialogueStepReference Step)> pending = new List<(ObjectInteraction, DialogueStepReference)>();
        private bool requireAll;

        #endregion

        #region Methods

        #region Progress

        /// <summary>Maps prefab references to their exact generated components before activating the visitor.</summary>
        /// <param name="step">Authored sources and any/all policy.</param>
        /// <param name="original">Prefab components in hierarchy order.</param>
        /// <param name="generated">Corresponding components on the inactive clone.</param>
        /// <returns>True when every selected interaction maps to its clone component.</returns>
        internal bool Bind(SpawnFlowStep step, ObjectInteraction[] original, ObjectInteraction[] generated)
        {
            // No progress or component reference survives a visitor change.
            Clear();
            requireAll = step.RequireAll;
            for (int selected = 0; selected < step.Completions.Length; selected++)
            {
                ObjectInteraction source = step.Completions[selected];
                int index = Array.IndexOf(original, source);
                if (index < 0 || index >= generated.Length || generated[index] == null)
                    return false;
                pending.Add((generated[index], step.CompletionAt(selected)));
            }
            return pending.Count > 0;
        }

        /// <summary>Counts each selected checkpoint once, including several branches on one dialogue.</summary>
        /// <param name="source">Component that completed on the current visitor.</param>
        /// <param name="moment">Whole interaction or entry checkpoint boundary.</param>
        /// <returns>True when this completion satisfies the visit's any/all policy.</returns>
        internal bool Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Other visitors and repeated notifications cannot satisfy outstanding conditions.
            bool matched = false;
            // One boundary can satisfy overlapping block and entry selections without counting either twice.
            for (int index = pending.Count - 1; index >= 0; index--)
                if (pending[index].Source == source && (pending[index].Step?.Matches(source, moment) ?? moment == InteractionMoment.Completed))
                {
                    pending.RemoveAt(index);
                    matched = true;
                }
            return matched && (!requireAll || pending.Count == 0);
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
