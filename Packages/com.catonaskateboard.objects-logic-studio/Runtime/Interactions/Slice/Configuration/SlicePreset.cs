using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable slice steps independently of the item's input, name and VFX bindings.</summary>
    [CreateAssetMenu(fileName = "Slice Preset", menuName = "Objects Logic Studio/Slice Preset")]
    public sealed class SlicePreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Slice")]
        [Tooltip("Reusable targeting and sequence settings copied into the selected Slice interaction.")]
        public SliceSettings Settings = new SliceSettings();

        #endregion

        #region Properties

        /// <summary>Restricts import and update to Slice feature cards.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Slice;

        #endregion
    }
}
