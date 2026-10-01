namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one original appearance across both inventory features and changes it only at count boundaries.</summary>
    internal sealed class InventoryFillRun
    {
        #region State

        private readonly InventoryFillStep[][] steps;
        private readonly ItemAppearanceChanges[][] changes;
        private readonly int[] selected = { -1, -1 };

        #endregion

        #region Methods

        #region Binding

        /// <summary>Retains two prepared step lists whose snapshots all precede the first visual change.</summary>
        /// <param name="configuration">Container and dispenser thresholds.</param>
        /// <param name="prepared">Bound appearance operations for each threshold.</param>
        private InventoryFillRun(InventoryFillStep[][] configuration, ItemAppearanceChanges[][] prepared)
        {
            // A shared baseline avoids one feature capturing the other's already modified mesh.
            steps = configuration;
            changes = prepared;
        }

        /// <summary>Prepares both features atomically before either can alter the object's geometry.</summary>
        /// <param name="item">Owner of the target hierarchy.</param>
        /// <param name="container">Optional storage configuration.</param>
        /// <param name="dispenser">Optional supply configuration.</param>
        /// <param name="run">Receives the shared visual state.</param>
        /// <param name="warning">Receives an invalid appearance target.</param>
        /// <returns>True when all configured steps were bound successfully.</returns>
        internal static bool TryCreate(ObjectItem item, ContainerSettings container, DispenserSettings dispenser,
            out InventoryFillRun run, out string warning)
        {
            // The two features may be linked for counts or use their own independent quantities.
            run = null;
            InventoryFillStep[][] configuration = { container?.FillSteps ?? System.Array.Empty<InventoryFillStep>(),
                dispenser != null && (dispenser.UseContainer || !dispenser.Unlimited)
                    ? dispenser.FillSteps : System.Array.Empty<InventoryFillStep>() };
            ItemAppearanceChanges[][] prepared = new ItemAppearanceChanges[2][];
            for (int group = 0; group < configuration.Length; group++)
            {
                if (!InventoryFillStep.TryValidate(configuration[group], out warning))
                    return false;
                prepared[group] = new ItemAppearanceChanges[configuration[group].Length];
                for (int index = 0; index < prepared[group].Length; index++)
                    if (!ItemAppearanceChanges.TryPrepare(item, configuration[group][index].Appearance, out prepared[group][index], out warning))
                        return false;
            }
            run = new InventoryFillRun(configuration, prepared);
            warning = string.Empty;
            return true;
        }

        #endregion

        #region Counts

        /// <summary>Applies threshold changes after a committed deposit or withdrawal.</summary>
        /// <param name="stored">Actual retained instances in the Container.</param>
        /// <param name="remaining">Remaining Dispenser supply, or minus one for unbounded supply.</param>
        internal void Update(int stored, int remaining)
        {
            // Returning across a threshold restores the baseline before applying the newly selected complete step.
            int container = Select(steps[0], stored);
            int dispenser = Select(steps[1], remaining);
            if (container == selected[0] && dispenser == selected[1])
                return;
            Restore();
            selected[0] = container;
            selected[1] = dispenser;
            for (int group = 0; group < selected.Length; group++)
                if (selected[group] >= 0)
                    changes[group][selected[group]].Commit();
        }

        /// <summary>Restores original geometry when changing steps or starting a fresh retained-scene session.</summary>
        internal void Restore()
        {
            // Reverse feature order mirrors commit order if both intentionally target the same component.
            for (int group = selected.Length - 1; group >= 0; group--)
            {
                if (selected[group] >= 0)
                    changes[group][selected[group]].Restore();
                selected[group] = -1;
            }
        }

        /// <summary>Finds the greatest reached threshold without requiring sorted authoring.</summary>
        /// <param name="configuration">One feature's unique thresholds.</param>
        /// <param name="count">Current item count; negative disables that feature's appearance.</param>
        /// <returns>The selected array index, or minus one below the first threshold.</returns>
        private static int Select(InventoryFillStep[] configuration, int count)
        {
            // Selection costs depend only on authored steps and run when inventory changes.
            int selected = -1;
            for (int index = 0; index < configuration.Length; index++)
                if (configuration[index].Count <= count && (selected < 0 || configuration[index].Count > configuration[selected].Count))
                    selected = index;
            return selected;
        }

        #endregion

        #endregion
    }
}
