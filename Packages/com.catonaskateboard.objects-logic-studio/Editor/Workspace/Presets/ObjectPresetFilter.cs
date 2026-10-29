using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Limits the library to the interaction types visible in the adjacent catalog.</summary>
    internal static class ObjectPresetFilter
    {
        #region Methods

        #region Matching

        /// <summary>Checks direct presets and the interaction branches of a complete master.</summary>
        /// <param name="preset">Reusable project asset.</param>
        /// <param name="visible">Components remaining after all catalog filters.</param>
        /// <returns>True when the asset can configure a visible interaction type.</returns>
        internal static bool Matches(UnityEngine.ScriptableObject preset, IReadOnlyList<ObjectInteraction> visible)
        {
            if (preset is ObjectMasterPreset master)
            {
                foreach (ObjectMasterPreset.InteractionEntry entry in master.Interactions)
                    if (entry.Preset != null && Matches(entry.Preset, visible))
                        return true;
                return false;
            }
            foreach (ObjectInteraction feature in visible)
                if (Matches(preset, feature))
                    return true;
            return false;
        }

        /// <summary>Compares typed preset metadata without loading a destination object.</summary>
        /// <param name="preset">Main, group or specialized preset.</param>
        /// <param name="feature">Visible interaction.</param>
        /// <returns>True when the preset belongs to this type.</returns>
        private static bool Matches(UnityEngine.ScriptableObject preset, ObjectInteraction feature)
        {
            return preset switch
            {
                ObjectInteractionPreset main => main.Configuration != null && !string.IsNullOrEmpty(main.Configuration.TypeName)
                    && Type.GetType(main.Configuration.TypeName, false) == feature.GetType(),
                ObjectGroupPreset group => group.Group != null && group.Group.TypeName == feature.GetType().FullName,
                HoverPreset => feature is ObjectHover,
                SingleInteractionPreset single => feature is ObjectSingleInteraction target && single.Kind == target.Kind,
                ExtendedInteractionPreset extended => feature is ObjectExtendedInteraction target && extended.Kind == target.Kind,
                DialogueEntry => feature is ObjectDialogue,
                OrderCatalog => feature is ObjectAvailableOrders,
                UnlockRuleSetPreset => feature is ObjectInteractionUnlock,
                _ => false
            };
        }

        #endregion

        #endregion
    }
}
