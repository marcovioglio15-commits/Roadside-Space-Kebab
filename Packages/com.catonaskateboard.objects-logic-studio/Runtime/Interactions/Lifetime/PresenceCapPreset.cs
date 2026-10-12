using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a reusable population limit for independently spawned objects.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Presence Cap Preset")]
    public sealed class PresenceCapPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Presence Cap")]
        [Tooltip("Identity population and offscreen removal policy copied by Import.")]
        public PresenceCapSettings Settings = new PresenceCapSettings();

        #endregion
        #region Properties

        /// <summary>Interaction accepting this snapshot.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.PresenceCap;

        #endregion
    }
}
