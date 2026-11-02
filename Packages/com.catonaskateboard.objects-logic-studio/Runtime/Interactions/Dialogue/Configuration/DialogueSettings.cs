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
    public sealed class DialogueSettings
    {
        #region Fields

        [Header("Dialogue Tree")]
        [Tooltip("Complete conversation sheet. Blocks, entries, triggers and connections are saved in this preset.")]
        public DialogueTree Tree;
        [Tooltip("Choose a different complete tree for exact spawn occurrences or from a threshold onward.")]
        public bool UseSpawnTrees;
        [Tooltip("Exact rules win over the latest matching From threshold. Spawn count is shared with Available Orders.")]
        public DialogueSpawnTree[] SpawnTrees = Array.Empty<DialogueSpawnTree>();
        [Header("Range")]
        [Tooltip("Default player distance in metres for starting or resuming an entry.")]
        [Range(0.1f, 20f)]
        public float Distance = 3f;
        [Tooltip("Default distance in metres that interrupts a page. Must be at least Distance.")]
        [Range(0.1f, 25f)]
        public float ExitDistance = 4f;
        [Tooltip("Higher values win when eligible dialogues compete for the shared HUD.")]
        [Range(-100, 100)]
        public int Priority;
        [Tooltip("Let the selected single actions claim a shared press before this dialogue.")]
        public bool PreferSingleActions = true;
        [Tooltip("Single actions allowed to claim the same physical press first.")]
        public DialogueActionPriority PreferredActions = DialogueActionPriority.Throw;
        [Header("Cursor")]
        [Tooltip("Require a free cursor on this object to start or resume a page.")]
        public bool RequireSightToStart;
        [Tooltip("Require a free cursor on this object to advance or close a page.")]
        public bool RequireSightToContinue;
        [Tooltip("Hide the page when the cursor leaves this object or an obstacle blocks it.")]
        public bool HideWhenSightLost;
        [Tooltip("Solid layers that can block the cursor. Player colliders, this object and triggers are excluded.")]
        public LayerMask ObstacleMask = ~0;
        [Header("Replay")]
        [Tooltip("Resume the page, restart its entry, or return to Start after an interruption.")]
        public DialogueInterruption Interruption;
        [Tooltip("Allow a completed tree to restart after leaving Exit Distance and returning.")]
        public bool ReplayOnReturn = true;
        [Tooltip("Allow a fresh Input Action press to restart a completed tree without leaving its range.")]
        public bool ReplayOnInput;
        [Header("Audio")]
        [Tooltip("Optional voice phrases and completion sound.")]
        public DialogueAudioSettings Audio = new DialogueAudioSettings();
        [Header("Dialogue Box")]
        [Tooltip("Box fade shared by every entry in this dialogue interaction.")]
        public DialogueBoxSettings Box = new DialogueBoxSettings();

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
            warning = "Dialogue needs valid audio, positive range and an Exit Distance at least as large as Distance.";
            if (Audio == null || !Audio.IsValid() || !InteractionValues.Positive(Distance) || !InteractionValues.Positive(ExitDistance)
                || ExitDistance < Distance || Interruption is not (DialogueInterruption.Resume or DialogueInterruption.Restart or DialogueInterruption.RestartTree))
                return false;
            if (Box == null || !Box.TryValidate(out warning))
                return false;
            warning = "Assign a complete Dialogue Tree preset.";
            if (Tree == null || !Tree.TryValidate(out warning, orders, bound))
                return false;
            if (PreferSingleActions && (int)PreferredActions != -1 && (PreferredActions & ~DialogueActionPriority.All) != 0)
            {
                warning = "Choose supported single action priorities.";
                return false;
            }
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
