using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Events supported by reusable dialogue trigger presets.</summary>
    public enum DialogueTrigger { Proximity, InputAction, Consumption, SpawnArrival, OnEnter }
    /// <summary>Selection policy among matching entries in one block.</summary>
    public enum DialogueSelection { Sequence, Random, WeightedRandom, FirstMatching }
    /// <summary>Progress retained after range, sight or availability interrupts a page.</summary>
    public enum DialogueInterruption { Resume, Restart, RestartTree }

    /// <summary>Stores one explicit page without automatic text splitting.</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        #region Fields

        [Header("Page")]
        [Tooltip("Optional speaker shown above this page.")]
        public string Speaker = string.Empty;
        [Tooltip("Complete page text. Advance moves to the next page.")]
        [TextArea(3, 10)]
        public string Text = string.Empty;

        #endregion
    }

    /// <summary>Combines a complete tree preset with local input, visibility and replay policy.</summary>
    [Serializable]
    public sealed class DialogueSettings : DialoguePolicy
    {
        #region Fields

        [Header("Dialogue Tree")]
        [Tooltip("Complete conversation sheet. Blocks, entries, triggers and connections are saved in this preset.")]
        public DialogueTree Tree;
        [Tooltip("Choose a different complete tree for exact spawn occurrences or from a threshold onward.")]
        public bool UseSpawnTrees;
        [Tooltip("Exact rules win over the latest matching From threshold. Spawn count is shared with Available Orders.")]
        public DialogueSpawnTree[] SpawnTrees = Array.Empty<DialogueSpawnTree>();
        #endregion

        #region Methods

        /// <summary>Whether any reachable Input Action trigger uses the object's default Button.</summary>
        public bool NeedsStartAction
        {
            get
            {
                if (Tree == null || NeedsAction(Tree))
                    return true;
                if (UseSpawnTrees && SpawnTrees != null)
                    foreach (DialogueSpawnTree rule in SpawnTrees)
                        if (rule?.Tree != null && NeedsAction(rule.Tree))
                            return true;
                return false;
            }
        }

        /// <summary>Checks a sheet's default input dependency without resolving player actions.</summary>
        /// <param name="tree">Complete sheet.</param>
        /// <returns>True when a trigger relies on the local Start Action.</returns>
        private static bool NeedsAction(DialogueTree tree)
        {
            foreach (DialogueNode block in tree.Nodes)
                foreach (DialogueRow row in block.Entries)
                    if (row.Trigger != null && row.Trigger.Type == DialogueTrigger.InputAction && row.Trigger.Action == null)
                        return true;
            return false;
        }

        #region Validation

        /// <summary>Checks default and spawn-specific sheets in the destination's context.</summary>
        /// <param name="warning">Receives invalid range, audio or tree data.</param>
        /// <param name="orders">Optional destination order catalogs.</param>
        /// <param name="bound">Whether the destination object is known.</param>
        /// <returns>True when all reachable presets can run.</returns>
        public bool TryValidate(out string warning, OrderSettings orders = null, bool bound = false)
        {
            if (!TryValidatePolicy(out warning))
                return false;
            warning = "Assign a complete Dialogue Tree preset.";
            if (Tree == null || !Tree.TryValidate(out warning, orders, bound))
                return false;
            if (UseSpawnTrees)
            {
                warning = "Add at least one complete spawn tree rule.";
                if (SpawnTrees == null || SpawnTrees.Length == 0)
                    return false;
                HashSet<(OrderSpawnMatch, int)> rules = new HashSet<(OrderSpawnMatch, int)>();
                foreach (DialogueSpawnTree rule in SpawnTrees)
                {
                    warning = "Spawn rules need a positive occurrence, a tree and a unique Exact/From pair.";
                    if (rule == null || rule.Spawn <= 0 || rule.Tree == null || !rules.Add((rule.Match, rule.Spawn))
                        || rule.Match is not (OrderSpawnMatch.Exact or OrderSpawnMatch.From))
                        return false;
                    if (!rule.Tree.TryValidate(out warning, orders, bound))
                        return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Freezes one complete conversation sheet for this object's spawn.</summary>
        /// <param name="spawn">One-based occurrence shared with orders.</param>
        /// <returns>The exact override, latest threshold or default tree.</returns>
        public DialogueTree ResolveTree(int spawn)
        {
            DialogueTree selected = Tree;
            int threshold = 0;
            if (UseSpawnTrees && SpawnTrees != null)
                foreach (DialogueSpawnTree rule in SpawnTrees)
                    if (rule?.Tree != null && rule.Spawn <= spawn)
                    {
                        if (rule.Match == OrderSpawnMatch.Exact && rule.Spawn == spawn)
                            return rule.Tree;
                        if (rule.Match == OrderSpawnMatch.From && rule.Spawn > threshold)
                        {
                            selected = rule.Tree;
                            threshold = rule.Spawn;
                        }
                    }
            return selected;
        }

        #endregion

        #endregion
    }
}
