using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable dirt trail configuration independently of object input bindings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dirt Trail Preset")]
    public sealed class DirtTrailPreset : ExtendedInteractionPreset
    {
        #region Fields
        [Header("Dirt Trail")]
        [Tooltip("Reusable interaction settings copied locally when imported.")]
        public DirtTrailSettings Settings = new DirtTrailSettings();
        #endregion
        #region Properties
        /// <summary>Interaction accepting this settings snapshot.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.DirtTrail;
        #endregion
    }
}
