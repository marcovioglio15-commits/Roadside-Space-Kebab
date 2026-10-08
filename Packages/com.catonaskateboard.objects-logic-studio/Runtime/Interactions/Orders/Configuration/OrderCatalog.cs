using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares named order candidates between objects while leaving extraction settings on each interaction.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Order Catalog")]
    public sealed class OrderCatalog : ScriptableObject
    {
        #region Fields

        [Header("Available Orders")]
        [Tooltip("Named candidates shared by every interaction referencing this catalog. Quantities occupy individual board slots.")]
        public OrderEntry[] Entries = Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Reports invalid candidates without changing authored names, quantities or weights.</summary>
        /// <param name="warning">Receives the first invalid candidate.</param>
        /// <returns>True when all candidates can be used for uniform or weighted selection.</returns>
        public bool TryValidate(out string warning)
        {
            // Names are stable keys for Consumption dialogue filters on all catalog consumers.
            warning = "Add at least one available order.";
            if (Entries == null || Entries.Length == 0)
                return false;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Entries.Length; index++)
            {
                OrderEntry entry = Entries[index];
                warning = $"Order {index + 1} needs a positive quantity and complete settings.";
                if (entry == null || entry.Quantity <= 0
                    || !OrderRecipeVariant.ValidateMetadata(entry.Name, entry.Text, entry.Weight, names, out warning))
                    return false;
                if (!ObjectFlagRules.TryValidate(entry.Flags, false, out warning))
                {
                    warning = $"Order {index + 1}, Identity Flags: {warning}";
                    return false;
                }
                if (!entry.UseRecipeModifiers)
                    continue;
                warning = $"Order '{entry.Name}' needs at least one recipe modifier variant, or disable Recipe Modifiers.";
                if (entry.Variants == null || entry.Variants.Length == 0)
                    return false;
                foreach (OrderRecipeVariant variant in entry.Variants)
                {
                    warning = $"Order '{entry.Name}' contains an empty recipe variant.";
                    if (variant == null || !OrderRecipeVariant.ValidateMetadata(variant.Name, variant.Text, variant.Weight, names, out warning)
                        || !ObjectFlagRules.TryValidate(variant.Flags, false, out warning))
                        return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Enumerates each enabled base order and recipe variant as an independent draw candidate.</summary>
        /// <returns>Enabled candidates sharing their parent identity and quantity.</returns>
        internal IEnumerable<OrderCandidate> Candidates()
        {
            if (Entries == null)
                yield break;
            foreach (OrderEntry entry in Entries)
            {
                if (entry == null)
                    continue;
                if (!entry.UseRecipeModifiers || entry.IncludeBaseOrder)
                    yield return new OrderCandidate(entry);
                if (entry.UseRecipeModifiers && entry.Variants != null)
                    foreach (OrderRecipeVariant variant in entry.Variants)
                        if (variant != null)
                            yield return new OrderCandidate(entry, variant);
            }
        }

        /// <summary>Checks names against enabled candidates without conflating a base order and its recipe variants.</summary>
        /// <param name="name">Exact name saved by a dialogue filter.</param>
        /// <returns>True when the named candidate can be extracted.</returns>
        public bool ContainsOrder(string name)
        {
            foreach (OrderCandidate candidate in Candidates())
                if (candidate.Name == name)
                    return true;
            return false;
        }

        /// <summary>Supplies independent base and variant names to editor selectors.</summary>
        /// <returns>Names of enabled draw candidates in authored order.</returns>
        public IEnumerable<string> OrderNames()
        {
            foreach (OrderCandidate candidate in Candidates())
                yield return candidate.Name;
        }

        #endregion
        #endregion
    }
}
