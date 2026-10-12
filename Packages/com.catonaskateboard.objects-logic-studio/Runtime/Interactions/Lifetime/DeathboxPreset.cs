using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable death-volume settings independently of scene collider references.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Deathbox Preset")]
    public sealed class DeathboxPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Deathbox")]
        [Tooltip("Contact filtering and player respawn policy copied by Import.")]
        public DeathboxSettings Settings = new DeathboxSettings();

        #endregion
        #region Properties

        /// <summary>Interaction accepting this snapshot.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Deathbox;

        #endregion
    }
}
