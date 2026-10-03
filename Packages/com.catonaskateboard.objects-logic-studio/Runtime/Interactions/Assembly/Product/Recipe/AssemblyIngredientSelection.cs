using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares deterministic magnet selection between insertion and competing recipe proposals.</summary>
    internal sealed class AssemblyIngredientSelection
    {
        #region State

        private readonly AssemblyCapacity capacity = new AssemblyCapacity();
        private readonly Dictionary<ObjectFlag, int> proposedCounts = new Dictionary<ObjectFlag, int>();
        private bool[] proposedSlots;

        #endregion

        #region Methods

        #region Eligibility

        /// <summary>Checks recipe membership before the first insertion makes later ordered magnets available.</summary>
        /// <param name="settings">Validated destination recipe.</param>
        /// <param name="grab">Ingredient whose identity and units may satisfy a recipe row.</param>
        /// <returns>True when at least one row accepts the ingredient's units and flags.</returns>
        internal static bool Matches(AssemblyProductSettings settings, ObjectGrab grab)
        {
            // Order is checked when choosing the actual pair; it must not prevent the second ingredient from waiting.
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
                if (grab.Units > 0 && grab.Units <= ingredient.Count)
                    foreach (ObjectFlag flag in ingredient.Flags)
                        if (grab.Identity.Has(flag))
                            return true;
            return false;
        }

        #endregion

        #region Selection

        /// <summary>Assigns one recipe flag and magnet without changing ingredient ownership.</summary>
        /// <param name="settings">Validated recipe and magnets.</param>
        /// <param name="counts">Units already inserted, or a proposed first ingredient.</param>
        /// <param name="occupied">Occupied magnets, or null for an empty product.</param>
        /// <param name="insertion">One-based physical insertion number.</param>
        /// <param name="grab">Eligible incoming ingredient.</param>
        /// <param name="magnet">Receives the selected magnet index.</param>
        /// <param name="flag">Receives the single recipe flag counted for this ingredient.</param>
        /// <returns>True when order, capacity and appearance bindings permit insertion.</returns>
        internal bool TrySelect(AssemblyProductSettings settings, IReadOnlyDictionary<ObjectFlag, int> counts,
            bool[] occupied, int insertion, ObjectGrab grab, out int magnet, out ObjectFlag flag)
        {
            // Prefer restricted magnets; recipe order resolves generic alternatives consistently.
            magnet = -1;
            flag = null;
            for (int pass = 0; pass < 2; pass++)
                for (int index = 0; index < settings.Magnets.Length; index++)
                {
                    AssemblyMagnet slot = settings.Magnets[index];
                    if (occupied != null && occupied[index] || slot.AnyIngredient != (pass == 1)
                        || !AssemblySlotOrder.Allows(settings.Magnets, occupied, index, insertion))
                        continue;
                    foreach (AssemblyIngredient ingredient in settings.Ingredients)
                        if (grab.Units > 0 && grab.Units <= ingredient.Count - AssemblyCapacity.Count(counts, ingredient.Flags))
                            foreach (ObjectFlag alternative in ingredient.Flags)
                                if ((slot.AnyIngredient || Array.IndexOf(slot.Flags, alternative) >= 0)
                                    && grab.Identity.Has(alternative)
                                    && capacity.CanFit(settings, counts, occupied, index, alternative, grab.Units)
                                    && (!slot.Appearance.HasChanges || slot.Appearance.CanBind(grab.Item)))
                                {
                                    magnet = index;
                                    flag = alternative;
                                    return true;
                                }
                }
            return false;
        }

        /// <summary>Clears scratch progress before evaluating a possible recipe without creating a product.</summary>
        /// <param name="settings">Validated recipe whose slots will be simulated.</param>
        internal void Begin(AssemblyProductSettings settings)
        {
            // Reuse buffers across contact queries and competing recipe checks.
            proposedCounts.Clear();
            if (proposedSlots == null || proposedSlots.Length != settings.Magnets.Length)
                proposedSlots = new bool[settings.Magnets.Length];
            else
                Array.Clear(proposedSlots, 0, proposedSlots.Length);
        }

        /// <summary>Appends an eligible ingredient to scratch progress without taking ownership.</summary>
        /// <param name="settings">Recipe being simulated.</param>
        /// <param name="grab">Next independent ingredient.</param>
        /// <param name="insertion">One-based proposed insertion number.</param>
        /// <param name="magnet">Receives the chosen magnet.</param>
        /// <param name="flag">Receives the counted recipe flag.</param>
        /// <returns>True when the ingredient fits after every previously proposed insertion.</returns>
        internal bool Append(AssemblyProductSettings settings, ObjectGrab grab, int insertion, out int magnet, out ObjectFlag flag)
        {
            // A rejected candidate leaves scratch progress unchanged for the next alternative.
            if (!TrySelect(settings, proposedCounts, proposedSlots, insertion, grab, out magnet, out flag))
                return false;
            proposedSlots[magnet] = true;
            proposedCounts.TryGetValue(flag, out int count);
            proposedCounts[flag] = count + grab.Units;
            return true;
        }

        /// <summary>Checks whether proposed ingredients finish the recipe instead of only matching a shared prefix.</summary>
        /// <param name="settings">Recipe whose required quantities and ordered slots are checked.</param>
        /// <returns>True when every mandatory requirement is satisfied.</returns>
        internal bool Completes(AssemblyProductSettings settings)
        {
            // Scratch and live products share the same mandatory quantity and ordered-slot rules.
            return Completes(settings, proposedCounts, proposedSlots, proposedCounts.Count > 0);
        }

        /// <summary>Evaluates completion for either proposed or actually attached ingredients.</summary>
        /// <param name="settings">Validated recipe.</param>
        /// <param name="counts">Logical units per recipe flag.</param>
        /// <param name="occupied">Occupied physical magnet slots.</param>
        /// <param name="hasIngredients">Whether at least one physical ingredient has been supplied.</param>
        /// <returns>True when every mandatory requirement is satisfied by a nonempty composition.</returns>
        internal static bool Completes(AssemblyProductSettings settings, IReadOnlyDictionary<ObjectFlag, int> counts,
            bool[] occupied, bool hasIngredients)
        {
            // Optional rows may remain empty; numbered slots still require their physical ingredient.
            if (!hasIngredients)
                return false;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
                if (!ingredient.Optional && AssemblyCapacity.Count(counts, ingredient.Flags) < ingredient.Count)
                    return false;
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (settings.Magnets[index].Order > 0 && !occupied[index])
                    return false;
            return true;
        }

        #endregion

        #endregion
    }
}
