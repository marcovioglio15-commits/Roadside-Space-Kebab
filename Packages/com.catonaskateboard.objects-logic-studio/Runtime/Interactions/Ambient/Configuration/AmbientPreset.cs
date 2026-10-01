using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Transfers ambient event and radius settings between passive interactions.</summary>
    [CreateAssetMenu(fileName = "Ambient Preset", menuName = "Objects Logic Studio/Ambient Preset")]
    public sealed class AmbientPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Ambient")]
        [Tooltip("Reusable ambient event and attenuation radius.")]
        public AmbientSettings Settings = new AmbientSettings();

        #endregion
        #region Properties

        /// <summary>Restricts preset operations to Play Ambient cards.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.PlayAmbient;

        #endregion
    }
}
