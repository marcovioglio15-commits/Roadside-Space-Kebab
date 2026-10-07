using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses uniform or weighted sampling without replacement.</summary>
    public enum OrderSelection { UniformRandom, WeightedRandom }

    /// <summary>Defines the available order catalog and its per-spawn unit budget.</summary>
    [Serializable]
    public sealed class OrderSettings
    {
        #region Fields

        [Header("Orders")]
        [Tooltip("ID of the scene Order Board receiving this object's entries. Multiple customers use the same ID.")]
        public string Board = "Orders";
        [Tooltip("Maximum individual orders drawn at spawn, additionally limited by the destination board's authored slot count.")]
        public int DrawCount = 1;
        [Tooltip("Draw candidates uniformly or by their relative weights, without replacement. Candidates exceeding the remaining unit budget are excluded before each draw.")]
        public OrderSelection Selection;
        [Tooltip("Available named orders. Each selected candidate contributes Quantity separate tickets and remains active until despawn.")]
        public OrderCatalog Catalog;

        /// <summary>Shared candidates read at the beginning of each spawn.</summary>
        public OrderEntry[] Entries => Catalog != null ? Catalog.Entries : Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the catalog and the single local contact interaction used to fulfil its tickets.</summary>
        /// <param name="owner">Object owning this order interaction.</param>
        /// <param name="warning">Receives an invalid catalog or missing consuming interaction.</param>
        /// <returns>True when the object can select orders and receive consumed items.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Destination-specific validation runs again when applying or spawning an object.
            warning = "Available Orders needs exactly one local, enabled Modify By Contact with permanent Consume Other.";
            return owner != null && owner.GetComponents<ObjectContactModifier>().Length == 1
                && Eligible(owner.GetComponent<ObjectContactModifier>(), owner) && TryValidate(out warning);
        }

        /// <summary>Checks a reusable configuration without requiring a destination object.</summary>
        /// <param name="warning">Receives the first invalid catalog or extraction setting.</param>
        /// <returns>True when the configuration contains at least one selectable candidate.</returns>
        public bool TryValidate(out string warning)
        {
            // The board's actual slot count is evaluated at spawn, never written back into this asset.
            warning = "Choose an order catalog, a board ID and a positive draw count.";
            if (Catalog == null || string.IsNullOrWhiteSpace(Board) || DrawCount <= 0)
                return false;
            if (!Catalog.TryValidate(out warning))
                return false;
            warning = "Choose a supported order selection mode.";
            if (Selection is not (OrderSelection.UniformRandom or OrderSelection.WeightedRandom))
                return false;
            foreach (OrderEntry entry in Entries)
                if (entry.Quantity <= DrawCount && (Selection != OrderSelection.WeightedRandom || entry.Weight > 0f))
                {
                    warning = string.Empty;
                    return true;
                }
            warning = "At least one selectable order must fit the draw count.";
            return false;
        }

        /// <summary>Identifies contact actions capable of committing the consumption represented by an order.</summary>
        /// <param name="source">Candidate interaction.</param>
        /// <param name="owner">Exact object containing the order interaction.</param>
        /// <returns>True for a local, enabled, permanent consuming action.</returns>
        public static bool Eligible(ObjectContactModifier source, GameObject owner)
        {
            // Temporary contact effects cannot consume participants and therefore cannot fulfil orders.
            return source != null && source.gameObject == owner && source.enabled && source.Settings != null
                && source.Settings.ConsumesOther;
        }

        #endregion
        #endregion
    }
}
