using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a reusable recipe and magnet layout with portable product-interaction mappings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Assembly Product Preset")]
    public sealed class AssemblyProductPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Assembly Product")]
        [Tooltip("Recipe, magnet layout and completion settings copied by Import.")]
        public AssemblyProductSettings Settings = new AssemblyProductSettings();
        [Tooltip("Destination interaction descriptors captured during Export, one per product interaction rule.")]
        public InteractionTemplateReference[] Targets = Array.Empty<InteractionTemplateReference>();

        #endregion

        #region Properties

        /// <summary>Interaction receiving this reusable product configuration.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AssemblyProduct;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks portable mappings independently of the destination prefab.</summary>
        /// <param name="warning">Receives invalid recipe settings or incomplete target mappings.</param>
        /// <returns>True when the preset can be resolved against a destination.</returns>
        public bool ValidateProduct(out string warning)
        {
            // Local component references never belong to the portable snapshot.
            if (!AssemblyValidation.TryValidate(Settings, out warning))
                return false;
            warning = "Capture one distinct interaction mapping for every product rule.";
            if (Targets == null || Targets.Length != Settings.InteractionRules.Length)
                return false;
            HashSet<string> mappings = new HashSet<string>(StringComparer.Ordinal);
            foreach (InteractionTemplateReference target in Targets)
                if (target == null || !target.Assigned || target.Occurrence < 0
                    || target.Kind == InteractionTemplateKind.AssemblyProduct
                    || target.Kind < InteractionTemplateKind.Hover || target.Kind > InteractionTemplateKind.Eject
                    || !mappings.Add(JsonUtility.ToJson(target)))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
