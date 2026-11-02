using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a complete reusable conversation sheet with blocks, entries and connections.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Tree Preset")]
    public sealed class DialogueTree : ScriptableObject
    {
        #region Fields

        [Header("Dialogue Tree")]
        [Tooltip("First block. Selected by name in the tree editor.")]
        [HideInInspector]
        public string Start = string.Empty;
        [Tooltip("Canvas position of the Start marker. Set by dragging it in the tree draft.")]
        [HideInInspector]
        public Vector2 StartPosition;
        [Tooltip("Whether the Start marker has a saved position instead of its initial layout position.")]
        [HideInInspector]
        public bool HasStartPosition;
        [Tooltip("Conversation blocks. Each block owns its alternative entry rows.")]
        public DialogueNode[] Nodes = Array.Empty<DialogueNode>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks this complete sheet before saving or creating a runtime session.</summary>
        /// <param name="warning">Receives the first invalid block, row or connection.</param>
        /// <param name="orders">Optional destination order sources.</param>
        /// <param name="bound">Whether local references must resolve on a known destination.</param>
        /// <returns>True for a complete connected conversation.</returns>
        public bool TryValidate(out string warning, OrderSettings orders = null, bool bound = false)
        {
            return DialogueTreeValidation.Validate(this, orders, bound, out warning);
        }

        #endregion

        #endregion
    }
}
