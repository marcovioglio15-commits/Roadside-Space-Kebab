using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the counter driving dispenser appearance thresholds.</summary>
    public enum DispenseStepCounter { Remaining, Dispensed }

    /// <summary>Configures either new prefab supply or recovery from this object's Container.</summary>
    [Serializable]
    public sealed class DispenserSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Player range, aim and obstruction settings.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Supply")]
        [Tooltip("Take existing items from the Container on this object. Empty storage disables dispensing; no prefab is spawned.")]
        public bool UseContainer;
        [Tooltip("Prefab root containing an enabled Grab and a valid physical body.")]
        public GameObject Prefab;
        [Tooltip("Allow unlimited prefab supply. Disable to define finite supply and the spaces available to a linked Container.")]
        public bool Unlimited = true;
        [Tooltip("Initial prefab stock, or total spaces for stored-only supply. Returned originals refill vacancies when Container limits storage to this Dispenser.")]
        public int Stock = 1;
        [Header("Output")]
        [Tooltip("Seconds to move from this dispenser's pivot to the object's Grab carry pose. Overrides Instant and Transition Duration for this withdrawal only.")]
        public float PickupDuration = 0.35f;
        [Tooltip("Local Euler rotation applied before the item enters the carry pose.")]
        public Vector3 OutputRotation;
        [Header("Fill Steps")]
        [Tooltip("Use remaining supply or dispensed objects for appearance thresholds. Finite prefab supply uses net withdrawals, so refilling reverses its steps; other supply modes count lifetime withdrawals.")]
        public DispenseStepCounter StepCounter;
        [Tooltip("Appearance selected by the configured counter. Refills reverse finite-supply thresholds. Unlimited prefab supply supports the dispensed counter only.")]
        public InventoryFillStep[] FillSteps = Array.Empty<InventoryFillStep>();
        /// <summary>Whether the selected supply counter has a meaningful finite value.</summary>
        public bool SupportsSteps => UseContainer || !Unlimited || StepCounter == DispenseStepCounter.Dispensed;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable supply settings without requiring a scene or modifying the prefab.</summary>
        /// <param name="warning">Receives a missing prefab or invalid setting.</param>
        /// <returns>True when these settings can supply a grabbable item.</returns>
        public bool TryValidate(out string warning)
        {
            // A linked container is validated on the destination component, not inside its reusable preset.
            warning = "Dispenser targeting is missing.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            if (SupportsSteps && !InventoryFillStep.TryValidate(FillSteps, out warning))
                return false;
            if (StepCounter is not (DispenseStepCounter.Remaining or DispenseStepCounter.Dispensed))
            {
                warning = "Choose a supported dispense step counter.";
                return false;
            }
            if (!InteractionValues.Positive(PickupDuration) || !InteractionValues.Finite(OutputRotation))
                warning = "Dispenser pickup duration must be positive and finite, with a finite output rotation.";
            else if (!Unlimited && Stock <= 0)
                warning = "Dispenser Stock must be a positive whole number.";
            else if (!UseContainer && (Prefab == null || Prefab.scene.IsValid() || Prefab.transform.parent != null
                || !Prefab.activeSelf || !Prefab.TryGetComponent(out ObjectGrab grab) || !grab.enabled))
                warning = "Choose an active prefab root with an enabled Grab.";
            else if (!UseContainer)
                return Prefab.GetComponent<ObjectGrab>().TryValidate(out warning);
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
