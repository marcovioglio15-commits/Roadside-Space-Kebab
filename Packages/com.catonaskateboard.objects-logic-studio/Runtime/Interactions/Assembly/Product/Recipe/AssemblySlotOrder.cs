using CatOnASkateboard.StudioIdentity;
using System.Collections.Generic;

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

        /// <summary>Protects quantity capacity needed by every still-empty numbered magnet.</summary>
        /// <param name="settings">Recipe and numbered magnets.</param>
        /// <param name="counts">Units already supplied for each recipe flag.</param>
        /// <param name="occupied">Current occupancy, or null before initialization.</param>
        /// <param name="selected">Candidate magnet receiving this insertion.</param>
        /// <param name="flag">Incoming ingredient flag.</param>
        /// <param name="units">Incoming logical units.</param>
        /// <returns>True when future numbered slots still have sufficient recipe quantity.</returns>
        internal static bool HasCapacity(AssemblyProductSettings settings, IReadOnlyDictionary<ObjectFlag, int> counts,
            bool[] occupied, int selected, ObjectFlag flag, int units)
        {
            // A multi-unit ingredient cannot exhaust units reserved for a later required slot.
            long available = 0;
            int generic = 0;
            long remainingUnits = 0;
            int inserted = 1;
            int lastRequired = 0;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
            {
                counts.TryGetValue(ingredient.Flag, out int count);
                int remaining = ingredient.Count - count - (ingredient.Flag == flag ? units : 0);
                remainingUnits += remaining;
                int required = 0;
                for (int index = 0; index < settings.Magnets.Length; index++)
                    if (index != selected && (occupied == null || !occupied[index]) && settings.Magnets[index].Order > 0
                        && !settings.Magnets[index].AnyIngredient && settings.Magnets[index].Flag == ingredient.Flag)
                        required++;
                if (required > remaining)
                    return false;
                available += remaining - required;
            }
            for (int index = 0; index < settings.Magnets.Length; index++)
            {
                if (occupied != null && occupied[index])
                    inserted++;
                lastRequired = System.Math.Max(lastRequired, settings.Magnets[index].Order);
                if (index != selected && (occupied == null || !occupied[index]) && settings.Magnets[index].Order > 0
                    && settings.Magnets[index].AnyIngredient)
                    generic++;
            }
            return available >= generic && remainingUnits >= lastRequired - inserted;
        }

        #endregion

        #endregion
    }
}
