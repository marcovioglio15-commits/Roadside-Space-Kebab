using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks block connections and entry conditions without changing incomplete proposals.</summary>
    internal static class DialogueTreeValidation
    {
        #region Methods

        #region Validation

        /// <summary>Validates a complete sheet and rejects automatic loops.</summary>
        /// <param name="tree">Proposed tree.</param>
        /// <param name="orders">Optional destination order catalog.</param>
        /// <param name="bound">Whether destination references are known.</param>
        /// <param name="warning">Receives the first configuration problem.</param>
        /// <returns>True when the tree can execute.</returns>
        internal static bool Validate(DialogueTree tree, OrderSettings orders, bool bound, out string warning)
        {
            warning = "Assign a tree with a Start block and at least one End block.";
            if (tree == null || tree.Nodes == null || tree.Nodes.Length == 0)
                return false;
            Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
            HashSet<string> rows = new HashSet<string>();
            bool hasEnd = false;
            // Resolve stable keys before checking any connection.
            foreach (DialogueNode node in tree.Nodes)
            {
                warning = "Every block needs a unique key and a title.";
                if (node == null || string.IsNullOrEmpty(node.Id) || string.IsNullOrWhiteSpace(node.Name) || !nodes.TryAdd(node.Id, node))
                    return false;
                hasEnd |= node.End;
            }
            warning = "Choose an existing Start block and include an End block.";
            if (!hasEnd || string.IsNullOrEmpty(tree.Start) || !nodes.ContainsKey(tree.Start))
                return false;
            foreach (DialogueNode node in tree.Nodes)
            {
                if (node.End)
                    continue;
                warning = node.Name + ": add at least one entry and choose a supported selection mode.";
                if (node.Entries == null || node.Entries.Length == 0
                    || node.Selection is not (DialogueSelection.FirstMatching or DialogueSelection.Sequence or DialogueSelection.Random or DialogueSelection.WeightedRandom))
                    return false;
                foreach (DialogueRow row in node.Entries)
                {
                    warning = node.Name + ": each row needs unique identity, text, trigger and a destination.";
                    if (row == null || string.IsNullOrEmpty(row.Id) || !rows.Add(row.Id) || row.Entry == null || row.Trigger == null
                        || string.IsNullOrEmpty(row.Next) || !nodes.ContainsKey(row.Next))
                        return false;
                    if (!row.Entry.TryValidate(out warning) || !row.Trigger.TryValidate(orders, bound, out warning))
                    {
                        warning = node.Name + " / " + row.Name + ": " + warning;
                        return false;
                    }
                    warning = node.Name + ": weights must be finite and non-negative.";
                    if (node.Selection == DialogueSelection.WeightedRandom && (!float.IsFinite(row.Weight) || row.Weight < 0f))
                        return false;
                }
            }
            HashSet<string> path = new HashSet<string>();
            HashSet<string> complete = new HashSet<string>();
            foreach (DialogueNode node in tree.Nodes)
                if (AutomaticLoop(node, nodes, path, complete))
                {
                    warning = "An On Enter loop needs a new input, proximity, arrival or consumption event.";
                    return false;
                }
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks only paths that can advance using On Enter rows.</summary>
        /// <param name="node">Block currently visited.</param>
        /// <param name="nodes">Resolved destination map.</param>
        /// <param name="path">Current automatic path.</param>
        /// <param name="complete">Already checked blocks.</param>
        /// <returns>True for an automatic cycle.</returns>
        private static bool AutomaticLoop(DialogueNode node, Dictionary<string, DialogueNode> nodes, HashSet<string> path, HashSet<string> complete)
        {
            if (node.End || complete.Contains(node.Id))
                return false;
            if (!path.Add(node.Id))
                return true;
            foreach (DialogueRow row in node.Entries)
                if (row.Trigger.Type == DialogueTrigger.OnEnter && AutomaticLoop(nodes[row.Next], nodes, path, complete))
                    return true;
            path.Remove(node.Id);
            complete.Add(node.Id);
            return false;
        }

        #endregion

        #endregion
    }
}
