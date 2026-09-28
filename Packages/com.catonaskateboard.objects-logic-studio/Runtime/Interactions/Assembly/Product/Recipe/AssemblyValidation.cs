using CatOnASkateboard.StudioIdentity;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks recipe capacity, magnet compatibility and existing product interaction references.</summary>
    public static class AssemblyValidation
    {
        #region Methods

        #region Recipe

        /// <summary>Validates an entire product recipe before ingredients can be accepted.</summary>
        /// <param name="owner">Product root containing its existing interactions.</param>
        /// <param name="settings">Recipe and product-local layout proposal.</param>
        /// <param name="warning">Receives an invalid quantity, impossible layout or missing interaction.</param>
        /// <returns>True when the recipe has compatible physical slots and attainable unit requirements.</returns>
        public static bool TryValidate(GameObject owner, AssemblyProductSettings settings, out string warning)
        {
            // Validate physical slots separately from unit quantities, which depend on each incoming Grab.
            warning = "Configure at least one ingredient and enough compatible magnets.";
            if (owner == null || settings == null || settings.Ingredients == null || settings.Ingredients.Length == 0
                || settings.Magnets == null || settings.Magnets.Length == 0 || settings.InteractionRules == null)
                return false;
            Dictionary<ObjectFlag, int> quantities = new Dictionary<ObjectFlag, int>();
            long total = 0;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
            {
                if (!ValidFlag( ingredient, out warning))
                    return false;
                if (!quantities.TryAdd(ingredient.Flag, ingredient.Count))
                {
                    warning = "Use each recipe flag once and set its quantity on that row.";
                    return false;
                }
                total += ingredient.Count;
            }
            int generic = 0;
            HashSet<ObjectFlag> specific = new HashSet<ObjectFlag>();
            HashSet<string> names = new HashSet<string>();
            HashSet<int> orders = new HashSet<int>();
            Dictionary<ObjectFlag, int> required = new Dictionary<ObjectFlag, int>();
            foreach (Transform child in owner.transform)
                if (!child.TryGetComponent(out ObjectAssemblyPart part) || part.Product == null || part.Product.gameObject != owner)
                    names.Add(child.name);
            foreach (AssemblyMagnet magnet in settings.Magnets)
            {
                if (magnet == null || string.IsNullOrWhiteSpace(magnet.Name) || magnet.Name.Contains('/') || !names.Add(magnet.Name))
                {
                    warning = "Give every magnet a unique name without slashes, distinct from the product's existing children.";
                    return false;
                }
                if (!ValidMagnet(magnet))
                {
                    warning = "Magnet positions and rotations must be finite, with positive finite scale on each axis.";
                    return false;
                }
                if (magnet.Order < 0 || magnet.Order > settings.Magnets.Length || magnet.Order > total
                    || magnet.Order > 0 && !orders.Add(magnet.Order))
                {
                    warning = "Magnet order must be zero or a unique insertion number within the slot and ingredient capacity.";
                    return false;
                }
                if (magnet.Appearance == null || !magnet.Appearance.TryValidate(out warning))
                    return false;
                if (magnet.AnyIngredient)
                    generic++;
                else if (magnet.Flag == null || !quantities.ContainsKey(magnet.Flag))
                {
                    warning = "Choose a recipe ingredient flag for each specific magnet.";
                    return false;
                }
                else
                {
                    specific.Add(magnet.Flag);
                    if (magnet.Order > 0)
                    {
                        required.TryGetValue(magnet.Flag, out int count);
                        if (count >= quantities[magnet.Flag])
                        {
                            warning = "Numbered magnets need at least one recipe unit each for their required flag.";
                            return false;
                        }
                        required[magnet.Flag] = count + 1;
                    }
                }
            }
            // One physical ingredient can supply multiple units; require a compatible slot for each flag.
            // Additional unit-one ingredients still need additional physical magnets at insertion.
            int remaining = 0;
            foreach (ObjectFlag flag in quantities.Keys)
                if (!specific.Contains(flag))
                    remaining++;
            if (remaining > generic)
            {
                warning = "The recipe needs more compatible magnets. Add generic slots or slots for its missing flags.";
                return false;
            }
            return settings.CompletedAppearance != null && settings.CompletedAppearance.TryValidate(out warning)
                && ValidateRules(owner, settings.InteractionRules, quantities, total, out warning);
        }

        /// <summary>Checks that a flag requirement uses a defined project flag and positive integer quantity.</summary>
        /// <param name="requirement">Proposed flag and quantity.</param>
        /// <param name="warning">Receives an invalid quantity or undefined flag.</param>
        /// <returns>True when the requirement is usable.</returns>
        private static bool ValidFlag(ItemFlagRequirement requirement, out string warning)
        {
            // A flag asset remains the same recipe key after renaming.
            warning = "Choose a flag asset and a positive whole-number quantity.";
            if (requirement == null || requirement.Flag == null || requirement.Count <= 0)
                return false;
            return requirement.Flag.TryValidate(out warning);
        }

        #endregion

        #region Availability

        /// <summary>Rejects ambiguous targets and ingredient thresholds that the recipe cannot satisfy.</summary>
        /// <param name="owner">Root of the assembled product.</param>
        /// <param name="rules">Per-feature ingredient requirements.</param>
        /// <param name="quantities">Maximum recipe quantity for each distinct flag.</param>
        /// <param name="total">Maximum allowed ingredient count.</param>
        /// <param name="warning">Receives a missing target or impossible threshold.</param>
        /// <returns>True when every rule has a unique existing target and attainable requirements.</returns>
        private static bool ValidateRules(GameObject owner, AssemblyInteractionRule[] rules, Dictionary<ObjectFlag, int> quantities,
            long total, out string warning)
        {
            // A feature has one assembly rule; independent Unlock Interactions may add their own restrictions.
            warning = string.Empty;
            HashSet<ObjectInteraction> targets = new HashSet<ObjectInteraction>();
            foreach (AssemblyInteractionRule rule in rules)
            {
                if (rule == null || rule.Target == null || !rule.Target.transform.IsChildOf(owner.transform)
                    || rule.Target is ObjectInteractionUnlock or ObjectAssemblyProduct || !targets.Add(rule.Target))
                    warning = "Select each existing product interaction only once; unlock rules cannot be ingredient targets.";
                else if (!rule.RequireComplete && (rule.MinimumIngredients <= 0 || rule.MinimumIngredients > total) || rule.Ingredients == null)
                    warning = "Choose a positive minimum ingredient count that fits this recipe.";
                else
                    foreach (ItemFlagRequirement requirement in rule.Ingredients)
                        if (!ValidFlag( requirement, out warning)
                            || !quantities.TryGetValue(requirement.Flag, out int count) || requirement.Count > count)
                        {
                            warning = "Product interaction requirements must use recipe flags and attainable positive quantities.";
                            break;
                        }
                if (warning.Length > 0)
                    return false;
            }
            return true;
        }

        /// <summary>Checks a magnet pose before physics insertion or editor preview rendering.</summary>
        /// <param name="magnet">Placement slot proposed by the tool.</param>
        /// <returns>True for finite position and rotation with positive finite scale.</returns>
        public static bool ValidMagnet(AssemblyMagnet magnet)
        {
            // Invalid input remains editable but never reaches a rendering or physics matrix.
            return magnet != null && InteractionValues.Finite(magnet.Position) && InteractionValues.Finite(magnet.Rotation)
                && InteractionValues.Positive(magnet.Scale.x) && InteractionValues.Positive(magnet.Scale.y)
                && InteractionValues.Positive(magnet.Scale.z);
        }

        #endregion

        #endregion
    }
}
