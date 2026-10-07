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
                warning = $"Order {index + 1} needs a unique name, text, positive quantity and a finite, nonnegative weight.";
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || !names.Add(entry.Name)
                    || string.IsNullOrWhiteSpace(entry.Text) || entry.Quantity <= 0
                    || !InteractionValues.Finite(entry.Weight) || entry.Weight < 0f)
                    return false;
                if (!ObjectFlagRules.TryValidate(entry.Flags, false, out warning))
                {
                    warning = $"Order {index + 1}, Identity Flags: {warning}";
                    return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
