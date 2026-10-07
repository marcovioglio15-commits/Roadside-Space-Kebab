using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a reusable degradation sequence imported through the object workspace.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Object Degradation Preset")]
    public sealed class DegradationPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Degradation")]
        [Tooltip("Reusable timed or impact-driven degradation stages.")]
        public DegradationSettings Settings = new DegradationSettings();

        #endregion
        #region Properties

        /// <summary>Interaction accepting this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ObjectDegradation;

        #endregion
    }
}
