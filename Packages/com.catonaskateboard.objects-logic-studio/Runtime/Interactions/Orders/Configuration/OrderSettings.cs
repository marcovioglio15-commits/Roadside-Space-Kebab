using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Associates one selected consuming contact interaction with one board entry.</summary>
    [Serializable]
    public sealed class OrderEntry
    {
        #region Fields

        [Tooltip("Modify By Contact on this same object. Each completion fulfils its first unfinished order in the list; several orders may share this source.")]
        public ObjectContactModifier Source;
        [Tooltip("Order text shown until the object despawns. Completion adds strikethrough without freeing its slot.")]
        [TextArea(2, 5)]
        public string Text = "Order";

        #endregion
    }

    /// <summary>Routes selected local consumption interactions to a shared scene order board.</summary>
    [Serializable]
    public sealed class OrderSettings
    {
        #region Fields

        [Header("Orders")]
        [Tooltip("ID of the scene Order Board receiving this object's entries. Multiple customers use the same ID.")]
        public string Board = "Orders";
        [Tooltip("Independent orders in queue order. Reuse a consuming interaction for multiple rows; each consumption completes only its first unfinished row.")]
        public OrderEntry[] Entries = Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks each order independently, allowing several rows to share the same consuming action.</summary>
        /// <param name="owner">Object owning this order interaction.</param>
        /// <param name="warning">Receives an incomplete row or missing consuming interaction.</param>
        /// <returns>True when every row has an eligible local source and nonempty text.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Empty or removed sources stay visible instead of being replaced by similarly named interactions.
            warning = "Select at least one enabled Modify By Contact with Consume on this object, a board ID and nonempty order text.";
            if (string.IsNullOrWhiteSpace(Board) || Entries == null || Entries.Length == 0)
                return false;
            foreach (OrderEntry entry in Entries)
                if (entry == null || !Eligible(entry.Source, owner) || string.IsNullOrWhiteSpace(entry.Text))
                    return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Identifies contact actions capable of committing the consumption represented by an order.</summary>
        /// <param name="source">Candidate interaction.</param>
        /// <param name="owner">Exact object containing the order interaction.</param>
        /// <returns>True for a local, enabled, permanent consuming action.</returns>
        public static bool Eligible(ObjectContactModifier source, GameObject owner)
        {
            // Temporary contact effects cannot consume participants and therefore cannot fulfil orders.
            return source != null && source.gameObject == owner && source.enabled && source.Settings != null
                && !source.Settings.WhileContact && (source.Settings.Self.Consume || source.Settings.Other.Consume);
        }

        #endregion
        #endregion
    }
}
