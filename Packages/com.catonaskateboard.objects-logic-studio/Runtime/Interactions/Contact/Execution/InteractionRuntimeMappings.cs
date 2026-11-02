using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves portable preset dependencies to the affected object's existing cards.</summary>
    internal static class InteractionRuntimeMappings
    {
        #region Methods

        #region Binding

        /// <summary>Maps preset-only component descriptors after copying instance-owned settings.</summary>
        /// <param name="feature">Destination runtime card.</param>
        /// <param name="preset">Immutable preset supplying portable descriptors.</param>
        /// <param name="warning">Receives an unresolved local dependency.</param>
        /// <returns>True when every active mapping resolves.</returns>
        internal static bool Apply(ObjectInteraction feature, ScriptableObject preset, out string warning)
        {
            warning = "A preset interaction mapping is missing on the affected object.";
            switch (feature)
            {
                case ObjectAvailableOrders orders when preset is AvailableOrdersPreset source:
                    if (orders.Settings.WaitForCompletion)
                    {
                        orders.Settings.CompletionSource = Resolve(feature.transform, source.Completion);
                        if (orders.Settings.CompletionSource == null)
                            return false;
                    }
                    break;
                case ObjectAssemblyProduct product when preset is AssemblyProductPreset source:
                    if (source.Targets == null || source.Targets.Length != product.Settings.InteractionRules.Length)
                        return false;
                    for (int index = 0; index < source.Targets.Length; index++)
                    {
                        product.Settings.InteractionRules[index].Target = Resolve(feature.transform, source.Targets[index]);
                        if (product.Settings.InteractionRules[index].Target == null)
                            return false;
                    }
                    break;
                case ObjectInteractionUnlock unlock when preset is UnlockRulePreset source:
                    unlock.Settings.Target = Resolve(feature.transform, source.Rule.Target);
                    unlock.Settings.Replacement = Resolve(feature.transform, source.Rule.Replacement);
                    if (unlock.Settings.Target == null || unlock.Settings.Operation == InteractionAvailabilityChange.Replace && unlock.Settings.Replacement == null
                        || source.Rule.Sources == null || source.Rule.Sources.Length != unlock.Settings.Conditions.Length)
                        return false;
                    for (int index = 0; index < source.Rule.Sources.Length; index++)
                        if (unlock.Settings.Conditions[index].Trigger == UnlockTrigger.Interaction)
                        {
                            unlock.Settings.Conditions[index].Source = Resolve(feature.transform, source.Rule.Sources[index]);
                            if (unlock.Settings.Conditions[index].Source == null)
                                return false;
                        }
                    InteractionRuntimeCopy.Into(source.Rule.ToolRequirement, unlock.ToolRequirement);
                    break;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Finds one captured interaction without reflection or prefab-stage references.</summary>
        /// <param name="root">Affected hierarchy root.</param>
        /// <param name="reference">Portable preset descriptor.</param>
        /// <returns>The exact matching local card, or null.</returns>
        private static ObjectInteraction Resolve(Transform root, InteractionTemplateReference reference)
        {
            if (reference == null || !reference.Assigned)
                return null;
            Transform branch = string.IsNullOrEmpty(reference.Path) || reference.Path == "." ? root : root.Find(reference.Path);
            if (branch == null)
                return null;
            int occurrence = 0;
            foreach (ObjectInteraction candidate in branch.GetComponents<ObjectInteraction>())
                if (InteractionTemplateKinds.Of(candidate) == reference.Kind && candidate.InteractionName == reference.Name
                    && occurrence++ == reference.Occurrence)
                    return candidate;
            return null;
        }

        #endregion

        #endregion
    }
}
