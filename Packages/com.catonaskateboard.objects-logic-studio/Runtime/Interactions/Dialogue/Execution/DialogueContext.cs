using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Freezes eligibility at a request boundary so later receipts cannot redirect an active conversation.</summary>
    public sealed class DialogueContext
    {
        #region State
        private readonly Dictionary<DialogueCondition, bool> conditions = new Dictionary<DialogueCondition, bool>();
        private readonly Dictionary<DialogueEntry, bool> entries = new Dictionary<DialogueEntry, bool>();
        #endregion
        #region Methods
        #region Capture
        /// <summary>Captures all referenced predicates without retaining mutable consumption progress.</summary>
        /// <param name="tree">Validated root and its subtree dependencies.</param>
        /// <param name="item">Object owning this conversation.</param>
        /// <param name="orders">Optional live order progress.</param>
        /// <param name="spawn">One-based lifetime shared with orders.</param>
        /// <param name="consumption">Whether entry consumption filters apply.</param>
        /// <returns>A detached eligibility snapshot for this request.</returns>
        public static DialogueContext Capture(DialogueTree tree, ObjectItem item, ObjectAvailableOrders orders, int spawn, bool consumption)
        {
            DialogueContext context = new DialogueContext();
            context.Read(tree, item, orders, spawn, consumption, new HashSet<ObjectFlag>(item.Identity.ActiveFlags), false, new HashSet<DialogueTree>());
            return context;
        }

        /// <summary>Creates a preview context without consuming objects or changing scene state.</summary>
        /// <param name="tree">Validated graph to simulate.</param>
        /// <param name="spawn">Simulated occurrence.</param>
        /// <param name="flags">Simulated owner flags.</param>
        /// <param name="receiptsMatch">Result supplied for enabled consumption predicates.</param>
        /// <returns>An isolated preview snapshot.</returns>
        public static DialogueContext Simulate(DialogueTree tree, int spawn, IEnumerable<ObjectFlag> flags, bool receiptsMatch)
        {
            DialogueContext context = new DialogueContext();
            context.Read(tree, null, null, spawn, true, new HashSet<ObjectFlag>(flags), receiptsMatch, new HashSet<DialogueTree>());
            return context;
        }

        /// <summary>Visits each dependency once and stores the enabled entry and branch predicates.</summary>
        /// <param name="tree">Current dependency.</param>
        /// <param name="item">Live owner, or null for simulation.</param>
        /// <param name="orders">Live order progress.</param>
        /// <param name="spawn">Captured occurrence.</param>
        /// <param name="consumption">Whether entry receipt filters apply.</param>
        /// <param name="flags">Captured owner identities.</param>
        /// <param name="simulated">Simulated receipt predicate result.</param>
        /// <param name="visited">Dependencies already captured.</param>
        private void Read(DialogueTree tree, ObjectItem item, ObjectAvailableOrders orders, int spawn, bool consumption,
            HashSet<ObjectFlag> flags, bool simulated, HashSet<DialogueTree> visited)
        {
            if (!visited.Add(tree))
                return;
            foreach (DialogueNode node in tree.Nodes)
                switch (node.Kind)
                {
                    case DialogueNodeKind.Entry:
                        entries[node.Entry] = !consumption || !node.Entry.Consumption.Enabled
                            || (item != null ? node.Entry.Consumption.Matches(item, orders) : simulated);
                        break;
                    case DialogueNodeKind.Condition:
                        conditions[node.Condition] = Evaluate(node.Condition, item, orders, spawn, flags, simulated);
                        break;
                    case DialogueNodeKind.Select:
                        foreach (DialogueBranch branch in node.Branches)
                            conditions[branch.Condition] = Evaluate(branch.Condition, item, orders, spawn, flags, simulated);
                        break;
                    case DialogueNodeKind.Subtree:
                        Read(node.Subtree, item, orders, spawn, consumption, flags, simulated, visited);
                        break;
                }
        }

        /// <summary>Combines enabled predicates once, preserving explicit Any, All and inversion.</summary>
        /// <param name="condition">Predicate to evaluate.</param>
        /// <param name="item">Live owner, or null for simulation.</param>
        /// <param name="orders">Live order progress.</param>
        /// <param name="spawn">Captured occurrence.</param>
        /// <param name="flags">Captured identities.</param>
        /// <param name="simulated">Simulated consumption result.</param>
        /// <returns>The combined predicate value.</returns>
        private static bool Evaluate(DialogueCondition condition, ObjectItem item, ObjectAvailableOrders orders, int spawn,
            HashSet<ObjectFlag> flags, bool simulated)
        {
            bool result = condition.RequireAll;
            if (condition.CheckFlags)
            {
                bool match = condition.AllFlags;
                foreach (ObjectFlag flag in condition.Flags)
                    match = condition.AllFlags ? match && flags.Contains(flag) : match || flags.Contains(flag);
                result = condition.RequireAll ? result && match : result || match;
            }
            if (condition.CheckSpawn)
            {
                bool match = spawn >= condition.FirstSpawn && (condition.LastSpawn == 0 || spawn <= condition.LastSpawn);
                result = condition.RequireAll ? result && match : result || match;
            }
            if (condition.Consumption.Enabled)
            {
                bool match = item != null ? item.HasConsumption && condition.Consumption.Matches(item, orders) : simulated;
                result = condition.RequireAll ? result && match : result || match;
            }
            if (!condition.CheckFlags && !condition.CheckSpawn && !condition.Consumption.Enabled)
                result = true;
            return result != condition.Invert;
        }
        #endregion
        #region Queries
        /// <summary>Reads a condition captured for this tree, without querying gameplay again.</summary>
        /// <param name="condition">Authored predicate.</param>
        /// <returns>The frozen result.</returns>
        internal bool Matches(DialogueCondition condition) => conditions[condition];

        /// <summary>Reads a shared entry's captured consumption eligibility.</summary>
        /// <param name="entry">Shared page sequence.</param>
        /// <returns>True when this entry may be shown for the captured request.</returns>
        internal bool Matches(DialogueEntry entry) => entries[entry];
        #endregion
        #endregion
    }
}
