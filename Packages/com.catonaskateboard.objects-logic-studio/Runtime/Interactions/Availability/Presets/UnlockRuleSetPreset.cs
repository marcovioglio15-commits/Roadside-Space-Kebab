using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Transfers the complete set of availability rules on one selected prefab object.</summary>
    [CreateAssetMenu(fileName = "Unlock Rule Set", menuName = "Objects Logic Studio/Unlock Rule Set Preset")]
    public sealed class UnlockRuleSetPreset : ScriptableObject
    {
        #region Fields

        [Header("Rule Set")]
        [Tooltip("Ordered availability-rule snapshots imported onto one destination object.")]
        public UnlockRuleSnapshot[] Rules = Array.Empty<UnlockRuleSnapshot>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks every rule before a set can replace the destination's availability-rule components.</summary>
        /// <param name="warning">Receives a missing or invalid rule.</param>
        /// <returns>True when at least one complete rule is available.</returns>
        public bool TryValidate(out string warning)
        {
            // Validate the entire set before creating or modifying any destination component.
            warning = "Export at least one configured availability rule.";
            if (Rules == null || Rules.Length == 0)
                return false;
            foreach (UnlockRuleSnapshot rule in Rules)
                if (rule == null || !rule.TryValidate(out warning))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
