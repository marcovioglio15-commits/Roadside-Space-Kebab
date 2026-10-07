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

        #endregion

        #region Properties

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
                if (Modifications[index] == null || !Modifications[index].TryValidate(out warning))
                {
                    warning = $"Modification {index + 1}: {warning}";
                    return false;
                }
            warning = string.Empty;
            return !RequiresInput || Target != null && Target.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
