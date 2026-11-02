using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one object's graph cursor, page and selection history without changing presets.</summary>
    internal sealed class DialogueSession
    {
        #region State

        private readonly Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
        private readonly Dictionary<string, int> cursors = new Dictionary<string, int>();
        internal readonly HashSet<string> ProximityUsed = new HashSet<string>();
        internal readonly DialogueTree Tree;
        internal DialogueNode Block;
        internal DialogueRow Row;
        internal DialogueNode Route;
        internal DialogueRow RouteRow;
        internal DialogueTriggerPreset ActivationTrigger;
        internal string ActivationId;
        internal int Page;
        internal float PageTime;
        internal int ConsumptionRevision;
        internal int ContactRevision;
        internal bool Started;

        #endregion

        #region Properties

        /// <summary>Whether the current cursor reached the explicit End block.</summary>
        internal bool Completed => Block.End;
        /// <summary>Block considered for a new request, including replay from Start.</summary>
        internal DialogueNode RequestBlock => Route ?? (Completed ? nodes[Tree.Start] : Block);
        /// <summary>Current explicit page.</summary>
        internal DialogueLine Line => Row?.Entry.Lines[Page];

        #endregion

        #region Methods

        #region Progress

        /// <summary>Indexes a previously validated sheet and captures its initial event revisions.</summary>
        /// <param name="tree">Validated sheet selected for this spawn.</param>
        /// <param name="receipt">Current consumption revision.</param>
        /// <param name="contact">Current gated contact revision.</param>
        internal DialogueSession(DialogueTree tree, int receipt, int contact)
        {
            Tree = tree;
            foreach (DialogueNode node in tree.Nodes)
                nodes.Add(node.Id, node);
            Block = nodes[tree.Start];
            ConsumptionRevision = receipt;
            ContactRevision = contact;
        }

        /// <summary>Selects among eligible rows without changing sequence cursors until Begin.</summary>
        /// <param name="eligible">Matching row indices in the request block.</param>
        /// <returns>Selected row index, or minus one when none match.</returns>
        internal int Choose(List<int> eligible)
        {
            if (eligible.Count == 0)
                return -1;
            DialogueNode block = RequestBlock;
            switch (block.Selection)
            {
                case DialogueSelection.Random:
                    return eligible[Random.Range(0, eligible.Count)];
                case DialogueSelection.WeightedRandom:
                    double total = 0d;
                    foreach (int index in eligible)
                        total += block.Entries[index].Weight;
                    double draw = Random.value * total;
                    foreach (int index in eligible)
                    {
                        draw -= block.Entries[index].Weight;
                        if (draw < 0d)
                            return index;
                    }
                    return eligible[eligible.Count - 1];
                case DialogueSelection.Sequence:
                    cursors.TryGetValue(block.Id, out int next);
                    for (int offset = 0; offset < block.Entries.Length; offset++)
                    {
                        int candidate = (next + offset) % block.Entries.Length;
                        if (eligible.Contains(candidate))
                            return candidate;
                    }
                    return -1;
                default:
                    return eligible[0];
            }
        }

        /// <summary>Commits the selected row and its next sequence cursor.</summary>
        /// <param name="index">Previously selected request row.</param>
        internal void Begin(int index)
        {
            DialogueNode request = RequestBlock;
            DialogueRow activation = RouteRow ?? request.Entries[index];
            if (Completed)
                Restart(true);
            Block = request;
            Row = Block.Entries[index];
            ActivationTrigger = activation.Trigger;
            ActivationId = activation.Id;
            Route = null;
            RouteRow = null;
            Page = 0;
            PageTime = 0f;
            cursors[Block.Id] = (index + 1) % Block.Entries.Length;
        }

        /// <summary>Moves one page and reports the last-page boundary to the owner.</summary>
        /// <returns>True when the entry's final page has closed.</returns>
        internal bool Advance()
        {
            PageTime = 0f;
            return Row != null && ++Page >= Row.Entry.Lines.Length;
        }

        /// <summary>Follows the completed row while leaving the shared HUD available to other dialogues.</summary>
        internal void Follow()
        {
            Block = nodes[Row.Next];
            Row = null;
            Page = 0;
            PageTime = 0f;
        }

        /// <summary>Returns to Start while preserving local sequence history.</summary>
        /// <param name="newRequest">Whether a completed tree is starting a separate request.</param>
        internal void Restart(bool newRequest)
        {
            Block = nodes[Tree.Start];
            Route = null;
            RouteRow = null;
            ActivationTrigger = null;
            ActivationId = string.Empty;
            Row = null;
            Page = 0;
            PageTime = 0f;
            ProximityUsed.Clear();
            if (newRequest)
                Started = false;
        }

        /// <summary>Resolves a validated branch destination without searching the node array.</summary>
        /// <param name="id">Stable destination key.</param>
        /// <returns>The indexed conversation block.</returns>
        internal DialogueNode Find(string id)
        {
            return nodes[id];
        }

        #endregion

        #endregion
    }
}
