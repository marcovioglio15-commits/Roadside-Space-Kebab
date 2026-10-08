using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Transfers the catalog reference and extraction settings without duplicating the shared orders.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Available Orders Preset")]
    public sealed class AvailableOrdersPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Available Orders")]
        [Tooltip("Catalog, destination board and per-spawn extraction settings copied by Import.")]
        public OrderSettings Settings = new OrderSettings();
        [Tooltip("Portable completion-source mapping captured on Export and resolved to an existing destination interaction on Import.")]
        public InteractionTemplateReference Completion = new InteractionTemplateReference();

        #endregion

        #region Properties

        /// <summary>Interaction receiving this reusable configuration.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AvailableOrders;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks portable presentation data without requiring a scene or prefab component.</summary>
        /// <param name="warning">Receives an invalid catalog or missing completion mapping.</param>
        /// <returns>True when the preset can be resolved on a destination object.</returns>
        public bool ValidateOrders(out string warning)
        {
            warning = "Configure available orders.";
            if (Settings == null || !Settings.TryValidate(out warning))
                return false;
            if (Settings.WaitForCompletion && (Completion == null || !Completion.Assigned
                || Completion.Kind == InteractionTemplateKind.AvailableOrders))
                warning = "Select a completion interaction from a sample object before exporting gated orders.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
