using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Groups candidates sharing one draw allowance while keeping overridden variants independent.</summary>
    internal sealed class OrderDrawGroup
    {
        #region Fields

        internal readonly string Name;
        internal int Quantity { get; private set; }
        internal readonly int Required;
        internal readonly List<OrderCandidate> Candidates = new List<OrderCandidate>();
        internal int Pending;
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
            Pending = Required;
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
                        OrderDrawGroup independent = new OrderDrawGroup(variant.Name, new OrderCandidate(entry, variant).Quantity, variant.MinimumDraws,
                            variant.MaximumDraws, variant.RandomRequiredDraws, variant.RequiredDrawRange, sample);
                        independent.Add(new OrderCandidate(entry, variant), selection);
                        groups.Add(independent);
                    }
                // No parent reservation remains when all enabled candidates have their own limits.
                if (hasShared)
                    groups.Add(shared);
                AddCombos(groups, entry, selection, sample);
            }
            return groups;
        }

        /// <summary>Shares one reservation and allowance across every compatible combo of a base order.</summary>
        /// <param name="groups">Pools accumulated for this spawn.</param>
        /// <param name="entry">Parent whose combo candidates share an allowance.</param>
        /// <param name="selection">Policy controlling candidate eligibility.</param>
        /// <param name="sample">Whether random policies may sample values.</param>
        private static void AddCombos(List<OrderDrawGroup> groups, OrderEntry entry, OrderSelection selection, bool sample)
        {
            // Only members of feasible combinations contribute to derived shared limits.
            List<OrderCandidate> candidates = new List<OrderCandidate>();
            List<OrderRecipeVariant> members = new List<OrderRecipeVariant>();
            foreach (OrderRecipeVariant[] combo in OrderComboCandidates.Enumerate(entry))
            {
                candidates.Add(new OrderCandidate(entry, combo, OrderComboCandidates.Quantity(entry, combo, sample)));
                foreach (OrderRecipeVariant variant in combo)
                    if (!members.Contains(variant))
                        members.Add(variant);
            }
            if (candidates.Count == 0)
                return;
            Vector3Int limits = OrderComboCandidates.Limits(entry, members.ToArray(), sample);
            OrderDrawGroup combined = new OrderDrawGroup(entry.Name + " Combos", int.MaxValue, limits.x, limits.z,
                limits.x != limits.y, new Vector2Int(limits.x, limits.y), sample);
            foreach (OrderCandidate candidate in candidates)
            {
                combined.Add(candidate, selection);
                combined.Quantity = Mathf.Min(combined.Quantity, candidate.Quantity);
            }
            groups.Add(combined);
        }

        /// <summary>Filters zero weights only in weighted selection while retaining invalid mandatory pools for diagnostics.</summary>
        /// <param name="candidate">Base order or recipe variant sharing this allowance.</param>
        /// <param name="selection">Current extraction policy.</param>
        private void Add(OrderCandidate candidate, OrderSelection selection)
        {
            if (candidate.Weight > 0f || candidate.Combo == null && selection != OrderSelection.WeightedRandom)
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
