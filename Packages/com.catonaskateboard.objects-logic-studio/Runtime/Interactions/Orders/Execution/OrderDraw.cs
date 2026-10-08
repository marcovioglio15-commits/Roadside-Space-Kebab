using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Draws complete order quantities once per spawn without exceeding the board's capacity.</summary>
    internal static class OrderDraw
    {
        #region Methods
        #region Selection

        /// <summary>Samples candidates without replacement, expanding each quantity into separate tickets.</summary>
        /// <param name="owner">Customer owning the resulting tickets.</param>
        /// <param name="capacity">Authored destination slot count, independent of current occupancy.</param>
        /// <returns>The fixed ticket set retained until the customer despawns.</returns>
        internal static OrderTicket[] Select(ObjectAvailableOrders owner, int capacity)
        {
            // Remaining capacity counts units, not candidate rows.
            OrderSettings settings = owner.Settings;
            int remaining = Mathf.Min(settings.DrawCount, capacity);
            List<OrderCandidate> candidates = new List<OrderCandidate>(settings.Catalog.Candidates());
            List<OrderTicket> selected = new List<OrderTicket>();
            while (remaining > 0 && candidates.Count > 0)
            {
                double total = 0d;
                for (int index = candidates.Count - 1; index >= 0; index--)
                    if (candidates[index].Quantity > remaining
                        || settings.Selection == OrderSelection.WeightedRandom && candidates[index].Weight <= 0f)
                        candidates.RemoveAt(index);
                    else
                        total += settings.Selection == OrderSelection.WeightedRandom ? candidates[index].Weight : 1d;
                if (candidates.Count == 0)
                    break;
                double choice = Random.value * total;
                int selectedIndex = candidates.Count - 1;
                for (int index = 0; index < candidates.Count; index++)
                {
                    choice -= settings.Selection == OrderSelection.WeightedRandom ? candidates[index].Weight : 1d;
                    if (choice >= 0d)
                        continue;
                    selectedIndex = index;
                    break;
                }
                OrderCandidate entry = candidates[selectedIndex];
                for (int unit = 0; unit < entry.Quantity; unit++)
                    selected.Add(new OrderTicket(owner, entry, settings.Board));
                remaining -= entry.Quantity;
                candidates.RemoveAt(selectedIndex);
            }
            return selected.ToArray();
        }

        #endregion
        #endregion
    }
}
