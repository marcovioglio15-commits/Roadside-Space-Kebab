using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable dialogue pages, requirements and flow independently of item input and HUD bindings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Preset")]
    public sealed class DialoguePreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Dialogue")]
        [Tooltip("Settings copied into a dialogue interaction when Import is used. Item actions and HUD bindings remain local.")]
        public DialogueSettings Settings = new DialogueSettings();

        #endregion

        #region Properties

        /// <summary>Dialogue accepts this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Dialogue;

        #endregion
    }
}
