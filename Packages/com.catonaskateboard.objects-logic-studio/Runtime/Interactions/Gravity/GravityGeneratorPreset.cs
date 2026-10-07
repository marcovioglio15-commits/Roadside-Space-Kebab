using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable gravity selection, pulse and restoration settings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Gravity Generator Preset")]
    public sealed class GravityGeneratorPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Gravity Generator")]
        [Tooltip("Reusable gravity pulse settings; the restoration action remains local to each object.")]
        public GravitySettings Settings = new GravitySettings();

        #endregion
        #region Properties

        /// <summary>Interaction accepting this reusable preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.GravityGenerator;

        #endregion
    }
}
