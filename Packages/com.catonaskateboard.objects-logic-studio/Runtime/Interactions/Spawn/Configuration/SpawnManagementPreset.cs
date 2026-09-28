using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares completion conditions and randomized output settings between spawn rule objects.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Spawn Management Preset")]
    public sealed class SpawnManagementPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Spawn Management")]
        [Tooltip("Reusable source prefab conditions, independent instance limits and random output choices.")]
        public SpawnManagementSettings Settings = new SpawnManagementSettings();

        #endregion

        #region Properties

        /// <summary>Feature category represented by this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.SpawnManagement;

        #endregion
    }
}
