using System;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses prefab entries without allocations inside an individual draw.</summary>
    internal static class SpawnDraw
    {
        #region Methods

        #region Selection

        /// <summary>Returns one eligible entry using the configured probability or authored order.</summary>
        /// <param name="settings">Validated output choices.</param>
        /// <param name="random">Random stream owned by this rule instance.</param>
        /// <param name="progress">Per-source sequence position.</param>
        /// <param name="used">Reused flags for draws without replacement.</param>
        /// <returns>An eligible choice index, or minus one if configuration changed during the draw.</returns>
        internal static int Select(SpawnManagementSettings settings, Random random, SpawnProgress progress, bool[] used)
        {
            // Sequence order is retained independently for each source instance.
            if (settings.Selection == SpawnSelection.Sequence)
                for (int offset = 0; offset < settings.Choices.Length; offset++)
                {
                    int index = progress.Sequence;
                    progress.Sequence = (progress.Sequence + 1) % settings.Choices.Length;
                    if (!used[index])
                        return index;
                }
            double total = 0d;
            for (int index = 0; index < settings.Choices.Length; index++)
                if (!used[index])
                    total += settings.Selection == SpawnSelection.WeightedRandom ? settings.Choices[index].Weight : 1d;
            if (total <= 0d)
                return -1;
            double draw = random.NextDouble() * total;
            for (int index = 0; index < settings.Choices.Length; index++)
            {
                if (used[index])
                    continue;
                draw -= settings.Selection == SpawnSelection.WeightedRandom ? settings.Choices[index].Weight : 1d;
                if (draw < 0d)
                    return index;
            }
            return -1;
        }

        #endregion

        #endregion
    }
}
