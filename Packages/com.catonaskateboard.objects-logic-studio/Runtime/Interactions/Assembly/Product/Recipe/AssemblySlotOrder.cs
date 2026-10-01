namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reserves numbered magnets while allowing unnumbered insertions between required positions.</summary>
    internal static class AssemblySlotOrder
    {
        #region Methods

        #region Selection

        /// <summary>Finds whether a magnet may receive the next physical ingredient.</summary>
        /// <param name="magnets">Authored placement slots.</param>
        /// <param name="occupied">Current occupancy, or null before initialization.</param>
        /// <param name="index">Candidate slot.</param>
        /// <param name="ordinal">Next physical insertion number.</param>
        /// <returns>True for the required pending magnet or an available unnumbered position.</returns>
        internal static bool Allows(AssemblyMagnet[] magnets, bool[] occupied, int index, int ordinal)
        {
            // A detached numbered part must be replaced before assembly can advance again.
            int required = -1;
            for (int candidate = 0; candidate < magnets.Length; candidate++)
                if ((occupied == null || !occupied[candidate]) && magnets[candidate].Order > 0
                    && magnets[candidate].Order <= ordinal
                    && (required < 0 || magnets[candidate].Order < magnets[required].Order))
                    required = candidate;
            return required >= 0 ? required == index : magnets[index].Order == 0;
        }

        #endregion

        #endregion
    }
}
