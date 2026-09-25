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
            Dictionary<string, int> quantities = new Dictionary<string, int>();
            long total = 0;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
            {
                if (!ValidTag(owner, ingredient, out warning))
                    return false;
                if (!quantities.TryAdd(ingredient.Tag, ingredient.Count))
                {
                    warning = "Use each recipe tag once and set its quantity on that row.";
                    return false;
                }
                total += ingredient.Count;
            }
            int generic = 0;
            HashSet<string> specific = new HashSet<string>();
            HashSet<string> names = new HashSet<string>();
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
                if (magnet.AnyIngredient)
                    generic++;
                else if (!quantities.ContainsKey(magnet.Tag))
                {
                    warning = "Choose a recipe ingredient tag for each specific magnet.";
                    return false;
                }
                else
                    specific.Add(magnet.Tag);
            }
            // One physical ingredient can supply multiple units; require a compatible slot for each tag.
            // Additional unit-one ingredients still need additional physical magnets at insertion.
            int remaining = 0;
            foreach (string tag in quantities.Keys)
                if (!specific.Contains(tag))
                    remaining++;
            if (remaining > generic)
            {
                warning = "The recipe needs more compatible magnets. Add generic slots or slots for its missing tags.";
                return false;
            }
            return ValidateRules(owner, settings.InteractionRules, quantities, total, out warning);
        }

        /// <summary>Checks that a tag requirement uses a defined project tag and positive integer quantity.</summary>
        /// <param name="owner">Object used for native tag validation.</param>
        /// <param name="requirement">Proposed tag and quantity.</param>
        /// <param name="warning">Receives an invalid quantity or undefined tag.</param>
        /// <returns>True when the requirement is usable.</returns>
        private static bool ValidTag(GameObject owner, ItemTagRequirement requirement, out string warning)
        {
            // CompareTag validates the catalog without assigning a tag or rewriting user input.
            warning = "Choose a project tag and a positive whole-number quantity.";
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.Tag) || requirement.Count <= 0)
                return false;
            try
            {
                owner.CompareTag(requirement.Tag);
            }
            catch (UnityException)
            {
                warning = "An ingredient tag is not defined in this project.";
                return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #region Availability

        /// <summary>Rejects ambiguous targets and ingredient thresholds that the recipe cannot satisfy.</summary>
        /// <param name="owner">Root of the assembled product.</param>
        /// <param name="rules">Per-feature ingredient requirements.</param>
        /// <param name="quantities">Maximum recipe quantity for each distinct tag.</param>
        /// <param name="total">Maximum allowed ingredient count.</param>
        /// <param name="warning">Receives a missing target or impossible threshold.</param>
        /// <returns>True when every rule has a unique existing target and attainable requirements.</returns>
        private static bool ValidateRules(GameObject owner, AssemblyInteractionRule[] rules, Dictionary<string, int> quantities,
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
                    foreach (ItemTagRequirement requirement in rule.Ingredients)
                        if (!ValidTag(owner, requirement, out warning)
                            || !quantities.TryGetValue(requirement.Tag, out int count) || requirement.Count > count)
                        {
                            warning = "Product interaction requirements must use recipe tags and attainable positive quantities.";
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
