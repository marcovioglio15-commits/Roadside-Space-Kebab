using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the counter driving dispenser appearance thresholds.</summary>
    public enum DispenseStepCounter { Remaining, Dispensed }

    /// <summary>Chooses carried, world or container-only output.</summary>
    public enum DispenseMode { ToHand, SingleOutput, ContinuousOutput, ScoopOnly }

    /// <summary>Configures either new prefab supply or recovery from this object's Container.</summary>
    [Serializable]
    public sealed class DispenserSettings
    {
        #region Fields

        private const int maximumOutputsPerShot = 32;

        [Header("Targeting")]
        [Tooltip("Player range, aim and obstruction settings.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Supply")]
        [Tooltip("Take existing items from the Container on this object. Empty storage disables dispensing; no prefab is spawned.")]
        public bool UseContainer;
        [Tooltip("Prefab supplied at the outlet or displayed by the Scoop Only magnets.")]
        public GameObject Prefab;
        [Tooltip("Magnets, floating motion and visible stock steps for Scoop Only.")]
        public ScoopDisplaySettings ScoopDisplay = new ScoopDisplaySettings();
        [Tooltip("Allow unlimited portions. Disable to define finite supply and the spaces available to a linked Container.")]
        public bool Unlimited = true;
        [Tooltip("Initial prefab stock, or total spaces for stored-only supply. Returned originals refill vacancies when Container limits storage to this Dispenser.")]
        [Range(1, 1000)]
        public int Stock = 1;
        [Tooltip("Destroy this object after its final finite portion. Animated scoops finish before the dispenser disappears.")]
        public bool DestroyWhenEmpty;
        [Header("Output")]
        [Tooltip("Output for the targeting action. To Hand carries an item. Single and Continuous Output release items. Scoop Only fills held containers.")]
        public DispenseMode Mode;
        [Tooltip("Give the specific tool a different output mode when Use Tool and the targeting action are distinct.")]
        public bool SeparateToolOutput;
        [Tooltip("Output when the specific tool uses its own action or confirms a weapon hit.")]
        public DispenseMode ToolMode = DispenseMode.ContinuousOutput;
        [Tooltip("Scaled seconds between outputs while holding the action. The initial press dispenses immediately.")]
        [Range(0.02f, 30f)]
        public float Interval = 0.25f;
        [Tooltip("Items released together when the required weapon hits through Continuous Output.")]
        [Range(1, maximumOutputsPerShot)]
        public int OutputsPerShot = 1;
        [Tooltip("Fan shot outputs out from the outlet with lift and tumble. The incoming shot skews the fan.")]
        public bool ScatterShotOutputs = true;
        [Tooltip("Scale the travel distance of the dispersed items. One uses the base launch. Spread and tumble stay the same.")]
        [Range(0.001f, 4f)]
        public float ScatterDistanceMultiplier = 1f;
        [Tooltip("Output poses relative to the dispenser root. One is selected uniformly at random for each successful output.")]
        public DispenserOutlet[] Outlets = { new DispenserOutlet() };
        [Tooltip("Seconds to move from this dispenser's pivot to the object's Grab carry pose. Overrides Instant and Transition Duration for this withdrawal only.")]
        [Range(0.01f, 3f)]
        public float PickupDuration = 0.35f;
        [Tooltip("Local Euler rotation applied before the item enters the carry pose.")]
        public Vector3 OutputRotation;
        [Header("Fill Steps")]
        [Tooltip("Use remaining supply or dispensed objects for appearance thresholds. Finite prefab supply uses net withdrawals, so refilling reverses its steps; other supply modes count lifetime withdrawals.")]
        public DispenseStepCounter StepCounter;
        [Tooltip("Appearance selected by the configured counter. Refills reverse finite-supply thresholds. Unlimited prefab supply supports the dispensed counter only.")]
        public InventoryFillStep[] FillSteps = Array.Empty<InventoryFillStep>();
        /// <summary>Whether the selected supply counter has a meaningful finite value.</summary>
        public bool SupportsSteps => !ScoopOnly && (StoredSupply || !Unlimited || StepCounter == DispenseStepCounter.Dispensed);
        /// <summary>Whether supply floats at magnets for held containers to scoop.</summary>
        public bool ScoopOnly => Mode == DispenseMode.ScoopOnly;
        /// <summary>Whether the active mode restores stored originals.</summary>
        public bool StoredSupply => !ScoopOnly && UseContainer;

        #endregion

        #region Methods

        #region Output

        /// <summary>Checks whether this setup can expose a second physical output route.</summary>
        /// <param name="mode">Output used by the targeting action.</param>
        /// <param name="requirement">Tool restriction used by this dispenser.</param>
        /// <param name="tool">Selected tool asset.</param>
        /// <returns>True for a physical dispenser restricted to a single tool.</returns>
        public static bool CanSeparateToolOutput(DispenseMode mode, InteractionToolMode requirement, PlayerTool tool)
        {
            return (mode == DispenseMode.ToHand || UsesWorldOutput(mode)) && requirement == InteractionToolMode.Specific && tool != null;
        }

        /// <summary>Identifies modes that need outlets and can release items without a free carry slot.</summary>
        /// <param name="mode">Output mode used for the current route.</param>
        /// <returns>True for single or continuous world output.</returns>
        public static bool UsesWorldOutput(DispenseMode mode) => mode is DispenseMode.SingleOutput or DispenseMode.ContinuousOutput;

        /// <summary>Checks when one weapon contact can release several world outputs.</summary>
        /// <param name="mode">Dispenser output mode.</param>
        /// <param name="requirement">Tool restriction used by the interaction.</param>
        /// <param name="tool">Tool asset selected by the Specific restriction.</param>
        /// <returns>True for continuous output restricted to a particular weapon.</returns>
        public static bool UsesWeaponBatch(DispenseMode mode, InteractionToolMode requirement, PlayerTool tool)
        {
            // Batch settings belong to one explicitly selected weapon.
            return mode == DispenseMode.ContinuousOutput && requirement == InteractionToolMode.Specific && tool != null && tool.UsesImpactInteractions;
        }

        #endregion

        #region Validation

        /// <summary>Checks an optional replacement prefab before an empty display can refill.</summary>
        /// <param name="warning">Receives a missing root, item or identity.</param>
        /// <returns>True when the replacement can use the same display magnets.</returns>
        private bool ValidateRestock(out string warning)
        {
            warning = string.Empty;
            // A disabled restock option does not constrain the selected asset.
            GameObject prefab = ScoopDisplay.RestockPrefab;
            if (ScoopDisplay.RestockForNewContainer && prefab != null && (prefab.scene.IsValid() || prefab.transform.parent != null
                || !prefab.activeSelf || !prefab.TryGetComponent(out ObjectItem _) || !prefab.TryGetComponent(out CatOnASkateboard.StudioIdentity.ObjectIdentity _)))
                warning = "Choose an active restock prefab root with Object Item and Object Identity.";
            return warning.Length == 0;
        }

        /// <summary>Checks reusable supply settings without requiring a scene or modifying the prefab.</summary>
        /// <param name="warning">Receives a missing prefab or invalid setting.</param>
        /// <param name="requirement">Optional tool restriction used to validate weapon output.</param>
        /// <returns>True when these settings can supply a grabbable item.</returns>
        public bool TryValidate(out string warning, InteractionToolRequirement requirement = null)
        {
            // A linked container is validated on the destination component, not inside its reusable preset.
            warning = "Dispenser targeting is missing.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            if (ScoopOnly)
            {
                warning = "Choose an active prefab root with Object Item and configure its floating display.";
                return Prefab != null && !Prefab.scene.IsValid() && Prefab.transform.parent == null && Prefab.activeSelf
                    && Prefab.GetComponent<ObjectItem>() != null && ScoopDisplay != null
                    && ScoopDisplay.TryValidate(Unlimited ? ScoopDisplay.VisibleCount : Stock, out warning)
                    && ValidateRestock(out warning);
            }
            if (SupportsSteps && !InventoryFillStep.TryValidate(FillSteps, out warning))
                return false;
            if (StepCounter is not (DispenseStepCounter.Remaining or DispenseStepCounter.Dispensed))
            {
                warning = "Choose a supported dispense step counter.";
                return false;
            }
            warning = "Select a supported dispenser output mode.";
            if (Mode is not (DispenseMode.ToHand or DispenseMode.SingleOutput or DispenseMode.ContinuousOutput or DispenseMode.ScoopOnly))
                return false;
            // Validate shared placement against both active output modes.
            bool separate = SeparateToolOutput && requirement != null && CanSeparateToolOutput(Mode, requirement.Mode, requirement.Tool);
            DispenseMode toolOutput = separate ? ToolMode : Mode;
            if (separate && toolOutput != DispenseMode.ToHand && !UsesWorldOutput(toolOutput))
                return false;
            if (requirement != null && UsesWeaponBatch(toolOutput, requirement.Mode, requirement.Tool))
            {
                if (OutputsPerShot is < 1 or > maximumOutputsPerShot)
                {
                    warning = "Outputs Per Shot must be between 1 and " + maximumOutputsPerShot + ".";
                    return false;
                }
                if (ScatterShotOutputs && (!float.IsFinite(ScatterDistanceMultiplier) || ScatterDistanceMultiplier is < 0.001f or > 4f))
                {
                    warning = "Set Scatter Distance Multiplier from 0.001 to 4.";
                    return false;
                }
            }
            if (UsesWorldOutput(Mode) || UsesWorldOutput(toolOutput))
            {
                warning = "World output requires at least one finite outlet pose and a positive continuous interval.";
                bool usesInterval = Mode == DispenseMode.ContinuousOutput
                    || toolOutput == DispenseMode.ContinuousOutput && requirement?.Tool?.UsesImpactInteractions != true;
                if (Outlets == null || Outlets.Length == 0 || usesInterval && !InteractionValues.Positive(Interval))
                    return false;
                foreach (DispenserOutlet outlet in Outlets)
                    if (outlet is not { IsValid: true })
                        return false;
            }
            warning = string.Empty;
            if ((Mode == DispenseMode.ToHand || toolOutput == DispenseMode.ToHand)
                && (!InteractionValues.Positive(PickupDuration) || !InteractionValues.Finite(OutputRotation)))
                warning = "Dispenser pickup duration must be positive and finite, with a finite output rotation.";
            else if (!Unlimited && Stock <= 0)
                warning = "Dispenser Stock must be a positive whole number.";
            else if (!StoredSupply && !ScoopOnly && (Prefab == null || Prefab.scene.IsValid() || Prefab.transform.parent != null || !Prefab.activeSelf))
                warning = "Choose an active prefab root for supply.";
            else if (!StoredSupply && !ScoopOnly && (!Prefab.TryGetComponent(out ObjectGrab grab) || !grab.enabled))
                warning = "Choose an active prefab root with an enabled Grab.";
            else if (!StoredSupply && !ScoopOnly)
                return Prefab.GetComponent<ObjectGrab>().TryValidate(out warning);
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
