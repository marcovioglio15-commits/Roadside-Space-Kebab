using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Groups independent flag-filtered modifications under one interaction and its shared bindings.</summary>
    [Serializable]
    public sealed class ContactModificationSettings
    {
        #region Fields

        [Header("Modifications")]
        [Tooltip("Independent contact modifications in priority order. Each has its own flags, timing and effects. Item reservations prevent conflicting modifications from running together.")]
        public ContactModificationDefinition[] Modifications = Array.Empty<ContactModificationDefinition>();
        [Tooltip("Reach and aim used when a modification waits for confirmation after snapping.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Consumption")]
        [Tooltip("Optional local dialogue completion required before consuming modifications may start. Non-consuming modifications remain available.")]
        public ContactDialogueGate ConsumeOn = new ContactDialogueGate();
        [Tooltip("Collect recipe prices or order price overrides, with optional deferred HUD settlement for consumed items.")]
        public ContactPriceSettings Price = new ContactPriceSettings();
        [Header("Modification Audio")]
        [Tooltip("Play frying audio only while a modification is actively applying its effects. Waiting, interruptions and completed effects stay silent.")]
        public bool FryingSound;

        #endregion

        #region Properties

        /// <summary>Whether a timed visual modification provides an active interval for frying audio.</summary>
        public bool SupportsFrying => Modifications != null && Array.Exists(Modifications,
            definition => definition != null && definition.Settings != null && definition.Settings.Duration > 0f && !definition.Settings.IdentityOnly);

        /// <summary>Whether any rule requests the interaction's local Button binding.</summary>
        public bool RequiresInput => Modifications != null && Array.Exists(Modifications,
            definition => definition != null && definition.Settings?.Preparation is { Snap: true, RequireInput: true });

        /// <summary>Whether any permanent modification consumes the contacted participant.</summary>
        public bool ConsumesOther => Modifications != null && Array.Exists(Modifications,
            definition => definition != null && definition.Settings != null && !definition.Settings.WhileContact
                && definition.Settings.Other != null && definition.Settings.Other.Consume && definition.Settings.Self != null && !definition.Settings.Self.Consume);
        /// <summary>Whether any modification can publish a consumption receipt.</summary>
        public bool ConsumesAny => Modifications != null && Array.Exists(Modifications,
            definition => definition != null && definition.Settings != null
                && (definition.Settings.Self != null && definition.Settings.Self.Consume || definition.Settings.Other != null && definition.Settings.Other.Consume));
        /// <summary>Shortest configured effect duration, used to validate automatic timing across every modification.</summary>
        public float MinimumDuration
        {
            get
            {
                float duration = float.PositiveInfinity;
                if (Modifications != null)
                    foreach (ContactModificationDefinition definition in Modifications)
                        if (definition != null && definition.Settings != null)
                            duration = Mathf.Min(duration, definition.Settings.Duration);
                return float.IsPositiveInfinity(duration) ? 0f : duration;
            }
        }

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks every authored modification without changing its flags, timing or effects.</summary>
        /// <param name="warning">Receives the first invalid modification and its array position.</param>
        /// <returns>True when at least one complete modification is configured.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Add at least one flag-filtered contact modification.";
            if (Modifications == null || Modifications.Length == 0)
                return false;
            for (int index = 0; index < Modifications.Length; index++)
            {
                if (Modifications[index] == null)
                {
                    warning = $"Modification {index + 1}: assign a definition or remove this entry.";
                    return false;
                }
                if (!Modifications[index].TryValidate(out warning))
                {
                    warning = $"Modification {index + 1}: {warning}";
                    return false;
                }
                // One shared definition must not run twice and duplicate tint, toggle or consumption effects.
                for (int previous = 0; previous < index; previous++)
                    if (Modifications[previous] == Modifications[index])
                    {
                        warning = $"Modification {index + 1} repeats '{Modifications[index].name}' from entry {previous + 1}. Assign each definition once.";
                        return false;
                    }
            }
            warning = "Price collection settings are missing.";
            if (ConsumesAny && (Price == null || !Price.TryValidate(null, out warning)))
                return false;
            warning = string.Empty;
            if (ConsumesAny && (ConsumeOn == null || ConsumeOn.Enabled && (string.IsNullOrWhiteSpace(ConsumeOn.DialogueName) || ConsumeOn.Occurrence < 0)))
            {
                warning = "Select the local Dialogue that enables consumption.";
                return false;
            }
            return !RequiresInput || Target != null && Target.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
