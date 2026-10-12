using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Validates graph structure and dependencies before a runner or preview is created.</summary>
    internal static class DialogueTreeValidation
    {
        #region Methods
        #region Validation
        /// <summary>Checks the root and all referenced trees using the same destination context.</summary>
        /// <param name="tree">Root tree to validate.</param>
        /// <param name="orders">Optional destination order catalogs.</param>
        /// <param name="bound">Whether the destination is known.</param>
        /// <param name="consumption">Whether entry receipt filters are active.</param>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when every referenced tree can execute within bounded depth.</returns>
        internal static bool Validate(DialogueTree tree, OrderSettings orders, bool bound, bool consumption, out string warning)
        {
            return Visit(tree, orders, bound, consumption, new HashSet<DialogueTree>(), new HashSet<DialogueTree>(), out warning);
        }

        /// <summary>Rejects subtree recursion before checking local links and immediate control-flow cycles.</summary>
        /// <param name="tree">Current shared tree.</param>
        /// <param name="orders">Destination catalogs.</param>
        /// <param name="bound">Whether local references must resolve.</param>
        /// <param name="consumption">Whether entry filters are used.</param>
        /// <param name="visiting">Active dependency chain.</param>
        /// <param name="complete">Already validated dependencies.</param>
        /// <param name="warning">Receives a structural or configuration error.</param>
        /// <returns>True when this dependency is valid.</returns>
        private static bool Visit(DialogueTree tree, OrderSettings orders, bool bound, bool consumption,
            HashSet<DialogueTree> visiting, HashSet<DialogueTree> complete, out string warning)
        {
            warning = "Assign a dialogue tree with nodes and an entry point.";
            if (tree == null || tree.Nodes == null || tree.Nodes.Length == 0 || tree.Nodes.Length > 4096)
                return false;
            if (complete.Contains(tree))
                return true;
            warning = "Subtree calls cannot recurse or exceed 32 nested trees: " + tree.name;
            if (visiting.Count >= 32 || !visiting.Add(tree))
                return false;
            Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
            foreach (DialogueNode node in tree.Nodes)
            {
                warning = "Every node needs a unique internal key and a title: " + tree.name;
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Name) || !nodes.TryAdd(node.Id, node))
                    return false;
            }
            warning = "Select an existing Start node: " + tree.name;
            if (string.IsNullOrEmpty(tree.Start) || !nodes.ContainsKey(tree.Start))
                return false;
            foreach (DialogueNode node in tree.Nodes)
            {
                if (!Node(node, orders, bound, consumption, out warning))
                {
                    warning = tree.name + " / " + node.Name + ": " + warning;
                    return false;
                }
                foreach (string target in Links(node))
                    if (string.IsNullOrEmpty(target) || !nodes.ContainsKey(target))
                    {
                        warning = tree.name + " / " + node.Name + ": connect every required output to an existing node.";
                        return false;
                    }
                if (node.Kind == DialogueNodeKind.Subtree && !Visit(node.Subtree, orders, bound, consumption, visiting, complete, out warning))
                    return false;
            }
            HashSet<string> done = new HashSet<string>();
            HashSet<string> path = new HashSet<string>();
            foreach (DialogueNode node in tree.Nodes)
                if (ImmediateLoop(node.Id, nodes, path, done))
                {
                    warning = tree.name + ": a control-flow loop needs an Entry or a positive Wait.";
                    return false;
                }
            visiting.Remove(tree);
            complete.Add(tree);
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks only fields used by the selected operation.</summary>
        /// <param name="node">Operation being checked.</param>
        /// <param name="orders">Destination catalogs.</param>
        /// <param name="bound">Whether catalog references must resolve.</param>
        /// <param name="consumption">Whether entry filters are active.</param>
        /// <param name="warning">Receives invalid operation data.</param>
        /// <returns>True for a complete operation.</returns>
        private static bool Node(DialogueNode node, OrderSettings orders, bool bound, bool consumption, out string warning)
        {
            warning = "Configure this node's active fields.";
            switch (node.Kind)
            {
                case DialogueNodeKind.Entry:
                    return node.Entry != null && node.Entry.TryValidate(out warning, consumption, true, orders, bound);
                case DialogueNodeKind.Condition:
                    return node.Condition != null && node.Condition.TryValidate(orders, bound, out warning);
                case DialogueNodeKind.Select:
                    if (node.Selection is not (DialogueSelection.Sequence or DialogueSelection.Random or DialogueSelection.WeightedRandom)
                        || node.Branches == null || node.Branches.Length == 0)
                        return false;
                    foreach (DialogueBranch branch in node.Branches)
                    {
                        warning = "Each branch needs a condition and a finite, non-negative weight.";
                        if (branch == null || !float.IsFinite(branch.Weight) || branch.Weight < 0f
                            || branch.Condition == null || !branch.Condition.TryValidate(orders, bound, out warning))
                            return false;
                    }
                    return true;
                case DialogueNodeKind.Wait:
                    warning = "Wait duration must be positive and finite.";
                    return InteractionValues.Positive(node.Duration);
                case DialogueNodeKind.Subtree:
                    warning = "Assign a subtree.";
                    return node.Subtree != null;
                case DialogueNodeKind.End:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Enumerates required flow links and any enabled fallback without exposing unused fields.</summary>
        /// <param name="node">Node whose active outputs are requested.</param>
        /// <returns>Destination keys, including empty required destinations for validation.</returns>
        internal static IEnumerable<string> Links(DialogueNode node)
        {
            switch (node.Kind)
            {
                case DialogueNodeKind.Entry:
                case DialogueNodeKind.Wait:
                case DialogueNodeKind.Subtree:
                    yield return node.Next;
                    break;
                case DialogueNodeKind.Condition:
                    yield return node.Next;
                    yield return node.Otherwise;
                    break;
                case DialogueNodeKind.Select:
                    foreach (DialogueBranch branch in node.Branches)
                        yield return branch.Target;
                    if (!string.IsNullOrEmpty(node.Otherwise))
                        yield return node.Otherwise;
                    break;
            }
        }

        /// <summary>Finds cycles without an input or timer boundary using a three-state traversal.</summary>
        /// <param name="id">Current node key.</param>
        /// <param name="nodes">Resolved local nodes.</param>
        /// <param name="path">Current immediate path.</param>
        /// <param name="done">Nodes already proved acyclic.</param>
        /// <returns>True for an immediate cycle.</returns>
        private static bool ImmediateLoop(string id, Dictionary<string, DialogueNode> nodes, HashSet<string> path, HashSet<string> done)
        {
            DialogueNode node = nodes[id];
            if (done.Contains(id) || node.Kind is DialogueNodeKind.Entry or DialogueNodeKind.Wait or DialogueNodeKind.End)
                return false;
            if (!path.Add(id))
                return true;
            foreach (string target in Links(node))
                if (ImmediateLoop(target, nodes, path, done))
                    return true;
            path.Remove(id);
            done.Add(id);
            return false;
        }
        #endregion
        #endregion
    }
}
