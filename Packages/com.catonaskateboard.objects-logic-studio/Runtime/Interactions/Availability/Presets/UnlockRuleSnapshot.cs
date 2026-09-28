using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies transferable interaction kinds without serializing references to another prefab instance.</summary>
    public enum InteractionTemplateKind { Hover, Grab, Drop, Throw, Dispenser, Container, Dialogue, Slice, Contact, Outline, Spawn, AssemblyStation, AssemblyProduct }

    /// <summary>Describes an existing interaction for explicit remapping when a rule preset is imported.</summary>
    [Serializable]
    public sealed class InteractionTemplateReference
    {
        #region Fields

        [Header("Interaction Mapping")]
        [Tooltip("Whether this template captured an assigned interaction.")]
        public bool Assigned;
        [Tooltip("Relative hierarchy route captured automatically during export.")]
        public string Path = string.Empty;
        [Tooltip("Interaction type used to match an existing destination component.")]
        public InteractionTemplateKind Kind;
        [Tooltip("Interaction name used to distinguish multiple components of the same type.")]
        public string Name = string.Empty;
        [Tooltip("Zero-based occurrence among interactions of this type on the captured branch.")]
        public int Occurrence;

        #endregion
    }

    /// <summary>Stores one reusable rule with hierarchy mappings instead of prefab-local component references.</summary>
    [Serializable]
    public sealed class UnlockRuleSnapshot
    {
        #region Fields

        [Header("Availability Rule")]
        [Tooltip("Name assigned to the imported rule.")]
        public string Name = "Availability Rule";
        [Tooltip("Whether the imported rule is enabled.")]
        public bool Enabled = true;
        [Tooltip("Availability operation and event/input conditions. Local component references are remapped at import.")]
        public InteractionUnlockSettings Settings = new InteractionUnlockSettings();
        [Tooltip("Existing interaction affected by the rule, captured from its source hierarchy.")]
        public InteractionTemplateReference Target = new InteractionTemplateReference();
        [Tooltip("Existing incoming interaction used by Replace.")]
        public InteractionTemplateReference Replacement = new InteractionTemplateReference();
        [Tooltip("One source mapping per condition. Input conditions retain their action reference instead.")]
        public InteractionTemplateReference[] Sources = Array.Empty<InteractionTemplateReference>();
        [Tooltip("Player tools allowed to trigger this rule.")]
        public InteractionToolRequirement ToolRequirement = new InteractionToolRequirement();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks a detached template before import, export or preset update.</summary>
        /// <param name="warning">Receives incomplete mappings or invalid condition values.</param>
        /// <returns>True when the snapshot contains complete reusable rule data.</returns>
        public bool TryValidate(out string warning)
        {
            // Actual destination components are checked separately during import.
            warning = "Configure a named rule, target mapping and at least one complete condition.";
            if (string.IsNullOrWhiteSpace(Name) || Settings == null || Target == null || !Target.Assigned
                || Settings.Operation is not (InteractionAvailabilityChange.Unlock or InteractionAvailabilityChange.Lock or InteractionAvailabilityChange.Replace)
                || Settings.Conditions == null || Settings.Conditions.Length == 0 || Sources == null || Sources.Length != Settings.Conditions.Length)
                return false;
            if (Settings.Operation == InteractionAvailabilityChange.Replace
                && (Replacement == null || !Replacement.Assigned || Replacement.Kind != Target.Kind))
                return false;
            for (int index = 0; index < Settings.Conditions.Length; index++)
            {
                InteractionUnlockCondition condition = Settings.Conditions[index];
                if (condition == null)
                    return false;
                switch (condition.Trigger)
                {
                    case UnlockTrigger.Interaction:
                        if (Sources[index] == null || !Sources[index].Assigned
                            || condition.Moment is not (InteractionMoment.Started or InteractionMoment.Completed))
                            return false;
                        break;
                    case UnlockTrigger.InputAction:
                        if (condition.Action == null || condition.Action.action is not { type: InputActionType.Button }
                            || !float.IsFinite(condition.Distance) || condition.Distance <= 0f)
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return ToolRequirement != null && ToolRequirement.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
