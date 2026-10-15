using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Flow operations supported by an authored dialogue tree.</summary>
    public enum DialogueNodeKind { Entry, Select, Condition, Wait, Subtree, End }

    /// <summary>One selectable outgoing connection with its own eligibility and relative weight.</summary>
    [Serializable]
    public sealed class DialogueBranch
    {
        #region Fields
        [Header("Branch")]
        [Tooltip("Label shown beside this connection in the graph.")]
        public string Name = "Branch";
        [Tooltip("Stable destination node key; the editor displays its name.")]
        public string Target = string.Empty;
        [Tooltip("Relative selection weight. Zero excludes this branch in weighted mode.")]
        [Range(0.01f, 10f)]
        public float Weight = 1f;
        [Tooltip("Conditions combined with the destination entry's consumption filter.")]
        public DialogueCondition Condition = new DialogueCondition();
        #endregion
    }

    /// <summary>Stores one graph operation and editor position independently of runtime progress.</summary>
    [Serializable]
    public sealed class DialogueNode
    {
        #region Fields
        [Header("Node")]
        [Tooltip("Stable connection key assigned by the editor. Renaming a node preserves its connections.")]
        public string Id = string.Empty;
        [Tooltip("Short title displayed in the graph and destination menus.")]
        public string Name = "Entry";
        [Tooltip("Position in the editor canvas; this does not affect execution.")]
        public Vector2 Position;
        [Tooltip("Operation performed when the runner reaches this node.")]
        public DialogueNodeKind Kind;
        [Tooltip("Shared page sequence displayed by an Entry node.")]
        public DialogueEntry Entry;
        [Tooltip("Next node after completion, or the True branch of a Condition node.")]
        public string Next = string.Empty;
        [Tooltip("False branch of a Condition, or optional fallback when Select has no eligible branch.")]
        public string Otherwise = string.Empty;
        [Header("Selection")]
        [Tooltip("Select a branch that can reach an eligible page, in sequence, uniformly or by weight. Sequence cursors are local to each object.")]
        public DialogueSelection Selection;
        [Tooltip("Outgoing choices made by the runner, not choices presented to the player.")]
        public DialogueBranch[] Branches = Array.Empty<DialogueBranch>();
        [Header("Conditions")]
        [Tooltip("Predicate evaluated by a Condition node against the conversation's captured context.")]
        public DialogueCondition Condition = new DialogueCondition();
        [Header("Wait")]
        [Tooltip("Positive scaled seconds before moving to the next node. Interruption pauses this timer.")]
        [Range(0f, 60f)]
        public float Duration = 1f;
        [Header("Subtree")]
        [Tooltip("Shared tree called by this node. Its End returns to this node's Next connection.")]
        public DialogueTree Subtree;
        #endregion
    }
}
