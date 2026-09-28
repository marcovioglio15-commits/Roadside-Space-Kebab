using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Transfers one availability rule between existing prefab interaction hierarchies.</summary>
    [CreateAssetMenu(fileName = "Unlock Rule", menuName = "Objects Logic Studio/Unlock Rule Preset")]
    public sealed class UnlockRulePreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Rule Snapshot")]
        [Tooltip("Rule configuration and automatically captured interaction mappings.")]
        public UnlockRuleSnapshot Rule = new UnlockRuleSnapshot();

        #endregion

        #region Properties

        /// <summary>Restricts this preset to the availability-rule category.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Unlock;

        #endregion
    }
}
