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
        [Tooltip("Event that can start this entry while its block is active. Consumption evaluates contact receipts; On Enter runs when the block is entered.")]
        public DialogueTrigger Type;
        [Tooltip("Button action that starts this entry in Input Action mode. Empty uses the dialogue interaction's Start Action.")]
        public InputActionReference Action;
        [Tooltip("Optional consumption filter. With Available Orders, evaluates this spawn's tickets; otherwise evaluates consumed flag quantities.")]
        public DialogueConsumptionFilter Consumption = new DialogueConsumptionFilter();
        [Tooltip("Replace the dialogue interaction's start and exit distances for Proximity or Input Action. Other trigger types do not use these distances.")]
        public bool OverrideRange;
        [Tooltip("Maximum player distance in metres for this Proximity or Input Action entry to start when Override Range is enabled.")]
        [Range(0.1f, 20f)]
        public float Distance = 3f;
        [Tooltip("Player distance in metres beyond which this entry is interrupted when Override Range is enabled. Must be at least Distance.")]
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
            if (Type == DialogueTrigger.Consumption && (Consumption == null || !Consumption.TryValidate(orders, out warning, !bound)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
