using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Overrides a dialogue tree for one occurrence or from a threshold onward.</summary>
    [Serializable]
    public sealed class DialogueSpawnTree
    {
        #region Fields
        [Header("Spawn Tree")]
        [Tooltip("Exact wins for its single occurrence; From uses the highest applicable threshold.")]
        public OrderSpawnMatch Match;
        [Tooltip("One-based prefab spawn occurrence shared with Available Orders.")]
        public int Spawn = 1;
        [Tooltip("Conversation used when this spawn rule wins.")]
        public DialogueTree Tree;
        #endregion
    }
}
