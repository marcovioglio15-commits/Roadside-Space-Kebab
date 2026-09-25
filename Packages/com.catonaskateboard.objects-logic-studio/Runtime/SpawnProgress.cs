using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains completion counts and draw order for exactly one source prefab instance.</summary>
    internal sealed class SpawnProgress
    {
        #region Fields

        internal readonly int[] Counts;
        internal readonly Transform Source;
        internal int Cycles;
        internal int Sequence;
        internal float Due;
        internal bool Pending;

        #endregion

        #region Methods

        #region Progress

        /// <summary>Allocates counters once when this source first completes a matching interaction.</summary>
        /// <param name="source">Runtime prefab root, never the shared asset.</param>
        /// <param name="count">Number of configured conditions.</param>
        internal SpawnProgress(Transform source, int count)
        {
            // Separate roots can never contribute to the same set of counters.
            Source = source;
            Counts = new int[count];
        }

        /// <summary>Tests all or any completion quantities on this one instance.</summary>
        /// <param name="settings">Validated condition configuration.</param>
        /// <returns>True when the next cycle may be scheduled.</returns>
        internal bool Ready(SpawnManagementSettings settings)
        {
            // Pending and exhausted instances ignore extra events until a new cycle can begin.
            if (Pending || Cycles >= (settings.Repeat ? settings.CyclesPerInstance : 1))
                return false;
            for (int index = 0; index < Counts.Length; index++)
                if (Counts[index] >= settings.Conditions[index].Count)
                {
                    if (!settings.RequireAll)
                        return true;
                }
                else if (settings.RequireAll)
                    return false;
            return settings.RequireAll;
        }

        #endregion

        #endregion
    }
}
