using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains a contact slot across deferred interaction edits and consumption of their trigger.</summary>
    internal readonly struct ScoopDisplayAnchor
    {
        #region Fields

        internal readonly ContactMagnetRun Source;
        internal readonly int Index;
        internal readonly Pose LocalPose;
        internal readonly ObjectFlag[] Ingredients;

        #endregion
        #region Methods
        #region Capture

        /// <summary>Stores the selected slot in the receiving item's local frame.</summary>
        /// <param name="source">Contact docking manager retaining this slot.</param>
        /// <param name="index">Selected contact magnet.</param>
        /// <param name="localPose">Pose relative to the dispenser's owning item.</param>
        /// <param name="ingredients">Recipe provenance captured before the loading item is consumed.</param>
        internal ScoopDisplayAnchor(ContactMagnetRun source, int index, Pose localPose, ObjectFlag[] ingredients)
        {
            Source = source;
            Index = index;
            LocalPose = localPose;
            Ingredients = ingredients;
        }

        /// <summary>Captures the trigger's actual slot before deferred edits release or consume it.</summary>
        /// <param name="owner">Item receiving the interaction changes.</param>
        /// <param name="trigger">Counterpart that caused those changes.</param>
        /// <returns>A retained contact anchor, or an empty value when no compatible setup exists.</returns>
        internal static ScoopDisplayAnchor Capture(ObjectItem owner, ObjectItem trigger)
        {
            // Prefer an existing lease before considering another compatible local slot.
            if (owner == null || trigger == null)
                return default;
            ObjectContactModifier[] modifiers = owner.GetComponents<ObjectContactModifier>();
            for (int pass = 0; pass < 2; pass++)
                foreach (ObjectContactModifier modifier in modifiers)
                    if (modifier.isActiveAndEnabled && modifier.Settings.MagnetLock.Enabled
                        && modifier.Magnets.TrySupplyAnchor(trigger, owner.transform, pass == 0, out ScoopDisplayAnchor anchor))
                        return anchor;
            return default;
        }

        /// <summary>Checks docking dependencies without requiring a live trigger in an editor draft.</summary>
        /// <param name="owner">Dispenser receiving a contact-driven supply.</param>
        /// <returns>True when an enabled local modifier defines valid contact magnets.</returns>
        internal static bool HasSetup(ObjectDispenser owner)
        {
            // Prefab contents may be inactive while their enabled settings remain available.
            foreach (ObjectContactModifier modifier in owner.GetComponents<ObjectContactModifier>())
                if (modifier.enabled && modifier.Settings.MagnetLock is { Enabled: true } magnets && magnets.TryValidate(out _))
                    return true;
            return false;
        }

        #endregion
        #endregion
    }
}
