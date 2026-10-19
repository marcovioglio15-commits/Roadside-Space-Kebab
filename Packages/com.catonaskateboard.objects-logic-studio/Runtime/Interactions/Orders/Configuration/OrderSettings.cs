using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses uniform or weighted sampling within each order's draw limits.</summary>
    public enum OrderSelection { UniformRandom, WeightedRandom }

    /// <summary>Defines the available order catalog and its per-spawn unit budget.</summary>
    [Serializable]
    public sealed class OrderSettings
    {
        #region Fields

        [Header("Orders")]
        [Tooltip("ID of the scene Order Board receiving this object's entries. Multiple customers use the same ID.")]
        public string Board = "Orders";
        [Tooltip("Maximum individual orders drawn at spawn, additionally limited by the destination board's authored slot count.")]
        public int DrawCount = 1;
        [Tooltip("Sample the unit budget once per spawn within Draw Unit Range. Required draws are reserved first and the board slot count remains the hard cap.")]
        public bool RandomDrawUnits;
        [Tooltip("Inclusive unit budget range. The lower bound may be zero. A feasible budget is sampled after reserving required draws.")]
        public Vector2Int DrawUnitRange = new Vector2Int(1, 3);
        [Tooltip("Fallback candidate mode when the catalog has no custom pools. Selection fills the largest reachable unit budget; exhausted allowances leave remaining slots empty.")]
        public OrderSelection Selection;
        [Tooltip("Available named orders. Each selected candidate contributes Quantity separate tickets and remains active until despawn.")]
        public OrderCatalog Catalog;
        [Header("Spawn Catalogs")]
        [Tooltip("Override the default catalog by this prefab's one-based spawn count. Counts survive scene changes within one play session.")]
        public bool UseSpawnCatalogs;
        [Tooltip("Exact spawn overrides win over the most recent From threshold. Duplicate rules for the same mode and spawn are invalid.")]
        public OrderSpawnCatalog[] SpawnCatalogs = Array.Empty<OrderSpawnCatalog>();
        [Header("Board Presentation")]
        [Tooltip("Draw orders at spawn, but display them only after the selected local interaction completes successfully.")]
        public bool WaitForCompletion;
        [Tooltip("Existing interaction on this same object whose completion reveals the drawn orders until despawn.")]
        public ObjectInteraction CompletionSource;

        [Header("Order Timer")]
        [Tooltip("Show the HUD stopwatch when this customer's first order actually appears on its board. Day Flow completion stops it.")]
        public bool UseTimer = true;
        [Tooltip("Caption shown before elapsed minutes and seconds.")]
        public string TimerLabel = "Order";
        [Tooltip("Show tenths of a second in addition to minutes and seconds.")]
        public bool TimerTenths;
        [Tooltip("Keep the stopped result visible until the customer leaves or a new customer's timer starts.")]
        public bool KeepTimerResult;

        /// <summary>Shared candidates read at the beginning of each spawn.</summary>
        public OrderEntry[] Entries => Catalog != null ? Catalog.Entries : Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the catalog and the single local contact interaction used to fulfil its tickets.</summary>
        /// <param name="owner">Object owning this order interaction.</param>
        /// <param name="warning">Receives an invalid catalog or missing consuming interaction.</param>
        /// <returns>True when the object can select orders and receive consumed items.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Destination-specific validation runs again when applying or spawning an object.
            warning = "Available Orders needs exactly one local, enabled Modify By Contact with permanent Consume Other.";
            return owner != null && owner.GetComponents<ObjectContactModifier>().Length == 1
                && Eligible(owner.GetComponent<ObjectContactModifier>(), owner) && TryValidate(out warning)
                && ValidateCompletion(owner, out warning);
        }

        /// <summary>Rejects missing, external or circular presentation dependencies.</summary>
        /// <param name="owner">Object whose orders are being configured.</param>
        /// <param name="warning">Receives an invalid completion source.</param>
        /// <returns>True when presentation is immediate or has a usable local source.</returns>
        public bool ValidateCompletion(GameObject owner, out string warning)
        {
            warning = string.Empty;
            if (WaitForCompletion && (CompletionSource == null || CompletionSource.gameObject != owner
                || !CompletionSource.enabled || CompletionSource is ObjectAvailableOrders or ObjectInteractionUnlock))
                warning = "Choose an enabled interaction on this object to reveal orders after completion; Available Orders and availability rules cannot be their own prerequisite.";
            return warning.Length == 0;
        }

        /// <summary>Checks a reusable configuration without requiring a destination object.</summary>
        /// <param name="warning">Receives the first invalid catalog or extraction setting.</param>
        /// <returns>True when the configuration contains at least one selectable candidate.</returns>
        public bool TryValidate(out string warning)
        {
            // The board's actual slot count is evaluated at spawn, never written back into this asset.
            warning = "Choose an order catalog, a board ID and a positive draw count.";
            if (Catalog == null || string.IsNullOrWhiteSpace(Board)
                || (RandomDrawUnits ? DrawUnitRange.x < 0 || DrawUnitRange.y < DrawUnitRange.x || DrawUnitRange.y == int.MaxValue : DrawCount <= 0 || DrawCount == int.MaxValue))
                return false;
            // Only catalogs without their own pools depend on this shared selection mode.
            bool usesFallbackSelection = false;
            foreach (OrderCatalog catalog in Catalogs())
                usesFallbackSelection |= catalog.Pools == null || catalog.Pools.Length == 0;
            warning = "Choose a supported order selection mode for catalogs without custom pools.";
            if (usesFallbackSelection && Selection is not (OrderSelection.UniformRandom or OrderSelection.WeightedRandom))
                return false;
            // Validate every reachable catalog without rewriting a budget or dropping an authored rule.
            HashSet<(OrderSpawnMatch, int)> rules = new HashSet<(OrderSpawnMatch, int)>();
            if (UseSpawnCatalogs)
            {
                warning = "Add valid spawn catalog rules with positive spawn numbers and no duplicate mode/number pairs.";
                if (SpawnCatalogs == null || SpawnCatalogs.Length == 0)
                    return false;
                foreach (OrderSpawnCatalog rule in SpawnCatalogs)
                    if (rule == null || rule.Spawn <= 0 || rule.Catalog == null
                        || rule.Match is not (OrderSpawnMatch.Exact or OrderSpawnMatch.From) || !rules.Add((rule.Match, rule.Spawn)))
                        return false;
            }
            foreach (OrderCatalog catalog in Catalogs())
            {
                if (!catalog.TryValidate(out warning))
                    return false;
                long required = 0;
                List<OrderDrawPool> pools = catalog.CreateDrawPools(Selection);
                foreach (OrderDrawGroup group in OrderDrawGroup.Create(catalog, pools, false))
                {
                    required += (long)group.Required * group.Quantity;
                    warning = "Required order group has no selectable candidate: " + group.Name;
                    if (group.Required > 0 && group.Candidates.Count == 0)
                        return false;
                }
                warning = "Required draws in catalog '" + catalog.name + "' can exceed the maximum Draw Units. Increase the budget or reduce required draws.";
                if (required > (RandomDrawUnits ? DrawUnitRange.y : DrawCount))
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Resolves a spawn rule independently of its position in the array.</summary>
        /// <param name="spawn">One-based occurrence of this prefab.</param>
        /// <returns>The exact catalog, latest threshold catalog, or default catalog.</returns>
        public OrderCatalog ResolveCatalog(int spawn)
        {
            OrderCatalog selected = Catalog;
            int threshold = 0;
            if (UseSpawnCatalogs && SpawnCatalogs != null)
                foreach (OrderSpawnCatalog rule in SpawnCatalogs)
                    if (rule != null && rule.Spawn <= spawn)
                    {
                        if (rule.Match == OrderSpawnMatch.Exact && rule.Spawn == spawn)
                            return rule.Catalog;
                        if (rule.Match == OrderSpawnMatch.From && rule.Spawn > threshold)
                        {
                            selected = rule.Catalog;
                            threshold = rule.Spawn;
                        }
                    }
            return selected;
        }

        /// <summary>Enumerates catalog sources for validation and dialogue authoring.</summary>
        /// <returns>Default and enabled override catalogs, ignoring empty references.</returns>
        internal IEnumerable<OrderCatalog> Catalogs()
        {
            if (Catalog != null)
                yield return Catalog;
            if (UseSpawnCatalogs && SpawnCatalogs != null)
                foreach (OrderSpawnCatalog rule in SpawnCatalogs)
                    if (rule?.Catalog != null)
                        yield return rule.Catalog;
        }

        /// <summary>Offers dialogue selectors all possible order names across spawn-specific catalogs.</summary>
        /// <returns>Distinct enabled order names in authored order.</returns>
        public IEnumerable<string> OrderNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (OrderCatalog catalog in Catalogs())
                foreach (string name in catalog.OrderNames())
                    if (names.Add(name))
                        yield return name;
        }

        /// <summary>Accepts a dialogue key from any catalog reachable by this interaction.</summary>
        /// <param name="name">Exact order or recipe-variant key.</param>
        /// <returns>True when at least one configured catalog supplies this key.</returns>
        public bool ContainsOrder(string name)
        {
            foreach (OrderCatalog catalog in Catalogs())
                if (catalog.ContainsOrder(name))
                    return true;
            return false;
        }

        /// <summary>Identifies contact actions capable of committing the consumption represented by an order.</summary>
        /// <param name="source">Candidate interaction.</param>
        /// <param name="owner">Exact object containing the order interaction.</param>
        /// <returns>True for a local, enabled, permanent consuming action.</returns>
        public static bool Eligible(ObjectContactModifier source, GameObject owner)
        {
            // Temporary contact effects cannot consume participants and therefore cannot fulfil orders.
            return source != null && source.gameObject == owner && source.enabled && source.Settings != null
                && source.Settings.ConsumesOther;
        }

        #endregion
        #endregion
    }
}
