using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a portable selection of one local dialogue without retaining prefab-stage references.</summary>
    [Serializable]
    public sealed class ContactDialogueGate
    {
        #region Fields

        [Header("Consumption Activation")]
        [Tooltip("Keep consuming modifications inactive until the selected local Dialogue completes. Qualifying contacts can trigger Awaiting Orders entries meanwhile.")]
        public bool Enabled;
        [Tooltip("Name of the local Dialogue whose completion enables consumption for this spawn.")]
        public string DialogueName = string.Empty;
        [Tooltip("Zero-based occurrence among local dialogues with the same name; selected automatically by the dialogue menu.")]
        public int Occurrence;

        #endregion
        #region Methods
        #region Binding

        /// <summary>Resolves the exact local selection once during activation or authoring validation.</summary>
        /// <param name="owner">Object carrying the modifier and its dialogues.</param>
        /// <returns>The matching dialogue, or null if the binding is missing.</returns>
        internal ObjectDialogue Resolve(GameObject owner)
        {
            int occurrence = 0;
            foreach (ObjectDialogue dialogue in owner.GetComponents<ObjectDialogue>())
                if (dialogue.InteractionName == DialogueName && occurrence++ == Occurrence)
                    return dialogue;
            return null;
        }

        #endregion
        #endregion
    }
}
