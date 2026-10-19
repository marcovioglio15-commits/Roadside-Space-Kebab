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

        /// <summary>Builds candidate groups using only names reachable through configured extraction pools.</summary>
        /// <param name="catalog">Validated shared order definitions.</param>
        /// <param name="pools">Resolved pool membership for this spawn.</param>
        /// <param name="sample">Whether to sample the required ranges for an actual spawn.</param>
        /// <returns>Parent groups and independently limited variants in authored order.</returns>
        internal static List<OrderDrawGroup> Create(OrderCatalog catalog, List<OrderDrawPool> pools, bool sample)
        {
            // Build one name set so group construction does not inspect pool membership repeatedly.
            List<OrderDrawGroup> groups = new List<OrderDrawGroup>();
            HashSet<string> selectable = OrderCatalog.SelectableNames(pools);
            // Parent, variant and combo limits remain independent where they were authored independently.
            foreach (OrderEntry entry in catalog.Entries)
            {
                OrderDrawGroup shared = new OrderDrawGroup(entry.Name, entry.Quantity, entry.MinimumDraws,
                    entry.MaximumDraws, entry.RandomRequiredDraws, entry.RequiredDrawRange, sample);
                bool hasShared = false;
                if (!entry.UseRecipeModifiers || entry.IncludeBaseOrder)
                    hasShared = shared.Add(new OrderCandidate(entry), selectable);
                if (entry.UseRecipeModifiers)
                    foreach (OrderRecipeVariant variant in entry.Variants)
                    {
                        if (!variant.UseDrawLimits)
                        {
                            hasShared |= shared.Add(new OrderCandidate(entry, variant), selectable);
                            continue;
                        }
                        OrderDrawGroup independent = new OrderDrawGroup(variant.Name, new OrderCandidate(entry, variant).Quantity, variant.MinimumDraws,
                            variant.MaximumDraws, variant.RandomRequiredDraws, variant.RequiredDrawRange, sample);
                        if (independent.Add(new OrderCandidate(entry, variant), selectable))
                            groups.Add(independent);
                    }
                // No parent reservation remains when all enabled candidates have their own limits.
                if (hasShared)
                    groups.Add(shared);
                AddCombos(groups, entry, selectable, sample);
            }
            return groups;
        }

        /// <summary>Shares one reservation and allowance across every compatible combo of a base order.</summary>
        /// <param name="groups">Pools accumulated for this spawn.</param>
        /// <param name="entry">Parent whose combo candidates share an allowance.</param>
        /// <param name="selectable">Names available through at least one active pool.</param>
        /// <param name="sample">Whether random policies may sample values.</param>
        private static void AddCombos(List<OrderDrawGroup> groups, OrderEntry entry, HashSet<string> selectable, bool sample)
        {
            // Only members of feasible combinations contribute to derived shared limits.
            List<OrderCandidate> candidates = new List<OrderCandidate>();
            List<OrderRecipeVariant> members = new List<OrderRecipeVariant>();
            foreach (OrderRecipeVariant[] combo in OrderComboCandidates.Enumerate(entry))
            {
                OrderCandidate candidate = new OrderCandidate(entry, combo, OrderComboCandidates.Quantity(entry, combo, sample));
                if (!selectable.Contains(candidate.Name))
                    continue;
                candidates.Add(candidate);
                foreach (OrderRecipeVariant variant in combo)
                    if (!members.Contains(variant))
                        members.Add(variant);
            }
            // Unlisted combinations do not create a draw-limit group.
            if (candidates.Count == 0)
                return;
            Vector3Int limits = OrderComboCandidates.Limits(entry, members.ToArray(), sample);
            OrderDrawGroup combined = new OrderDrawGroup(entry.Name + " Combos", int.MaxValue, limits.x, limits.z,
                limits.x != limits.y, new Vector2Int(limits.x, limits.y), sample);
            foreach (OrderCandidate candidate in candidates)
            {
                combined.Add(candidate, selectable);
                combined.Quantity = Mathf.Min(combined.Quantity, candidate.Quantity);
            }
            if (combined.Candidates.Count > 0)
                groups.Add(combined);
        }

        /// <summary>Adds a candidate only when at least one active pool exposes its exact name.</summary>
        /// <param name="candidate">Base order or recipe variant sharing this allowance.</param>
        /// <param name="selectable">Names available through active pools.</param>
        /// <returns>True when the group gained a candidate.</returns>
        private bool Add(OrderCandidate candidate, HashSet<string> selectable)
        {
            // One shared group can serve several pool members while preserving its original allowance.
            if (!selectable.Contains(candidate.Name))
                return false;
            Candidates.Add(candidate);
            return true;
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
