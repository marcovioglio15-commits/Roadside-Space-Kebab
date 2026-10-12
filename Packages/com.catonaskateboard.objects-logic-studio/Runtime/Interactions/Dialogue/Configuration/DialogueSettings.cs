using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Determines what requests a dialogue after range and consumption conditions pass.</summary>
    public enum DialogueTrigger { Proximity, InputAction, Consumption, SpawnArrival }
    /// <summary>Chooses among eligible dialogue entries or branches of a graph selector.</summary>
    public enum DialogueSelection { Sequence, Random, WeightedRandom }
    /// <summary>Controls retained dialogue progress after interruption by range or availability.</summary>
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
        [Tooltip("Let selected interaction types use the same start or advance button first, including separate input actions bound to that physical button.")]
        public bool PreferSingleActions = true;
        [Tooltip("Eligible interaction types that may consume a shared press before this dialogue. Only Throw has priority by default.")]
        public DialogueActionPriority PreferredActions = DialogueActionPriority.Throw;
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
        [Tooltip("After interruption, resume the current page, restart its entry, or select the next eligible entry.")]
        public DialogueInterruption Interruption;
        [Tooltip("Allow a completed dialogue to start again after the player leaves the exit range and returns. Consumption mode also rearms when new items are consumed.")]
        public bool ReplayOnReturn = true;
        [Tooltip("For Input Action triggers, allow a fresh press to start another entry after completion without leaving Exit Distance. Holding the button never repeats automatically.")]
        public bool ReplayOnInput;
        [Tooltip("Select eligible entries in sequence, uniformly at random or using their weights.")]
        public DialogueSelection Selection;
        [Tooltip("Shared entry presets used by default. Each entry contains its own ordered pages and optional consumption filter.")]
        public DialogueEntry[] Entries = Array.Empty<DialogueEntry>();
        [Tooltip("Replace entries and selection mode for exact spawn occurrences or from a spawn threshold onward.")]
        public bool UseSpawnPresets;
        [Tooltip("Exact rules win over the highest matching From threshold. Only Entries and Selection are read from each preset; activation, audio, inputs and nested spawn rules remain local.")]
        public DialogueSpawnPreset[] SpawnPresets = Array.Empty<DialogueSpawnPreset>();
        [Tooltip("Shared conversation graph containing connected entries, conditions, waits and subtrees.")]
        [HideInInspector]
        public DialogueTree Tree;
        [Tooltip("Select a different tree for exact prefab spawn occurrences or from a spawn threshold onward.")]
        [HideInInspector]
        public bool UseSpawnTrees;
        [Tooltip("Exact rules win over the highest applicable From threshold. Counts are shared with Available Orders.")]
        [HideInInspector]
        public DialogueSpawnTree[] SpawnTrees = Array.Empty<DialogueSpawnTree>();
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
                || Interruption is not (DialogueInterruption.Resume or DialogueInterruption.Restart or DialogueInterruption.SelectNext))
                warning = "Choose supported dialogue trigger, selection and interruption modes.";
            else if (Trigger == DialogueTrigger.Consumption && (Consumption == null || !Consumption.TryValidate(orders, out warning, !bound)))
                warning = warning.Length > 0 ? warning : "Configure the interaction consumption filter.";
            else if (PreferSingleActions && (int)PreferredActions != -1 && (PreferredActions & ~DialogueActionPriority.All) != 0)
                warning = "Choose supported dialogue input priority types.";
            else
            {
                if (!ValidateEntries(this, Trigger == DialogueTrigger.Consumption, orders, bound, out warning))
                    return false;
                if (!UseSpawnPresets)
                    return true;
                warning = "Add at least one spawn preset rule or disable spawn presets.";
                if (SpawnPresets == null || SpawnPresets.Length == 0)
                    return false;
                // Reusable content is checked in the destination's context without following nested overrides.
                HashSet<(OrderSpawnMatch, int)> rules = new HashSet<(OrderSpawnMatch, int)>();
                foreach (DialogueSpawnPreset rule in SpawnPresets)
                {
                    warning = "Spawn preset rules require a positive occurrence, assigned preset and unique mode/occurrence pair.";
                    if (rule == null || rule.Preset == null || rule.Spawn <= 0
                        || rule.Match is not (OrderSpawnMatch.Exact or OrderSpawnMatch.From) || !rules.Add((rule.Match, rule.Spawn)))
                        return false;
                    if (!ValidateEntries(rule.Preset.Settings, Trigger == DialogueTrigger.Consumption, orders, bound, out warning))
                        return false;
                }
                warning = string.Empty;
            }
            return warning.Length == 0;
        }

        /// <summary>Checks reusable entries against the consuming interaction, without following a preset's own spawn rules.</summary>
        /// <param name="content">Default or spawn-specific entry selection.</param>
        /// <param name="consumption">Whether receipt conditions participate.</param>
        /// <param name="orders">Destination order catalogs.</param>
        /// <param name="bound">Whether the destination is known.</param>
        /// <param name="warning">Receives the first invalid entry or selection mode.</param>
        /// <returns>True when the entry list can be selected and displayed.</returns>
        private static bool ValidateEntries(DialogueSettings content, bool consumption, OrderSettings orders, bool bound, out string warning)
        {
            warning = "Add at least one entry and choose a supported selection mode.";
            if (content == null || content.Entries == null || content.Entries.Length == 0
                || content.Selection is not (DialogueSelection.Sequence or DialogueSelection.Random or DialogueSelection.WeightedRandom))
                return false;
            // Shared entries retain their own pages and predicates; the destination defines which predicates are active.
            foreach (DialogueEntry entry in content.Entries)
            {
                warning = "Assign every dialogue entry preset.";
                if (entry == null || !entry.TryValidate(out warning, consumption, content.Selection == DialogueSelection.WeightedRandom, orders, bound))
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Selects reusable entry content once per spawn without overriding the component's activation policy.</summary>
        /// <param name="spawn">One-based occurrence shared with Available Orders.</param>
        /// <returns>Matching preset content, or this interaction's default content.</returns>
        public DialogueSettings ResolveEntries(int spawn)
        {
            DialogueSettings selected = this;
            int threshold = 0;
            // Exact overrides are independent of array order; otherwise the nearest preceding threshold wins.
            if (UseSpawnPresets && SpawnPresets != null)
                foreach (DialogueSpawnPreset rule in SpawnPresets)
                    if (rule?.Preset != null && rule.Spawn <= spawn)
                    {
                        if (rule.Match == OrderSpawnMatch.Exact && rule.Spawn == spawn)
                            return rule.Preset.Settings;
                        if (rule.Match == OrderSpawnMatch.From && rule.Spawn > threshold)
                        {
                            selected = rule.Preset.Settings;
                            threshold = rule.Spawn;
                        }
                    }
            return selected;
        }

        /// <summary>Freezes the conversation source using the same exact/threshold precedence as orders.</summary>
        /// <param name="spawn">One-based prefab occurrence.</param>
        /// <returns>The exact override, latest threshold or default tree.</returns>
        public DialogueTree ResolveTree(int spawn)
        {
            DialogueTree selected = Tree;
            int threshold = 0;
            if (UseSpawnTrees && SpawnTrees != null)
                foreach (DialogueSpawnTree rule in SpawnTrees)
                    if (rule != null && rule.Spawn <= spawn)
                    {
                        if (rule.Match == OrderSpawnMatch.Exact && rule.Spawn == spawn)
                            return rule.Tree;
                        if (rule.Match == OrderSpawnMatch.From && rule.Spawn > threshold)
                        {
                            selected = rule.Tree;
                            threshold = rule.Spawn;
                        }
                    }
            return selected;
        }

        #endregion

        #endregion
    }
}
