using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares a configured product reference and table placement settings between assembly tables.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Assembly Table Preset")]
    public sealed class AssemblyStationPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Assembly Table")]
        [Tooltip("Reusable product prefab, range and output placement. Player action remains bound per table.")]
        public AssemblyStationSettings Settings = new AssemblyStationSettings();

        #endregion

        #region Properties

        /// <summary>Feature category represented by this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AssemblyStation;

        #endregion
    }
}
