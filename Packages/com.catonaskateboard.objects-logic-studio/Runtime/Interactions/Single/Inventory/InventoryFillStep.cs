using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects a complete appearance at a storage or remaining-stock threshold.</summary>
    [Serializable]
    public sealed class InventoryFillStep
    {
        #region Fields

        [Header("Fill Step")]
        [Tooltip("Minimum item count for this appearance. The highest reached threshold wins, including while the count decreases.")]
        [Range(0, 1000)]
        public int Count;
        [Tooltip("Change the container appearance or display one visual piece for each occupied magnet.")]
        public InventoryFillMode Mode;
        [Tooltip("Placement and optional ingredient model for Actual Item Fill.")]
        public InventoryItemFill ItemFill = new InventoryItemFill();
        [Tooltip("Mesh and material changes relative to the original object. Below all thresholds the original appearance is restored.")]
        public ItemAppearanceSettings Appearance = new ItemAppearanceSettings();
        [Tooltip("Optional reversible flag state selected by this threshold.")]
        public InventoryFlagSettings Flags = new InventoryFlagSettings();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks threshold uniqueness and reusable appearance data without sorting or changing the list.</summary>
        /// <param name="steps">Authored fill thresholds.</param>
        /// <param name="warning">Receives invalid thresholds or replacements.</param>
        /// <param name="allowItemFill">Whether these thresholds belong to a container with hidden or scooped contents.</param>
        /// <returns>True when the configuration can select one unambiguous step for any count.</returns>
        internal static bool TryValidate(InventoryFillStep[] steps, out string warning, bool allowItemFill = false)
        {
            // Empty lists opt out of appearance management entirely.
            warning = "Fill steps need unique non-negative counts and valid appearance settings.";
            if (steps == null)
                return false;
            HashSet<int> counts = new HashSet<int>();
            foreach (InventoryFillStep step in steps)
            {
                if (step == null || step.Count < 0 || !counts.Add(step.Count) || step.Flags == null)
                    return false;
                switch (step.Mode)
                {
                    case InventoryFillMode.Appearance when step.Appearance != null:
                        if (!step.Appearance.TryValidate(out warning))
                            return false;
                        break;
                    case InventoryFillMode.ActualItemFill when allowItemFill && step.ItemFill != null:
                        if (!step.ItemFill.TryValidate(step.Count, out warning))
                            return false;
                        break;
                    default:
                        warning = "Actual Item Fill requires a container with hidden contents or Scoop From Dispenser.";
                        return false;
                }
                if (!step.Flags.TryValidate(out warning))
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks that imported targets exist on the destination object before Apply.</summary>
        /// <param name="steps">Validated threshold list.</param>
        /// <param name="item">Destination owning mesh and renderer branches.</param>
        /// <param name="warning">Receives the first unavailable binding.</param>
        /// <returns>True when every threshold can be applied to this hierarchy.</returns>
        internal static bool CanBind(InventoryFillStep[] steps, ObjectItem item, out string warning)
        {
            // Bindings are checked at activation or explicit authoring boundaries, never per frame.
            warning = "A fill-step target is missing or belongs to a different item. Rebind it using the hierarchy selector.";
            foreach (InventoryFillStep step in steps)
                if (step.Mode == InventoryFillMode.Appearance && !step.Appearance.CanBind(item))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
