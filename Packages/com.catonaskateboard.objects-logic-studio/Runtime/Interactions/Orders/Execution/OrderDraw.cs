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
            OrderSettings settings = owner.Settings;
            List<OrderDrawGroup> groups = OrderDrawGroup.Create(owner.ActiveCatalog, settings.Selection, true);
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
            List<OrderTicket> selected = new List<OrderTicket>(budget);
            foreach (OrderDrawGroup group in groups)
            {
                if (group.Required == 0)
                    continue;
                if (group.Candidates.Count == 0)
                {
                    Debug.LogWarning("Available Orders: required order has no eligible candidate: " + group.Name, owner);
                    return Array.Empty<OrderTicket>();
                }
                for (int draw = 0; draw < group.Required; draw++)
                    Add(selected, Pick(group.Candidates, settings.Selection), owner);
            }
            // Reachability excludes choices that strand slots despite a complete alternative combination.
            int target = budget - selected.Count;
            bool[] reachable = Reachable(groups, target);
            while (target > 0 && !reachable[target])
                target--;
            while (target > 0)
            {
                List<OrderCandidate> eligible = new List<OrderCandidate>();
                foreach (OrderDrawGroup group in groups)
                {
                    if (group.Remaining == 0 || group.Quantity > target || group.Candidates.Count == 0)
                        continue;
                    group.Remaining--;
                    bool fits = Reachable(groups, target - group.Quantity)[target - group.Quantity];
                    group.Remaining++;
                    if (fits)
                        eligible.AddRange(group.Candidates);
                }
                OrderCandidate chosen = Pick(eligible, settings.Selection);
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
        /// <returns>Membership by achievable unit total, including zero.</returns>
        private static bool[] Reachable(List<OrderDrawGroup> groups, int budget)
        {
            bool[] reachable = new bool[budget + 1];
            reachable[0] = true;
            foreach (OrderDrawGroup group in groups)
            {
                if (group.Candidates.Count == 0)
                    continue;
                int quantity = group.Quantity;
                int draws = Mathf.Min(group.Remaining, budget / quantity);
                for (int draw = 0; draw < draws; draw++)
                    for (int total = budget; total >= quantity; total--)
                        reachable[total] |= reachable[total - quantity];
            }
            return reachable;
        }

        /// <summary>Samples uniformly or by weight only among feasible candidates.</summary>
        /// <param name="candidates">Nonempty set preserving the remaining budget.</param>
        /// <param name="selection">Uniform or weighted policy.</param>
        /// <returns>The candidate to expand into individual board tickets.</returns>
        private static OrderCandidate Pick(List<OrderCandidate> candidates, OrderSelection selection)
        {
            double total = 0d;
            foreach (OrderCandidate candidate in candidates)
                total += selection == OrderSelection.WeightedRandom ? candidate.Weight : 1d;
            double choice = Random.value * total;
            foreach (OrderCandidate candidate in candidates)
            {
                choice -= selection == OrderSelection.WeightedRandom ? candidate.Weight : 1d;
                if (choice < 0d)
                    return candidate;
            }
            return candidates[candidates.Count - 1];
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
