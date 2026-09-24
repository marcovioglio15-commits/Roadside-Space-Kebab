using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores outline appearance independently of a prefab's renderer bindings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Outline Preset")]
    public sealed class OutlinePreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Outline")]
        [Tooltip("Shader settings copied into the selected outline interaction.")]
        public OutlineSettings Settings = new OutlineSettings();

        #endregion

        #region Properties

        /// <summary>Outline interactions accept this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Outline;

        #endregion
    }
}
