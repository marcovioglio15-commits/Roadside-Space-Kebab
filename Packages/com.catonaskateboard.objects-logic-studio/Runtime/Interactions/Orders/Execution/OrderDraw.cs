using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reserves required draws, then samples repeatable candidates without wasting fillable board slots.</summary>
    internal static class OrderDraw
    {
        #region Methods
        #region Selection

        /// <summary>Draws full quantities within parent or independent variant limits and the board's capacity.</summary>
        /// <param name="owner">Customer retaining tickets until despawn.</param>
        /// <param name="capacity">Authored board slots, independent of current occupancy.</param>
        /// <returns>The fixed ticket set, or an empty set when required quantities cannot fit.</returns>
        internal static OrderTicket[] Select(ObjectAvailableOrders owner, int capacity)
        {
            // Resolve the pool stage before building the candidate limits used by feasibility checks.
            OrderSettings settings = owner.Settings;
            List<OrderDrawPool> pools = owner.ActiveCatalog.CreateDrawPools(settings.Selection);
            List<OrderDrawGroup> groups = OrderDrawGroup.Create(owner.ActiveCatalog, pools, true);
            long reserved = 0;
            // Reserve whole draws, including all units of each selected order.
            foreach (OrderDrawGroup group in groups)
                reserved += (long)group.Required * group.Quantity;
            int ceiling = Mathf.Min(capacity, settings.RandomDrawUnits ? settings.DrawUnitRange.y : settings.DrawCount);
            if (reserved > ceiling)
            {
                Debug.LogWarning($"Available Orders: {reserved} required units cannot fit the {ceiling}-slot budget. No partial mandatory order was drawn.", owner);
                return Array.Empty<OrderTicket>();
            }
            int budget = settings.RandomDrawUnits
                ? Random.Range(Mathf.Max(Mathf.Min(settings.DrawUnitRange.x, ceiling), (int)reserved), ceiling + 1) : ceiling;
            foreach (OrderDrawGroup group in groups)
                if (group.Required > 0 && group.Candidates.Count == 0)
                {
                    Debug.LogWarning("Available Orders: required order has no eligible candidate: " + group.Name, owner);
                    return Array.Empty<OrderTicket>();
                }
            List<OrderTicket> selected = new List<OrderTicket>(budget);
            int target = budget;
            bool[] reachable = Reachable(groups, budget, true);
            while (target > 0 && !reachable[target])
                target--;
            foreach (OrderDrawGroup group in groups)
            {
                if (group.Required == 0)
                    continue;
                for (int draw = 0; draw < group.Required; draw++)
                {
                    // Mandatory choices must preserve the best attainable final fill, including other reservations.
                    group.Pending--;
                    List<OrderCandidate> mandatory = new List<OrderCandidate>();
                    bool[] remainder = Reachable(groups, target, true);
                    foreach (OrderCandidate candidate in group.Candidates)
                        if (candidate.Quantity <= target && remainder[target - candidate.Quantity])
                            mandatory.Add(candidate);
                    OrderCandidate chosen = Pick(mandatory, pools);
                    Add(selected, chosen, owner);
                    target -= chosen.Quantity;
                }
            }
            // Reachability excludes choices that strand slots despite a complete alternative combination.
            while (target > 0)
            {
                List<OrderCandidate> eligible = new List<OrderCandidate>();
                foreach (OrderDrawGroup group in groups)
                {
                    if (group.Remaining == 0 || group.Quantity > target || group.Candidates.Count == 0)
                        continue;
                    group.Remaining--;
                    bool[] remainder = Reachable(groups, target);
                    foreach (OrderCandidate candidate in group.Candidates)
                        if (candidate.Quantity <= target && remainder[target - candidate.Quantity])
                            eligible.Add(candidate);
                    group.Remaining++;
                }
                OrderCandidate chosen = Pick(eligible, pools);
                Add(selected, chosen, owner);
                foreach (OrderDrawGroup group in groups)
                    if (group.Candidates.Contains(chosen))
                    {
                        group.Remaining--;
                        break;
                    }
                target -= chosen.Quantity;
            }
            // A catalog may intentionally leave slots empty when its authored allowances are exhausted.
            return selected.ToArray();
        }

        /// <summary>Computes achievable unit totals with bounded repetitions and no partial quantities.</summary>
        /// <param name="groups">Shared parent pools and independently limited variants.</param>
        /// <param name="budget">Largest total relevant to this selection pass.</param>
        /// <param name="includeRequired">Whether outstanding mandatory draws must also be fulfilled.</param>
        /// <returns>Achievable unit totals; zero is included only when no mandatory draws remain.</returns>
        private static bool[] Reachable(List<OrderDrawGroup> groups, int budget, bool includeRequired = false)
        {
            bool[] reachable = new bool[budget + 1];
            reachable[0] = true;
            foreach (OrderDrawGroup group in groups)
            {
                int minimum = includeRequired ? group.Pending : 0;
                if (group.Candidates.Count == 0 && minimum == 0)
                    continue;
                bool[] exact = reachable;
                bool[] combined = new bool[budget + 1];
                if (minimum == 0)
                    Array.Copy(reachable, combined, reachable.Length);
                int draws = (int)Math.Min((long)group.Remaining + minimum, budget / group.Quantity);
                for (int draw = 1; draw <= draws; draw++)
                {
                    bool[] next = new bool[budget + 1];
                    for (int total = group.Quantity; total <= budget; total++)
                        foreach (OrderCandidate candidate in group.Candidates)
                            if (candidate.Quantity <= total && exact[total - candidate.Quantity])
                            {
                                next[total] = true;
                                if (draw >= minimum)
                                    combined[total] = true;
                                break;
                            }
                    exact = next;
                }
                reachable = combined;
            }
            return reachable;
        }

        /// <summary>Chooses an eligible pool first, then samples one feasible candidate inside that pool.</summary>
        /// <param name="candidates">Nonempty set preserving the remaining budget.</param>
        /// <param name="pools">Resolved pools for this customer spawn.</param>
        /// <returns>The candidate to expand into individual board tickets.</returns>
        private static OrderCandidate Pick(List<OrderCandidate> candidates, List<OrderDrawPool> pools)
        {
            // A pool participates only when at least one candidate preserves this selection pass.
            double totalPoolWeight = 0d;
            foreach (OrderDrawPool pool in pools)
                if (pool.Weight > 0f && HasCandidate(pool, candidates))
                    totalPoolWeight += pool.Weight;
            double poolChoice = Random.value * totalPoolWeight;
            OrderDrawPool selectedPool = null;
            foreach (OrderDrawPool pool in pools)
                if (pool.Weight > 0f && HasCandidate(pool, candidates))
                {
                    selectedPool = pool;
                    poolChoice -= pool.Weight;
                    if (poolChoice < 0d)
                        break;
                }
            if (selectedPool == null)
                return candidates[0];
            // Candidate weights are evaluated only after the first-stage pool is selected.
            double totalCandidateWeight = 0d;
            foreach (OrderCandidate candidate in candidates)
                if (selectedPool.Candidates.TryGetValue(candidate.Name, out float candidateWeight))
                    totalCandidateWeight += selectedPool.Selection == OrderSelection.WeightedRandom ? candidateWeight : 1d;
            double candidateChoice = Random.value * totalCandidateWeight;
            OrderCandidate fallback = candidates[0];
            foreach (OrderCandidate candidate in candidates)
                if (selectedPool.Candidates.TryGetValue(candidate.Name, out float selectedWeight))
                {
                    fallback = candidate;
                    candidateChoice -= selectedPool.Selection == OrderSelection.WeightedRandom ? selectedWeight : 1d;
                    if (candidateChoice < 0d)
                        return candidate;
                }
            return fallback;
        }

        /// <summary>Checks whether a pool contains a candidate allowed by the current budget pass.</summary>
        /// <param name="pool">Resolved pool under consideration.</param>
        /// <param name="candidates">Candidates that preserve required draws and remaining capacity.</param>
        /// <returns>True when the pool can be chosen without stranding the board budget.</returns>
        private static bool HasCandidate(OrderDrawPool pool, List<OrderCandidate> candidates)
        {
            // Weighted pools exclude zero-mass members while uniform pools ignore their weights.
            foreach (OrderCandidate candidate in candidates)
                if (pool.Candidates.TryGetValue(candidate.Name, out float memberWeight)
                    && (pool.Selection != OrderSelection.WeightedRandom || memberWeight > 0f))
                    return true;
            return false;
        }

        /// <summary>Expands one draw into separate board slots without merging equal orders.</summary>
        /// <param name="tickets">Current spawn's mutable ticket buffer.</param>
        /// <param name="candidate">Whole order quantity selected for this draw.</param>
        /// <param name="owner">Customer owning each new ticket.</param>
        private static void Add(List<OrderTicket> tickets, OrderCandidate candidate, ObjectAvailableOrders owner)
        {
            for (int unit = 0; unit < candidate.Quantity; unit++)
                tickets.Add(new OrderTicket(owner, candidate, owner.Settings.Board));
        }

        #endregion
        #endregion
    }
}
