using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares deterministic magnet selection between ordinary insertion and deferred two-ingredient starts.</summary>
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

        /// <summary>Prepares an empty recipe's first two insertions while leaving both physical objects usable.</summary>
        /// <param name="settings">Validated destination recipe.</param>
        /// <param name="first">Completed product waiting to become the first ingredient.</param>
        /// <param name="second">Additional ingredient that starts the new assembly.</param>
        /// <param name="firstMagnet">Receives the first insertion slot.</param>
        /// <param name="firstFlag">Receives the first ingredient's recipe category.</param>
        /// <param name="secondMagnet">Receives the second insertion slot.</param>
        /// <param name="secondFlag">Receives the second ingredient's recipe category.</param>
        /// <returns>True when both distinct objects fit consecutively, including mandatory magnet order.</returns>
        internal bool TrySelectPair(AssemblyProductSettings settings, ObjectGrab first, ObjectGrab second,
            out int firstMagnet, out ObjectFlag firstFlag, out int secondMagnet, out ObjectFlag secondFlag)
        {
            // Scratch progress never changes the prefab's counts or allocates a temporary product.
            secondMagnet = -1;
            secondFlag = null;
            if (!TrySelect(settings, null, null, 1, first, out firstMagnet, out firstFlag))
                return false;
            proposedCounts.Clear();
            proposedCounts.Add(firstFlag, first.Units);
            if (proposedSlots == null || proposedSlots.Length != settings.Magnets.Length)
                proposedSlots = new bool[settings.Magnets.Length];
            else
                Array.Clear(proposedSlots, 0, proposedSlots.Length);
            proposedSlots[firstMagnet] = true;
            return TrySelect(settings, proposedCounts, proposedSlots, 2, second, out secondMagnet, out secondFlag);
        }

        #endregion

        #endregion
    }
}
