using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable spray sauce configuration independently of object input bindings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Spray Sauce Preset")]
    public sealed class SpraySaucePreset : ExtendedInteractionPreset
    {
        #region Fields
        [Header("Spray Sauce")]
        [Tooltip("Reusable interaction settings copied locally when imported.")]
        public SpraySauceSettings Settings = new SpraySauceSettings();
        #endregion
        #region Properties
        /// <summary>Interaction accepting this settings snapshot.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.SpraySauce;
        #endregion
    }
}
