using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves stable node keys once and shares dependency maps within a runner.</summary>
    internal sealed class DialogueTreePlan
    {
        #region State
        internal readonly DialogueTree Tree;
        internal readonly Dictionary<string, DialogueNode> Nodes = new Dictionary<string, DialogueNode>();
        internal readonly Dictionary<DialogueTree, DialogueTreePlan> Dependencies;
        #endregion
        #region Methods
        #region Compilation
        /// <summary>Compiles a previously validated tree without reflection or runtime asset mutation.</summary>
        /// <param name="tree">Root or dependency to index.</param>
        /// <param name="dependencies">Shared map for nested tree calls.</param>
        internal DialogueTreePlan(DialogueTree tree, Dictionary<DialogueTree, DialogueTreePlan> dependencies = null)
        {
            Tree = tree;
            Dependencies = dependencies ?? new Dictionary<DialogueTree, DialogueTreePlan>();
            Dependencies.Add(tree, this);
            foreach (DialogueNode node in tree.Nodes)
                Nodes.Add(node.Id, node);
            foreach (DialogueNode node in tree.Nodes)
                if (node.Kind == DialogueNodeKind.Subtree && !Dependencies.ContainsKey(node.Subtree))
                    _ = new DialogueTreePlan(node.Subtree, Dependencies);
        }

        /// <summary>Finds whether a captured request can reach a displayable entry without sampling random branches.</summary>
        /// <param name="id">Candidate entry point.</param>
        /// <param name="context">Frozen predicates.</param>
        /// <param name="visited">Reusable traversal set, unique for one reachability query.</param>
        /// <returns>True when a page can be reached.</returns>
        internal bool CanEnter(string id, DialogueContext context, HashSet<DialogueNode> visited)
        {
            if (string.IsNullOrEmpty(id) || !Nodes.TryGetValue(id, out DialogueNode node) || !visited.Add(node))
                return false;
            switch (node.Kind)
            {
                case DialogueNodeKind.Entry:
                    return context.Matches(node.Entry) || CanEnter(node.Next, context, visited);
                case DialogueNodeKind.Condition:
                    return CanEnter(context.Matches(node.Condition) ? node.Next : node.Otherwise, context, visited);
                case DialogueNodeKind.Select:
                    foreach (DialogueBranch branch in node.Branches)
                        if (context.Matches(branch.Condition) && (node.Selection != DialogueSelection.WeightedRandom || Weight(branch) > 0d)
                            && CanEnter(branch.Target, context, visited))
                            return true;
                    return CanEnter(node.Otherwise, context, visited);
                case DialogueNodeKind.Subtree:
                    return Dependencies[node.Subtree].CanEnter(node.Subtree.Start, context, visited) || CanEnter(node.Next, context, visited);
                case DialogueNodeKind.Wait:
                    return CanEnter(node.Next, context, visited);
                default:
                    return false;
            }
        }

        /// <summary>Combines a branch weight with its directly referenced entry's reusable weight.</summary>
        /// <param name="branch">Candidate connection.</param>
        /// <returns>A non-negative effective weight.</returns>
        internal double Weight(DialogueBranch branch)
        {
            DialogueNode target = Nodes[branch.Target];
            return (double)branch.Weight * (target.Kind == DialogueNodeKind.Entry ? target.Entry.Weight : 1f);
        }
        #endregion
        #endregion
    }
}
