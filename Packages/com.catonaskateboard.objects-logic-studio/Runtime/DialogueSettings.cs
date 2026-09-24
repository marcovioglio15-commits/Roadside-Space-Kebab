using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Determines what requests a dialogue after range and consumption conditions pass.</summary>
    public enum DialogueTrigger { Proximity, InputAction, Consumption }
    /// <summary>Chooses among dialogue entries whose tag requirements are satisfied.</summary>
    public enum DialogueSelection { Sequence, Random, WeightedRandom }
    /// <summary>Controls the next entry after a dialogue is interrupted by range or availability.</summary>
    public enum DialogueInterruption { Resume, Restart, SelectNext }

    /// <summary>Defines one explicit dialogue page; the advance action moves to the next array entry.</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        #region Fields

        [Header("Dialogue Page")]
        [Tooltip("Optional speaker label shown above this page.")]
        public string Speaker = string.Empty;
        [Tooltip("Complete text shown until the advance action is performed. Each array entry is a separate page.")]
        [TextArea(3, 10)]
        public string Text = string.Empty;

        #endregion
    }

    /// <summary>Groups an explicit sequence of pages with optional consumption eligibility and selection weight.</summary>
    [Serializable]
    public sealed class DialogueEntry
    {
        #region Fields

        [Header("Dialogue Entry")]
        [Tooltip("Label used to identify this dialogue entry in the tool.")]
        public string Name = "Dialogue";
        [Tooltip("Relative selection chance in Weighted Random mode. Zero excludes this entry from weighted selection.")]
        public float Weight = 1f;
        [Tooltip("Consumption receipts required before this entry becomes eligible. Empty means no consumption requirement.")]
        public ItemTagRequirement[] RequiredTags = Array.Empty<ItemTagRequirement>();
        [Tooltip("Require every listed tag. When disabled, any one listed requirement is sufficient.")]
        public bool RequireAll = true;
        [Tooltip("Pages shown in this exact order. The advance action displays the next page and closes after the last one.")]
        public DialogueLine[] Lines = { new DialogueLine() };

        #endregion

        #region Methods

        #region Conditions

        /// <summary>Evaluates consumption receipts without removing them or changing future eligibility.</summary>
        /// <param name="item">Item retaining the consumption history.</param>
        /// <returns>True when this entry's tag requirements are satisfied.</returns>
        internal bool Matches(ObjectItem item)
        {
            // No requirements means the entry is available to ordinary range or input triggers.
            if (RequiredTags.Length == 0)
                return true;
            foreach (ItemTagRequirement requirement in RequiredTags)
                if ((item.CountConsumed(requirement.Tag) >= requirement.Count) != RequireAll)
                    return !RequireAll;
            return RequireAll;
        }

        #endregion

        #endregion
    }

    /// <summary>Stores trigger, range, arbitration and replay policy independently of input and HUD bindings.</summary>
    [Serializable]
    public sealed class DialogueSettings
    {
        #region Fields

        [Header("Activation")]
        [Tooltip("Start by proximity, by a performed input action, or after new consumption receipts satisfy an entry. All modes require the player to be in range.")]
        public DialogueTrigger Trigger;
        [Tooltip("Maximum player-root distance in metres for starting or resuming a dialogue.")]
        public float Distance = 3f;
        [Tooltip("Distance in metres that interrupts an active dialogue. Must be at least the activation distance.")]
        public float ExitDistance = 4f;
        [Tooltip("Higher values start first when several eligible dialogue interactions compete for the HUD.")]
        public int Priority;
        [Header("Dialogue Flow")]
        [Tooltip("Choose eligible entries in array order, uniformly at random, or using their relative weights.")]
        public DialogueSelection Selection;
        [Tooltip("After interruption, resume the current page, restart its first page, or select the next entry using the configured selection mode.")]
        public DialogueInterruption Interruption;
        [Tooltip("Allow a completed dialogue to start again after the player leaves the exit range and returns. Consumption mode also rearms when new items are consumed.")]
        public bool ReplayOnReturn = true;
        [Tooltip("Available dialogue sequences and their independent tag conditions.")]
        public DialogueEntry[] Entries = { new DialogueEntry() };

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active flow settings and every authored page without rewriting invalid values.</summary>
        /// <param name="warning">Receives the first missing page or invalid condition.</param>
        /// <returns>True when the dialogue configuration is complete.</returns>
        public bool TryValidate(out string warning)
        {
            // Range hysteresis avoids restarting at the same boundary that just interrupted the HUD.
            warning = string.Empty;
            if (!InteractionValues.Positive(Distance) || !InteractionValues.Positive(ExitDistance) || ExitDistance < Distance)
                warning = "Dialogue needs positive finite distances; Exit Distance must be at least Distance.";
            else if (Trigger is not (DialogueTrigger.Proximity or DialogueTrigger.InputAction or DialogueTrigger.Consumption)
                || Selection is not (DialogueSelection.Sequence or DialogueSelection.Random or DialogueSelection.WeightedRandom)
                || Interruption is not (DialogueInterruption.Resume or DialogueInterruption.Restart or DialogueInterruption.SelectNext))
                warning = "Choose supported dialogue trigger, selection and interruption modes.";
            else if (Entries == null || Entries.Length == 0)
                warning = "Add at least one dialogue entry.";
            else
            {
                double totalWeight = 0d;
                foreach (DialogueEntry entry in Entries)
                {
                    if (entry == null || entry.RequiredTags == null || entry.Lines == null || entry.Lines.Length == 0)
                    {
                        warning = "Each dialogue entry needs conditions and at least one explicit text page.";
                        break;
                    }
                    if (Selection == DialogueSelection.WeightedRandom && (!InteractionValues.Finite(entry.Weight) || entry.Weight < 0f))
                        warning = "Dialogue weights must be finite and non-negative.";
                    if (Trigger == DialogueTrigger.Consumption && entry.RequiredTags.Length == 0)
                        warning = "Consumption-triggered entries need at least one consumed-tag requirement.";
                    foreach (ItemTagRequirement requirement in entry.RequiredTags)
                        if (requirement == null || string.IsNullOrWhiteSpace(requirement.Tag) || requirement.Count <= 0)
                            warning = "Each consumed-tag requirement needs a project tag and a positive count.";
                    foreach (DialogueLine line in entry.Lines)
                        if (line == null || string.IsNullOrWhiteSpace(line.Text))
                            warning = "Write text for every dialogue page or remove the unused page.";
                    totalWeight += entry.Weight;
                    if (warning.Length > 0)
                        break;
                }
                if (warning.Length == 0 && Selection == DialogueSelection.WeightedRandom && totalWeight <= 0d)
                    warning = "At least one dialogue entry needs a positive selection weight.";
            }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
