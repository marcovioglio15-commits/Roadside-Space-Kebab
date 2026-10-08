using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Determines what requests a dialogue after range and consumption conditions pass.</summary>
    public enum DialogueTrigger { Proximity, InputAction, Consumption, SpawnArrival }
    /// <summary>Chooses among dialogue entries whose flag requirements are satisfied.</summary>
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

    /// <summary>Stores trigger, range, arbitration and replay policy independently of input and HUD bindings.</summary>
    [Serializable]
    public sealed class DialogueSettings
    {
        #region Fields

        [Header("Activation")]
        [Tooltip("Start by proximity, by a performed input action, or after new consumption receipts satisfy an entry. Spawn Arrival waits for a Day Flow walk-in request and ignores range and sight.")]
        public DialogueTrigger Trigger;
        [Tooltip("Optional Consumption filter applied to the whole interaction before entry selection.")]
        public DialogueConsumptionFilter Consumption = new DialogueConsumptionFilter();
        [Tooltip("Maximum player-root distance in metres for starting or resuming a dialogue.")]
        public float Distance = 3f;
        [Tooltip("Distance in metres that interrupts an active dialogue. Must be at least the activation distance.")]
        public float ExitDistance = 4f;
        [Tooltip("Higher values start first when several eligible dialogue interactions compete for the HUD.")]
        public int Priority;
        [Tooltip("Let an eligible Single interaction use a shared start or advance action first. A successful Single consumes the press without starting or advancing this dialogue.")]
        public bool PreferSingleActions = true;
        [Header("Visibility")]
        [Tooltip("Require some part of the object to be inside the gameplay camera frame and unobstructed when starting or resuming dialogue.")]
        public bool RequireSightToStart;
        [Tooltip("Require visibility to advance to the next page or close the final page. While blocked, the current page remains unless Hide When Sight Lost is enabled.")]
        public bool RequireSightToContinue;
        [Tooltip("Hide an active dialogue when visibility is lost, independently of the start and page-advance requirements. Use the selected interruption policy when sight returns.")]
        public bool HideWhenSightLost;
        [Tooltip("Solid layers blocking dialogue visibility. Player and dialogue-object colliders, and all triggers, are excluded.")]
        public LayerMask ObstacleMask = ~0;
        [Tooltip("Fallback sight point for objects without renderers or colliders. Visible geometry otherwise determines sight.")]
        public Vector3 SightOffset;
        [Header("Dialogue Flow")]
        [Tooltip("Choose eligible entries in array order, uniformly at random, or using their relative weights.")]
        public DialogueSelection Selection;
        [Tooltip("After interruption, resume the current page, restart its first page, or select the next entry using the configured selection mode.")]
        public DialogueInterruption Interruption;
        [Tooltip("Allow a completed dialogue to start again after the player leaves the exit range and returns. Consumption mode also rearms when new items are consumed.")]
        public bool ReplayOnReturn = true;
        [Tooltip("Shared dialogue entry presets containing pages, selection weights and optional consumption conditions.")]
        public DialogueEntry[] Entries = Array.Empty<DialogueEntry>();
        [Header("Audio")]
        [Tooltip("Optional voice phrases and final-page result sound.")]
        public DialogueAudioSettings Audio = new DialogueAudioSettings();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active flow settings and every authored page without rewriting invalid values.</summary>
        /// <param name="warning">Receives the first missing page or invalid condition.</param>
        /// <param name="orders">Local Available Orders catalog, when validating a bound interaction.</param>
        /// <param name="bound">Whether a destination object is known, so inactive filter data can be ignored.</param>
        /// <returns>True when the dialogue configuration is complete.</returns>
        public bool TryValidate(out string warning, OrderSettings orders = null, bool bound = false)
        {
            // Range hysteresis avoids restarting at the same boundary that just interrupted the HUD.
            warning = string.Empty;
            if (Audio == null || !Audio.IsValid())
                warning = "Choose supported dialogue sounds and a positive voice interval.";
            else if (!InteractionValues.Positive(Distance) || !InteractionValues.Positive(ExitDistance) || ExitDistance < Distance)
                warning = "Dialogue needs positive finite distances; Exit Distance must be at least Distance.";
            else if ((RequireSightToStart || RequireSightToContinue || HideWhenSightLost) && !InteractionValues.Finite(SightOffset))
                warning = "Dialogue Sight Offset must be finite.";
            else if (Trigger is not (DialogueTrigger.Proximity or DialogueTrigger.InputAction or DialogueTrigger.Consumption or DialogueTrigger.SpawnArrival)
                || Selection is not (DialogueSelection.Sequence or DialogueSelection.Random or DialogueSelection.WeightedRandom)
                || Interruption is not (DialogueInterruption.Resume or DialogueInterruption.Restart or DialogueInterruption.SelectNext))
                warning = "Choose supported dialogue trigger, selection and interruption modes.";
            else if (Entries == null || Entries.Length == 0)
                warning = "Add at least one dialogue entry.";
            else if (Trigger == DialogueTrigger.Consumption && (Consumption == null || !Consumption.TryValidate(orders, out warning, !bound)))
                warning = warning.Length > 0 ? warning : "Configure the interaction consumption filter.";
            else
            {
                double totalWeight = 0d;
                for (int entryIndex = 0; entryIndex < Entries.Length; entryIndex++)
                {
                    DialogueEntry entry = Entries[entryIndex];
                    if (entry == null)
                    {
                        warning = "Assign a preset to every dialogue entry or remove the empty reference.";
                        break;
                    }
                    if (!entry.TryValidate(out warning, Trigger == DialogueTrigger.Consumption,
                        Selection == DialogueSelection.WeightedRandom, orders, bound))
                        warning = $"Dialogue entry {entryIndex + 1} ('{entry.Name}'): {warning}";
                    if (warning.Length > 0)
                        break;
                    totalWeight += entry.Weight;
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
