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
            // Completed recipes retire ingredient colliders, so carry geometry must exist on the product itself.
            if (owner.TryGetComponent(out ObjectGrab _) && !ObjectGrab.ValidateBody(owner, out warning))
            {
                warning = "Product Physics: " + warning;
                return false;
            }
            Dictionary<ObjectFlag, int> quantities = new Dictionary<ObjectFlag, int>();
            long total = 0;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
            {
                if (ingredient == null || !ingredient.TryValidate(out warning))
                    return false;
                foreach (ObjectFlag flag in ingredient.Flags)
                    if (!quantities.TryAdd(flag, ingredient.Count))
                    {
                        warning = "Use each flag in only one recipe row; alternatives on a row share its quantity.";
                        return false;
                    }
                total += ingredient.Count;
            }
            HashSet<string> names = new HashSet<string>();
            HashSet<int> orders = new HashSet<int>();
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
                if (!magnet.AnyIngredient)
                {
                    if (!ObjectFlagRules.TryValidate(magnet.Flags, false, out warning))
                        return false;
                    foreach (ObjectFlag flag in magnet.Flags)
                        if (!quantities.ContainsKey(flag))
                        {
                            warning = "Choose recipe flags for each restricted magnet.";
                            return false;
                        }
                }
            }
            if (!new AssemblyCapacity().CanFit(settings, null))
            {
                warning = "Add compatible magnets or quantity capacity for all mandatory ingredients and numbered slots.";
                return false;
            }
            // Final appearance belongs to this prefab, so invalid targets must fail before Apply or spawning.
            if (settings.CompletedAppearance == null)
            {
                warning = "Configure the completed appearance lists.";
                return false;
            }
            if (!settings.CompletedAppearance.TryValidate(out warning)
                || !settings.CompletedAppearance.TryValidateBindings(owner.GetComponent<ObjectItem>(), out warning))
            {
                warning = "Completed Appearance: " + warning;
                return false;
            }
            return ValidateRules(owner, settings, quantities, total, out warning);
        }

        #endregion

        #region Availability

        /// <summary>Rejects ambiguous targets and ingredient thresholds that the recipe cannot satisfy.</summary>
        /// <param name="owner">Root of the assembled product.</param>
        /// <param name="settings">Recipe and per-feature ingredient requirements.</param>
        /// <param name="quantities">Maximum recipe quantity for each distinct flag.</param>
        /// <param name="total">Maximum allowed ingredient count.</param>
        /// <param name="warning">Receives a missing target or impossible threshold.</param>
        /// <returns>True when every rule has a unique existing target and attainable requirements.</returns>
        private static bool ValidateRules(GameObject owner, AssemblyProductSettings settings, Dictionary<ObjectFlag, int> quantities,
            long total, out string warning)
        {
            // A feature has one assembly rule; independent Unlock Interactions may add their own restrictions.
            warning = string.Empty;
            HashSet<ObjectInteraction> targets = new HashSet<ObjectInteraction>();
            foreach (AssemblyInteractionRule rule in settings.InteractionRules)
            {
                if (rule == null || rule.Target == null || !rule.Target.transform.IsChildOf(owner.transform)
                    || rule.Target is ObjectInteractionUnlock or ObjectAssemblyProduct || !targets.Add(rule.Target))
                    warning = "Select each existing product interaction only once; unlock rules cannot be ingredient targets.";
                else if (!rule.RequireComplete && (rule.MinimumIngredients <= 0 || rule.MinimumIngredients > total) || rule.Ingredients == null)
                    warning = "Choose a positive minimum ingredient count that fits this recipe.";
                else
                    foreach (ItemFlagRequirement requirement in rule.Ingredients)
                    {
                        if (requirement == null || !requirement.TryValidate(out warning))
                            return false;
                        foreach (ObjectFlag flag in requirement.Flags)
                            if (!quantities.ContainsKey(flag))
                            {
                                warning = "Product interaction conditions must use recipe flags.";
                                return false;
                            }
                        long capacity = 0;
                        foreach (AssemblyIngredient ingredient in settings.Ingredients)
                            foreach (ObjectFlag flag in requirement.Flags)
                                if (System.Array.IndexOf(ingredient.Flags, flag) >= 0)
                                {
                                    capacity += ingredient.Count;
                                    break;
                                }
                        if (requirement.Count > capacity)
                        {
                            warning = "The required quantity exceeds the matching recipe capacity.";
                            return false;
                        }
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
