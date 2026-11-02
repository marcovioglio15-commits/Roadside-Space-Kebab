using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Pairs reusable text and a trigger with one outgoing connection inside a block.</summary>
    [Serializable]
    public sealed class DialogueRow
    {
        #region Fields

        [Header("Entry")]
        [Tooltip("Stable row key. Assigned by the editor and preserved when reordered.")]
        [HideInInspector]
        public string Id = string.Empty;
        [Tooltip("Short label shown beneath the containing block.")]
        public string Name = "Entry";
        [Tooltip("Reusable speaker and page sequence.")]
        public DialogueEntry Entry;
        [Tooltip("Reusable condition selecting this entry while its block is active.")]
        public DialogueTriggerPreset Trigger;
        [Tooltip("Destination reached after the final page. Selected by block name or by connecting the output.")]
        [HideInInspector]
        public string Next = string.Empty;
        [Tooltip("Relative chance when the block uses Weighted Random. Zero excludes this row.")]
        [Range(0f, 10f)]
        public float Weight = 1f;

        #endregion
    }

    /// <summary>Groups alternative entries beneath one clearly named conversation block.</summary>
    [Serializable]
    public sealed class DialogueNode
    {
        #region Fields

        [Header("Block")]
        [Tooltip("Stable block key. Names and layout never change connections.")]
        [HideInInspector]
        public string Id = string.Empty;
        [Tooltip("Title identifying this group of entries.")]
        public string Name = "Dialogue";
        [Tooltip("Canvas position saved by dragging this block.")]
        [HideInInspector]
        public Vector2 Position;
        [Tooltip("Use the position chosen by dragging this block. New sheets start with an ordered layout.")]
        [HideInInspector]
        public bool HasPosition;
        [Tooltip("Finish this tree when the flow reaches this block.")]
        public bool End;
        [Tooltip("Choose a conversation branch using triggers without showing a page in this block.")]
        public bool Routing;
        [Tooltip("Use this branch's range, priority, replay, audio and box settings instead of the interaction defaults.")]
        public bool OverridePolicy;
        [Tooltip("Presentation and input policy used while this branch is requested or speaking.")]
        public DialoguePolicy Policy = new DialoguePolicy();
        [Tooltip("Choose the first matching row, cycle eligible rows, or select randomly.")]
        public DialogueSelection Selection = DialogueSelection.FirstMatching;
        [Tooltip("Alternative entries owned by this block. Each row has its own trigger and output.")]
        public DialogueRow[] Entries = Array.Empty<DialogueRow>();

        #endregion
    }
}
