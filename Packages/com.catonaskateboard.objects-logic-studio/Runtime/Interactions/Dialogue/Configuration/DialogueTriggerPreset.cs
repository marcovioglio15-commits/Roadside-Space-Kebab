using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one entry activation condition without coupling it to the text asset.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Trigger Preset")]
    public sealed class DialogueTriggerPreset : ScriptableObject
    {
        #region Fields

        [Header("Trigger")]
        [Tooltip("Event selecting this entry while its containing block is active.")]
        public DialogueTrigger Type;
        [Tooltip("Optional Button override for Input Action. Empty uses this Dialogue's Start Action.")]
        public InputActionReference Action;
        [Tooltip("Order result or consumed flags used by Consumption.")]
        public DialogueConsumptionFilter Consumption = new DialogueConsumptionFilter();
        [Tooltip("Optional additional consumption condition. Both filters must match the same event.")]
        public DialogueConsumptionFilter AdditionalConsumption = new DialogueConsumptionFilter();
        [Tooltip("Use this preset's range instead of the interaction's range.")]
        public bool OverrideRange;
        [Tooltip("Player distance in metres required to select this entry.")]
        [Range(0.1f, 20f)]
        public float Distance = 3f;
        [Tooltip("Distance in metres that interrupts this entry. Must be at least Distance.")]
        [Range(0.1f, 25f)]
        public float ExitDistance = 4f;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates only the selected event's active settings.</summary>
        /// <param name="orders">Optional destination order configuration.</param>
        /// <param name="bound">Whether the destination is known.</param>
        /// <param name="warning">Receives missing event data.</param>
        /// <returns>True when the preset can select an entry.</returns>
        public bool TryValidate(OrderSettings orders, bool bound, out string warning)
        {
            warning = "Choose a supported dialogue trigger.";
            if (Type is not (DialogueTrigger.Proximity or DialogueTrigger.InputAction or DialogueTrigger.Consumption or DialogueTrigger.SpawnArrival or DialogueTrigger.OnEnter))
                return false;
            warning = "Range needs finite positive distances; Exit Distance must be at least Distance.";
            if (OverrideRange && (Type is DialogueTrigger.Proximity or DialogueTrigger.InputAction)
                && (!InteractionValues.Positive(Distance) || !InteractionValues.Positive(ExitDistance) || ExitDistance < Distance))
                return false;
            warning = "Choose a Button action for this trigger.";
            if (Type == DialogueTrigger.InputAction && Action != null && Action.action is not { type: InputActionType.Button })
                return false;
            warning = "Configure the Consumption trigger.";
            if (Type == DialogueTrigger.Consumption && (Consumption == null || AdditionalConsumption == null
                || !Consumption.TryValidate(orders, out warning, !bound) || !AdditionalConsumption.TryValidate(orders, out warning, !bound)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
