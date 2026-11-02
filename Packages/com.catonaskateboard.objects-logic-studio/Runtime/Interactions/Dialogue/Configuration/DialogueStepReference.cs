using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects whole-tree completion or one entry checkpoint through stable editor-selected keys.</summary>
    [Serializable]
    public sealed class DialogueStepReference
    {
        #region Fields

        [Header("Dialogue Step")]
        [Tooltip("Optional block key. Empty waits for the complete dialogue tree.")]
        [HideInInspector]
        public string Block = string.Empty;
        [Tooltip("Optional entry row key. Empty accepts any completed entry in the chosen block.")]
        [HideInInspector]
        public string Entry = string.Empty;

        #endregion

        #region Methods

        #region Matching

        /// <summary>Rejects stale checkpoints after a sheet or entry was removed.</summary>
        /// <param name="source">Resolved completion source.</param>
        /// <param name="warning">Receives a missing block or entry selection.</param>
        /// <returns>True for whole completion or a checkpoint present in a possible spawn sheet.</returns>
        public bool TryValidate(ObjectInteraction source, out string warning)
        {
            warning = string.Empty;
            if (string.IsNullOrEmpty(Block))
                return true;
            if (source is ObjectDialogue dialogue)
            {
                if (Contains(dialogue.Settings.Tree))
                    return true;
                if (dialogue.Settings.UseSpawnTrees && dialogue.Settings.SpawnTrees != null)
                    foreach (DialogueSpawnTree rule in dialogue.Settings.SpawnTrees)
                        if (rule != null && Contains(rule.Tree))
                            return true;
            }
            warning = "Choose an existing Dialogue block or entry checkpoint.";
            return false;
        }

        /// <summary>Finds a selectable entry boundary without requiring the whole sheet to validate again.</summary>
        /// <param name="tree">Possible spawn sheet.</param>
        /// <returns>True when this checkpoint can be published.</returns>
        private bool Contains(DialogueTree tree)
        {
            if (tree?.Nodes == null)
                return false;
            foreach (DialogueNode block in tree.Nodes)
                if (block != null && !block.End && block.Id == Block && block.Entries != null)
                    foreach (DialogueRow row in block.Entries)
                        if (row != null && (string.IsNullOrEmpty(Entry) || row.Id == Entry))
                            return true;
            return false;
        }

        /// <summary>Distinguishes checkpoints from full-tree completion without emitting duplicate completion events.</summary>
        /// <param name="source">Interaction publishing its current boundary.</param>
        /// <param name="moment">Boundary currently being published.</param>
        /// <returns>True for the selected completion boundary.</returns>
        public bool Matches(ObjectInteraction source, InteractionMoment moment)
        {
            if (string.IsNullOrEmpty(Block))
                return moment == InteractionMoment.Completed;
            return moment == InteractionMoment.Progressed && source is ObjectDialogue dialogue && dialogue.CompletedBlock == Block
                && (string.IsNullOrEmpty(Entry) || dialogue.CompletedEntry == Entry);
        }

        #endregion

        #endregion
    }
}
