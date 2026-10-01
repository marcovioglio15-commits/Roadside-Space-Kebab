using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks remaining slot assignments when recipe rows and magnets accept alternative flags.</summary>
    internal sealed class AssemblyCapacity
    {
        #region State

        private int[,] capacity;
        private int[] demand;
        private int[] parents;
        private int[] queue;

        #endregion

        #region Methods

        #region Membership

        /// <summary>Counts assigned recipe units across one group of alternative flags.</summary>
        /// <param name="counts">Units already assigned to exactly one flag per inserted object.</param>
        /// <param name="flags">Distinct flags belonging to the group.</param>
        /// <returns>Total assigned units without counting an ingredient twice.</returns>
        internal static long Count(IReadOnlyDictionary<ObjectFlag, int> counts, ObjectFlag[] flags)
        {
            // Each part records only the flag selected at insertion.
            long total = 0;
            if (counts != null)
                foreach (ObjectFlag flag in flags)
                    if (counts.TryGetValue(flag, out int count))
                        total += count;
            return total;
        }

        /// <summary>Checks whether one slot can receive any alternative from a recipe row.</summary>
        /// <param name="magnet">Generic or restricted physical slot.</param>
        /// <param name="flags">Recipe alternatives.</param>
        /// <returns>True when at least one alternative can occupy this slot.</returns>
        internal static bool Matches(AssemblyMagnet magnet, ObjectFlag[] flags)
        {
            // Generic slots still draw their eligible ingredients from the recipe.
            if (magnet.AnyIngredient)
                return true;
            foreach (ObjectFlag flag in flags)
                if (Array.IndexOf(magnet.Flags, flag) >= 0)
                    return true;
            return false;
        }

        #endregion

        #region Capacity

        /// <summary>Proves that remaining mandatory rows and numbered magnets can share the available slots.</summary>
        /// <param name="settings">Validated recipe and layout.</param>
        /// <param name="counts">Existing progress, or null for an empty product.</param>
        /// <param name="occupied">Existing physical occupancy, or null before activation.</param>
        /// <param name="selected">Proposed insertion slot, or -1 for validation.</param>
        /// <param name="flag">Single alternative assigned by the proposed insertion.</param>
        /// <param name="units">Units supplied by the proposed insertion.</param>
        /// <returns>True when future ingredients have a feasible assignment.</returns>
        internal bool CanFit(AssemblyProductSettings settings, IReadOnlyDictionary<ObjectFlag, int> counts,
            bool[] occupied = null, int selected = -1, ObjectFlag flag = null, int units = 0)
        {
            // Lower bounds require one physical slot per unfinished mandatory row and per numbered magnet.
            int rows = settings.Ingredients.Length;
            int slots = settings.Magnets.Length;
            int sink = rows + slots + 1;
            int source = sink + 1;
            int destination = source + 1;
            Prepare(destination + 1);
            int inserted = selected >= 0 ? 1 : 0;
            int lastOrder = 0;
            long remainingUnits = 0;
            for (int row = 0; row < rows; row++)
            {
                AssemblyIngredient ingredient = settings.Ingredients[row];
                long remaining = ingredient.Count - Count(counts, ingredient.Flags)
                    - (flag != null && Array.IndexOf(ingredient.Flags, flag) >= 0 ? units : 0);
                if (remaining < 0)
                    return false;
                remainingUnits += remaining;
                Bound(capacity, demand, 0, row + 1, !ingredient.Optional && remaining > 0 ? 1 : 0, (int)Math.Min(remaining, slots));
                for (int slot = 0; slot < slots; slot++)
                    if (slot != selected && (occupied == null || !occupied[slot]) && Matches(settings.Magnets[slot], ingredient.Flags))
                        capacity[row + 1, rows + slot + 1] = 1;
            }
            // Missing early numbered slots remain required after detachment; later slots cannot consume their capacity.
            for (int slot = 0; slot < slots; slot++)
            {
                AssemblyMagnet magnet = settings.Magnets[slot];
                lastOrder = Math.Max(lastOrder, magnet.Order);
                if (occupied != null && occupied[slot])
                    inserted++;
                if (slot != selected && (occupied == null || !occupied[slot]))
                    Bound(capacity, demand, rows + slot + 1, sink, magnet.Order > 0 ? 1 : 0, 1);
            }
            if (remainingUnits < lastOrder - inserted)
                return false;
            capacity[sink, 0] = slots;
            int needed = 0;
            for (int node = 0; node <= sink; node++)
                if (demand[node] > 0)
                {
                    capacity[source, node] = demand[node];
                    needed += demand[node];
                }
                else if (demand[node] < 0)
                    capacity[node, destination] = -demand[node];
            return Flow(capacity, source, destination) == needed;
        }

        /// <summary>Converts one required assignment into residual capacity and node demand.</summary>
        /// <param name="capacity">Residual network.</param>
        /// <param name="demand">Net lower-bound demand per node.</param>
        /// <param name="from">Origin node.</param>
        /// <param name="to">Destination node.</param>
        /// <param name="minimum">Required flow.</param>
        /// <param name="maximum">Available flow.</param>
        private static void Bound(int[,] capacity, int[] demand, int from, int to, int minimum, int maximum)
        {
            // Lower bounds are removed before the ordinary augmenting-path solve.
            capacity[from, to] = maximum - minimum;
            demand[from] -= minimum;
            demand[to] += minimum;
        }

        /// <summary>Finds a bounded flow using reusable breadth-first search buffers for this query.</summary>
        /// <param name="capacity">Residual capacities modified by the solve.</param>
        /// <param name="source">Demand source.</param>
        /// <param name="sink">Demand destination.</param>
        /// <returns>Units of demand satisfied by compatible slot assignments.</returns>
        private int Flow(int[,] capacity, int source, int sink)
        {
            // Polynomial path search avoids exponential combinations when many magnets accept the same flags.
            int count = capacity.GetLength(0);
            int total = 0;
            while (true)
            {
                Array.Fill(parents, -1);
                parents[source] = source;
                queue[0] = source;
                int tail = 1;
                for (int head = 0; head < tail && parents[sink] < 0; head++)
                    for (int next = 0; next < count; next++)
                        if (parents[next] < 0 && capacity[queue[head], next] > 0)
                        {
                            parents[next] = queue[head];
                            queue[tail++] = next;
                        }
                if (parents[sink] < 0)
                    return total;
                int amount = int.MaxValue;
                for (int node = sink; node != source; node = parents[node])
                    amount = Math.Min(amount, capacity[parents[node], node]);
                for (int node = sink; node != source; node = parents[node])
                {
                    capacity[parents[node], node] -= amount;
                    capacity[node, parents[node]] += amount;
                }
                total += amount;
            }
        }

        #endregion

        #region Buffers

        /// <summary>Reuses solver storage across contact queries until the recipe layout changes size.</summary>
        /// <param name="size">Number of flow nodes needed by this recipe.</param>
        private void Prepare(int size)
        {
            // Runtime recipes are fixed; ordinary candidate checks only clear existing buffers.
            if (demand == null || demand.Length != size)
            {
                capacity = new int[size, size];
                demand = new int[size];
                parents = new int[size];
                queue = new int[size];
            }
            else
            {
                Array.Clear(capacity, 0, capacity.Length);
                Array.Clear(demand, 0, demand.Length);
            }
        }

        #endregion

        #endregion
    }
}
