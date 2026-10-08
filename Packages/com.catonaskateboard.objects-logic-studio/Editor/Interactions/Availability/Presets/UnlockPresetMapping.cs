using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Captures portable rule mappings and resolves them against existing destination interactions.</summary>
    internal static class UnlockPresetMapping
    {
        #region Methods

        #region Capture

        /// <summary>Captures the current rule draft without saving references to a prefab-stage instance.</summary>
        /// <param name="draft">Pending rule values.</param>
        /// <param name="owner">Existing object whose prefab hierarchy supplies source components.</param>
        /// <returns>A detached rule snapshot with automatically generated mappings.</returns>
        internal static UnlockRuleSnapshot Capture(ExtendedInteractionDraft draft, GameObject owner)
        {
            // Draft IDs resolve against the actual hierarchy before being converted to portable descriptors.
            return Capture(draft.Unlock.Resolve(owner.transform.root), draft.Name, draft.Enabled, draft.ToolRequirement, owner.transform.root);
        }

        /// <summary>Captures one applied component for a complete rule-set snapshot.</summary>
        /// <param name="rule">Existing rule whose settings are exported.</param>
        /// <returns>A detached snapshot without component references to the source prefab.</returns>
        internal static UnlockRuleSnapshot Capture(ObjectInteractionUnlock rule)
        {
            // Source rule identity remains local; only its transferable configuration is stored.
            return Capture(rule.Settings, rule.InteractionName, rule.enabled, rule.ToolRequirement, rule.transform.root);
        }

        /// <summary>Builds route descriptors and removes source-only component references.</summary>
        /// <param name="settings">Resolved source configuration.</param>
        /// <param name="name">Rule name to retain.</param>
        /// <param name="enabled">Authored enabled state.</param>
        /// <param name="tools">Tool requirement to retain.</param>
        /// <param name="root">Source prefab root.</param>
        /// <returns>A complete portable snapshot.</returns>
        private static UnlockRuleSnapshot Capture(InteractionUnlockSettings settings, string name, bool enabled,
            InteractionToolRequirement tools, Transform root)
        {
            // Every source condition has a parallel mapping, including empty descriptors for input-only conditions.
            UnlockRuleSnapshot snapshot = new UnlockRuleSnapshot
            {
                Name = name, Enabled = enabled, Settings = ObjectWorkspace.Copy(settings),
                ToolRequirement = ObjectWorkspace.Copy(tools), Target = Capture(root, settings.Target),
                Replacement = Capture(root, settings.Replacement), Sources = new InteractionTemplateReference[settings.Conditions.Length]
            };
            snapshot.Settings.Target = snapshot.Settings.Replacement = null;
            for (int index = 0; index < snapshot.Sources.Length; index++)
            {
                snapshot.Sources[index] = Capture(root, settings.Conditions[index].Source);
                snapshot.Settings.Conditions[index].Source = null;
            }
            return snapshot;
        }

        /// <summary>Describes a component through its hierarchy, kind, name and occurrence.</summary>
        /// <param name="root">Source prefab root.</param>
        /// <param name="feature">Existing component or null for an unused reference.</param>
        /// <returns>A generated descriptor used only when importing a preset.</returns>
        internal static InteractionTemplateReference Capture(Transform root, ObjectInteraction feature)
        {
            // Component order disambiguates otherwise identical cards on the same branch.
            if (feature == null)
                return new InteractionTemplateReference();
            InteractionTemplateReference reference = new InteractionTemplateReference
            {
                Assigned = true, Path = AnimationUtility.CalculateTransformPath(feature.transform, root),
                Kind = Kind(feature), Name = feature.InteractionName
            };
            foreach (ObjectInteraction candidate in feature.GetComponents<ObjectInteraction>())
            {
                if (candidate == feature)
                    break;
                if (candidate is not ObjectInteractionUnlock && Kind(candidate) == reference.Kind)
                    reference.Occurrence++;
            }
            return reference;
        }

        #endregion

        #region Import

        /// <summary>Resolves the entire rule before modifying any destination draft or component.</summary>
        /// <param name="snapshot">Portable source configuration.</param>
        /// <param name="root">Destination prefab root containing the required existing interactions.</param>
        /// <param name="settings">Receives remapped runtime settings.</param>
        /// <param name="warning">Receives an unresolved or ambiguous interaction mapping.</param>
        /// <returns>True when every required destination interaction can be resolved.</returns>
        internal static bool TryResolve(UnlockRuleSnapshot snapshot, Transform root, out InteractionUnlockSettings settings, out string warning)
        {
            // A partial import must never leave a mixture of source and destination references.
            settings = null;
            warning = "Select a destination prefab containing this rule's existing interactions.";
            if (root == null || snapshot == null || !snapshot.TryValidate(out warning))
                return false;
            settings = ObjectWorkspace.Copy(snapshot.Settings);
            ObjectInteraction[] candidates = root.GetComponentsInChildren<ObjectInteraction>(true);
            settings.Target = Resolve(snapshot.Target, root, candidates);
            settings.Replacement = Resolve(snapshot.Replacement, root, candidates);
            for (int index = 0; index < settings.Conditions.Length; index++)
                settings.Conditions[index].Source = Resolve(snapshot.Sources[index], root, candidates);
            if (settings.TryValidate(root, out warning))
                return true;
            warning = "Cannot map rule '" + snapshot.Name + "'. Add or name its target/source interactions on the destination. " + warning;
            return false;
        }

        /// <summary>Prefers the captured branch and name, then accepts a unique matching destination component.</summary>
        /// <param name="reference">Generated source descriptor.</param>
        /// <param name="root">Destination prefab root.</param>
        /// <param name="candidates">Existing destination interactions captured once for the import.</param>
        /// <returns>The uniquely resolved interaction, or null if missing or ambiguous.</returns>
        internal static ObjectInteraction Resolve(InteractionTemplateReference reference, Transform root, ObjectInteraction[] candidates)
        {
            // Never guess among different cards merely because they share an interaction type.
            if (reference == null || !reference.Assigned)
                return null;
            List<ObjectInteraction> typed = new List<ObjectInteraction>();
            List<ObjectInteraction> branch = new List<ObjectInteraction>();
            foreach (ObjectInteraction candidate in candidates)
                if (candidate is not ObjectInteractionUnlock && Kind(candidate) == reference.Kind)
                {
                    typed.Add(candidate);
                    if (AnimationUtility.CalculateTransformPath(candidate.transform, root) == reference.Path)
                        branch.Add(candidate);
                }
            ObjectInteraction named = UniqueName(branch, reference.Name);
            if (named != null)
                return named;
            if (branch.Count == 1)
                return branch[0];
            if (branch.Count > 1 && reference.Occurrence >= 0 && reference.Occurrence < branch.Count
                && branch.TrueForAll(candidate => candidate.transform == branch[0].transform)
                && branch[reference.Occurrence].InteractionName == reference.Name)
                return branch[reference.Occurrence];
            named = UniqueName(typed, reference.Name);
            return named != null ? named : typed.Count == 1 ? typed[0] : null;
        }

        /// <summary>Finds an exact name only when it identifies a single compatible component.</summary>
        /// <param name="candidates">Compatible destination components.</param>
        /// <param name="name">Captured source interaction name.</param>
        /// <returns>A unique name match or null.</returns>
        private static ObjectInteraction UniqueName(List<ObjectInteraction> candidates, string name)
        {
            // Empty or duplicate names cannot override structural disambiguation.
            ObjectInteraction selected = null;
            foreach (ObjectInteraction candidate in candidates)
                if (candidate.InteractionName == name)
                {
                    if (selected != null)
                        return null;
                    selected = candidate;
                }
            return selected;
        }

        /// <summary>Maps supported components explicitly so presets never depend on runtime reflection.</summary>
        /// <param name="feature">Existing ordinary interaction.</param>
        /// <returns>The stable serialized interaction kind.</returns>
        private static InteractionTemplateKind Kind(ObjectInteraction feature)
        {
            // Rules cannot reference other rules, matching the existing unlock validation contract.
            return feature switch
            {
                ObjectHover => InteractionTemplateKind.Hover,
                ObjectGrab => InteractionTemplateKind.Grab,
                ObjectDrop => InteractionTemplateKind.Drop,
                ObjectThrow => InteractionTemplateKind.Throw,
                ObjectDispenser => InteractionTemplateKind.Dispenser,
                ObjectContainer => InteractionTemplateKind.Container,
                ObjectDialogue => InteractionTemplateKind.Dialogue,
                ObjectSlice => InteractionTemplateKind.Slice,
                ObjectContactModifier => InteractionTemplateKind.Contact,
                ObjectAvailableOrders => InteractionTemplateKind.AvailableOrders,
                ObjectDegradation => InteractionTemplateKind.ObjectDegradation,
                ObjectGravityGenerator => InteractionTemplateKind.GravityGenerator,
                ObjectElasticDeformation => InteractionTemplateKind.ElasticDeformation,
                ObjectDirtTrail => InteractionTemplateKind.DirtTrail,
                ObjectSpraySauce => InteractionTemplateKind.SpraySauce,
                ObjectTriggerAnimation => InteractionTemplateKind.TriggerAnimation,
                ObjectEject => InteractionTemplateKind.Eject,
                ObjectAmbient => InteractionTemplateKind.Ambient,
                ObjectOutline => InteractionTemplateKind.Outline,
                ObjectSpawnManager => InteractionTemplateKind.Spawn,
                ObjectAssemblyStation => InteractionTemplateKind.AssemblyStation,
                ObjectAssemblyProduct => InteractionTemplateKind.AssemblyProduct,
                _ => throw new ArgumentException("Unsupported interaction mapping.", nameof(feature))
            };
        }

        #endregion

        #endregion
    }
}
