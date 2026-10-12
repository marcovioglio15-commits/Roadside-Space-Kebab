using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Groups candidates sharing one draw allowance while keeping overridden variants independent.</summary>
    internal sealed class OrderDrawGroup
    {
        #region Fields

        internal readonly string Name;
        internal readonly int Quantity;
        internal readonly int Required;
        internal readonly List<OrderCandidate> Candidates = new List<OrderCandidate>();
        internal int Remaining;

        #endregion
        #region Methods
        #region Construction

        /// <summary>Captures the required reservation and optional remainder without modifying the catalog.</summary>
        /// <param name="name">Order or overridden variant used in configuration diagnostics.</param>
        /// <param name="quantity">Slots occupied by every candidate in this group.</param>
        /// <param name="minimum">Fixed required draws.</param>
        /// <param name="maximum">Total draw allowance.</param>
        /// <param name="random">Whether required draws use a range.</param>
        /// <param name="range">Inclusive required-draw range.</param>
        /// <param name="sample">Sample at spawn; validation instead checks the highest required value.</param>
        private OrderDrawGroup(string name, int quantity, int minimum, int maximum, bool random, Vector2Int range, bool sample)
        {
            Name = name;
            Quantity = quantity;
            Required = random ? sample ? Random.Range(range.x, range.y + 1) : range.y : minimum;
            Remaining = maximum - Required;
        }

        /// <summary>Builds active draw pools using the same grouping for validation and extraction.</summary>
        /// <param name="catalog">Validated shared order definitions.</param>
        /// <param name="selection">Selection policy controlling zero-weight exclusion.</param>
        /// <param name="sample">Whether to sample the required ranges for an actual spawn.</param>
        /// <returns>Parent pools and independently limited variants in authored order.</returns>
        internal static List<OrderDrawGroup> Create(OrderCatalog catalog, OrderSelection selection, bool sample)
        {
            List<OrderDrawGroup> groups = new List<OrderDrawGroup>();
            foreach (OrderEntry entry in catalog.Entries)
            {
                OrderDrawGroup shared = new OrderDrawGroup(entry.Name, entry.Quantity, entry.MinimumDraws,
                    entry.MaximumDraws, entry.RandomRequiredDraws, entry.RequiredDrawRange, sample);
                bool hasShared = !entry.UseRecipeModifiers || entry.IncludeBaseOrder;
                if (hasShared)
                    shared.Add(new OrderCandidate(entry), selection);
                if (entry.UseRecipeModifiers)
                    foreach (OrderRecipeVariant variant in entry.Variants)
                    {
                        if (!variant.UseDrawLimits)
                        {
                            hasShared = true;
                            shared.Add(new OrderCandidate(entry, variant), selection);
                            continue;
                        }
                        OrderDrawGroup independent = new OrderDrawGroup(variant.Name, entry.Quantity, variant.MinimumDraws,
                            variant.MaximumDraws, variant.RandomRequiredDraws, variant.RequiredDrawRange, sample);
                        independent.Add(new OrderCandidate(entry, variant), selection);
                        groups.Add(independent);
                    }
                // No parent reservation remains when all enabled candidates have their own limits.
                if (hasShared)
                    groups.Add(shared);
            }
            return groups;
        }

        /// <summary>Filters zero weights only in weighted selection while retaining invalid mandatory pools for diagnostics.</summary>
        /// <param name="candidate">Base order or recipe variant sharing this allowance.</param>
        /// <param name="selection">Current extraction policy.</param>
        private void Add(OrderCandidate candidate, OrderSelection selection)
        {
            if (selection != OrderSelection.WeightedRandom || candidate.Weight > 0f)
                Candidates.Add(candidate);
        }

        #endregion
        #region Validation

        /// <summary>Checks a fixed or randomized required count without clamping serialized values.</summary>
        /// <param name="minimum">Fixed required draws.</param>
        /// <param name="maximum">Total draw allowance.</param>
        /// <param name="random">Whether the required range is active.</param>
        /// <param name="range">Inclusive required range.</param>
        /// <returns>True when all possible required counts fit the allowance and random sampling is safe.</returns>
        internal static bool ValidLimits(int minimum, int maximum, bool random, Vector2Int range)
        {
            int lower = random ? range.x : minimum;
            int upper = random ? range.y : minimum;
            return lower >= 0 && upper >= lower && upper < int.MaxValue && maximum >= upper;
        }

        #endregion
        #endregion
    }
}
