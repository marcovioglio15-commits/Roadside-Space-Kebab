using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies a reusable contact or dialogue configuration imported as an independent item draft.</summary>
    public abstract class ExtendedInteractionPreset : ScriptableObject
    {
        #region Tool Requirement

        [Header("Player Tool")]
        [Tooltip("Tool restriction copied into an interaction by Import; existing object snapshots remain independent.")]
        public InteractionToolRequirement ToolRequirement = new InteractionToolRequirement();

        #endregion

        #region Properties

        /// <summary>Feature accepting this preset type.</summary>
        public abstract ExtendedInteractionKind Kind { get; }

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks transferable settings without requiring destination-specific component bindings.</summary>
        /// <param name="warning">Receives invalid settings or an unsupported preset type.</param>
        /// <returns>True when the complete snapshot is safe to import or export.</returns>
        public bool TryValidate(out string warning)
        {
            // Local actions, HUD and renderer paths are checked again when applying to the destination.
            if (!ToolRequirement.TryValidate(out warning))
                return false;
            return this switch
            {
                ContactModificationPreset contact => contact.Settings.TryValidate(out warning),
                DegradationPreset degradation => degradation.Settings.TryValidate(out warning),
                GravityGeneratorPreset gravity => gravity.Settings.TryValidate(out warning),
                DialoguePreset dialogue => dialogue.Settings.TryValidate(out warning),
                AmbientPreset ambient => ambient.Settings.TryValidate(out warning),
                SlicePreset slice => slice.Settings.TryValidate(out warning),
                OutlinePreset outline => outline.Settings.TryValidate(out warning),
                SpawnManagementPreset spawn => spawn.Settings.TryValidate(out warning),
                AssemblyStationPreset station => station.Settings.TryValidate(out warning),
                AvailableOrdersPreset orders => orders.ValidateOrders(out warning),
                AssemblyProductPreset product => product.ValidateProduct(out warning),
                UnlockRulePreset unlock => unlock.Rule != null && unlock.Rule.TryValidate(out warning),
                _ => false
            };
        }

        #endregion

        #endregion
    }
}
