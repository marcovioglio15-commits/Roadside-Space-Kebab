using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares named order candidates between objects while leaving extraction settings on each interaction.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Order Catalog")]
    public sealed class OrderCatalog : ScriptableObject
    {
        #region Fields

        [Header("Available Orders")]
        [Tooltip("Named candidates shared by every interaction referencing this catalog. Quantities occupy individual board slots.")]
        public OrderEntry[] Entries = Array.Empty<OrderEntry>();
        [Tooltip("Optional first-stage pools. Each can include orders, variants and generated combos; an empty list uses all candidates.")]
        public OrderPool[] Pools = Array.Empty<OrderPool>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Reports invalid candidates without changing authored names, quantities or weights.</summary>
        /// <param name="warning">Receives the first invalid candidate.</param>
        /// <returns>True when definitions and active extraction pools are valid.</returns>
        public bool TryValidate(out string warning)
        {
            // Names are stable keys for Consumption dialogue filters on all catalog consumers.
            warning = "Add at least one available order.";
            if (Entries == null || Entries.Length == 0)
                return false;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Entries.Length; index++)
            {
                OrderEntry entry = Entries[index];
                warning = $"Order {index + 1} needs a positive quantity and complete settings.";
                if (entry == null || entry.Quantity <= 0
                    || !OrderRecipeVariant.ValidateMetadata(entry.Name, entry.Text, entry.Weight, names, out warning))
                    return false;
                warning = $"Order '{entry.Name}' needs non-negative draw limits, an ordered required range and Maximum Draws at least as large as every required value.";
                if (!OrderDrawGroup.ValidLimits(entry.MinimumDraws, entry.MaximumDraws, entry.RandomRequiredDraws, entry.RequiredDrawRange))
                    return false;
                if (entry.TextStyle != null && !entry.TextStyle.TryValidate(out warning))
                    return false;
                warning = $"Order '{entry.Name}' Price must be finite and between zero and ten thousand.";
                if (entry.OverridePrice && !CollectedPrice.Valid(entry.Price))
                    return false;
                if (!ObjectFlagRules.TryValidate(entry.Flags, false, out warning))
                {
                    warning = $"Order {index + 1}, Identity Flags: {warning}";
                    return false;
                }
                if (!entry.UseRecipeModifiers)
                    continue;
                warning = $"Order '{entry.Name}' needs at least one recipe modifier variant, or disable Recipe Modifiers.";
                if (entry.Variants == null || entry.Variants.Length == 0)
                    return false;
                foreach (OrderRecipeVariant variant in entry.Variants)
                {
                    warning = $"Order '{entry.Name}' contains an empty recipe variant.";
                    if (variant == null || !OrderRecipeVariant.ValidateMetadata(variant.Name, variant.Text, variant.Weight, names, out warning, true)
                        || !ObjectFlagRules.TryValidate(variant.Flags, true, out warning)
                        || !ObjectFlagRules.TryValidate(variant.ExcludedFlags, true, out warning))
                        return false;
                    if (variant.TextStyle != null && !variant.TextStyle.TryValidate(out warning))
                        return false;
                    warning = $"Variant '{variant.Name}' needs a positive independent quantity.";
                    if (variant.UseDrawLimits && variant.UseQuantity && variant.Quantity <= 0)
                        return false;
                    warning = $"Variant '{variant.Name}' needs non-negative draw limits and a required range within Maximum Draws.";
                    if (variant.UseDrawLimits && !OrderDrawGroup.ValidLimits(variant.MinimumDraws, variant.MaximumDraws,
                        variant.RandomRequiredDraws, variant.RequiredDrawRange))
                        return false;
                    warning = $"Variant '{variant.Name}' needs additive or subtractive flags; the same flag cannot appear in both lists.";
                    if (variant.Flags.Length + variant.ExcludedFlags.Length == 0)
                        return false;
                    foreach (ObjectFlag flag in variant.Flags)
                        if (Array.IndexOf(variant.ExcludedFlags, flag) >= 0)
                            return false;
                }
                if (entry.Combos == null || !entry.Combos.TryValidate(out warning))
                    return false;
                warning = $"Order '{entry.Name}' exceeds {OrderComboCandidates.MaximumCombinations} possible combos. Reduce the maximum combo size or number of variants.";
                if (entry.Combos.Enabled && !OrderComboCandidates.IsBounded(entry))
                    return false;
                foreach (OrderRecipeVariant[] combo in OrderComboCandidates.Enumerate(entry))
                {
                    warning = "A generated combo name conflicts with another order or variant: " + OrderComboCandidates.Name(entry, combo);
                    if (!names.Add(OrderComboCandidates.Name(entry, combo)))
                        return false;
                }
            }
            if (!ValidatePools(out warning))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Builds resolved pool candidates or one implicit pool when no custom pools are authored.</summary>
        /// <param name="defaultSelection">Selection mode used by the implicit pool for existing catalogs.</param>
        /// <returns>Ordered extraction pools with candidate weights resolved once.</returns>
        internal List<OrderDrawPool> CreateDrawPools(OrderSelection defaultSelection)
        {
            // Resolve dialogue keys once so each pool stores direct candidate data for this spawn.
            Dictionary<string, OrderCandidate> candidates = new Dictionary<string, OrderCandidate>(StringComparer.Ordinal);
            foreach (OrderCandidate candidate in Candidates())
                if (!string.IsNullOrWhiteSpace(candidate.Name))
                    candidates.TryAdd(candidate.Name, candidate);
            List<OrderDrawPool> pools = new List<OrderDrawPool>();
            // Existing catalogs without explicit pools keep their single-stage selection behavior.
            if (Pools == null || Pools.Length == 0)
            {
                OrderDrawPool pool = new OrderDrawPool("All Orders", 1f, defaultSelection);
                foreach (OrderCandidate candidate in candidates.Values)
                    pool.Add(candidate, 1f);
                pools.Add(pool);
                return pools;
            }
            // Custom pool order is preserved because it controls the first extraction stage.
            foreach (OrderPool definition in Pools)
            {
                if (definition == null || definition.Candidates == null)
                    continue;
                OrderDrawPool pool = new OrderDrawPool(definition.Name, definition.Weight, definition.Selection);
                foreach (OrderPoolEntry member in definition.Candidates)
                    if (member != null && !string.IsNullOrWhiteSpace(member.Candidate)
                        && candidates.TryGetValue(member.Candidate, out OrderCandidate candidate))
                        pool.Add(candidate, member.Weight);
                pools.Add(pool);
            }
            return pools;
        }

        /// <summary>Enumerates each enabled base order and recipe variant as an independent draw candidate.</summary>
        /// <returns>Enabled candidates sharing their parent identity and quantity.</returns>
        internal IEnumerable<OrderCandidate> Candidates()
        {
            if (Entries == null)
                yield break;
            foreach (OrderEntry entry in Entries)
            {
                if (entry == null)
                    continue;
                if (!entry.UseRecipeModifiers || entry.IncludeBaseOrder)
                    yield return new OrderCandidate(entry);
                if (entry.UseRecipeModifiers && entry.Variants != null)
                    foreach (OrderRecipeVariant variant in entry.Variants)
                        if (variant != null)
                            yield return new OrderCandidate(entry, variant);
                foreach (OrderRecipeVariant[] combo in OrderComboCandidates.Enumerate(entry))
                    yield return new OrderCandidate(entry, combo, OrderComboCandidates.Quantity(entry, combo, false));
            }
        }

        /// <summary>Checks names against candidates reachable through at least one active pool.</summary>
        /// <param name="name">Exact name saved by a dialogue filter.</param>
        /// <returns>True when the named candidate can be extracted.</returns>
        public bool ContainsOrder(string name)
        {
            HashSet<string> selectable = SelectableNames(OrderSelection.UniformRandom);
            return selectable.Contains(name);
        }

        /// <summary>Supplies active order, variant and combo names to dialogue selectors.</summary>
        /// <returns>Names of enabled draw candidates in authored order.</returns>
        public IEnumerable<string> OrderNames()
        {
            HashSet<string> selectable = SelectableNames(OrderSelection.UniformRandom);
            foreach (OrderCandidate candidate in Candidates())
                if (selectable.Contains(candidate.Name))
                    yield return candidate.Name;
        }

        /// <summary>Supplies every valid catalog name to pool membership controls.</summary>
        /// <returns>Base orders, enabled variants and generated compatible combos.</returns>
        public IEnumerable<string> CandidateNames()
        {
            foreach (OrderCandidate candidate in Candidates())
                if (!string.IsNullOrWhiteSpace(candidate.Name))
                    yield return candidate.Name;
        }

        /// <summary>Validates pool names, candidate references and the availability of a weighted draw path.</summary>
        /// <param name="warning">Receives the first invalid pool configuration.</param>
        /// <returns>True when the implicit pool or at least one configured pool can provide a candidate.</returns>
        private bool ValidatePools(out string warning)
        {
            warning = string.Empty;
            if (Pools == null || Pools.Length == 0)
                return true;
            HashSet<string> poolNames = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, OrderCandidate> candidates = new Dictionary<string, OrderCandidate>(StringComparer.Ordinal);
            // Candidate metadata supplies the base weight used by each pool's weighted mode.
            foreach (OrderCandidate candidate in Candidates())
                candidates.TryAdd(candidate.Name, candidate);
            bool selectablePool = false;
            // Pool labels are stable identifiers in diagnostics; member names link to catalog candidates.
            foreach (OrderPool pool in Pools)
            {
                warning = "Each order pool needs a unique name, finite non-negative weight and supported selection mode.";
                if (pool == null || string.IsNullOrWhiteSpace(pool.Name) || !poolNames.Add(pool.Name)
                    || !float.IsFinite(pool.Weight) || pool.Weight < 0f
                    || pool.Selection is not (OrderSelection.UniformRandom or OrderSelection.WeightedRandom))
                    return false;
                HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
                // Empty pools are allowed when another active pool supplies at least one candidate.
                if (pool.Candidates == null)
                {
                    warning = "Order pool '" + pool.Name + "' needs a candidate list.";
                    return false;
                }
                foreach (OrderPoolEntry member in pool.Candidates)
                {
                    warning = "Order pool '" + pool.Name + "' contains a missing or duplicated catalog candidate.";
                    if (member == null || string.IsNullOrWhiteSpace(member.Candidate))
                        return false;
                    if (!candidates.TryGetValue(member.Candidate, out OrderCandidate candidate) || !members.Add(member.Candidate))
                        return false;
                    warning = "Order pool '" + pool.Name + "' contains a non-finite or negative candidate weight.";
                    if (!float.IsFinite(member.Weight) || member.Weight < 0f)
                        return false;
                    // Weighted extraction uses this product directly after selecting the pool.
                    float effectiveWeight = member.Weight * candidate.Weight;
                    warning = "Order pool '" + pool.Name + "' produces a non-finite effective candidate weight.";
                    if (!float.IsFinite(effectiveWeight))
                        return false;
                    if (pool.Weight > 0f && (pool.Selection != OrderSelection.WeightedRandom
                        || effectiveWeight > 0f))
                        selectablePool = true;
                }
            }
            warning = "Add a candidate to at least one positive-weight pool, or leave Pools empty to use all catalog candidates.";
            return selectablePool;
        }

        /// <summary>Collects candidates that can be reached through positive-weight pools.</summary>
        /// <param name="defaultSelection">Fallback policy for catalogs without custom pools.</param>
        /// <returns>Unique candidate names in active pools.</returns>
        private HashSet<string> SelectableNames(OrderSelection defaultSelection)
        {
            return SelectableNames(CreateDrawPools(defaultSelection));
        }

        /// <summary>Collects names reachable through positive-weight pools for selection and limit checks.</summary>
        /// <param name="pools">Resolved pools for one interaction and spawn.</param>
        /// <returns>Unique candidates available in the current extraction.</returns>
        internal static HashSet<string> SelectableNames(List<OrderDrawPool> pools)
        {
            HashSet<string> selectable = new HashSet<string>(StringComparer.Ordinal);
            // Uniform pools ignore candidate weights; weighted pools omit zero-mass members.
            foreach (OrderDrawPool pool in pools)
            {
                if (pool.Weight <= 0f)
                    continue;
                foreach (KeyValuePair<string, float> member in pool.Candidates)
                    if (pool.Selection != OrderSelection.WeightedRandom || member.Value > 0f)
                        selectable.Add(member.Key);
            }
            return selectable;
        }

        #endregion
        #endregion
    }
}
