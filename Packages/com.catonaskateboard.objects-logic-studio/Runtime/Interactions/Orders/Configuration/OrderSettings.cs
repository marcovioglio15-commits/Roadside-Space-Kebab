using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Associates one selected consuming contact interaction with one board entry.</summary>
    [Serializable]
    public sealed class OrderEntry
    {
        #region Fields

        [Tooltip("Modify By Contact on this same object whose successful consumption completes the order.")]
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
        [Tooltip("Selected consuming contact interactions and their independent board text, in queue order.")]
        public OrderEntry[] Entries = Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks exact component references so duplicate names cannot complete the wrong order.</summary>
        /// <param name="owner">Object owning this order interaction.</param>
        /// <param name="warning">Receives an incomplete row or missing consuming interaction.</param>
        /// <returns>True when every selected row is eligible and unique.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Empty or removed sources stay visible instead of being replaced by similarly named interactions.
            warning = "Select at least one enabled Modify By Contact with Consume on this object, a board ID and nonempty order text.";
            if (string.IsNullOrWhiteSpace(Board) || Entries == null || Entries.Length == 0)
                return false;
            HashSet<ObjectContactModifier> sources = new HashSet<ObjectContactModifier>();
            foreach (OrderEntry entry in Entries)
                if (entry == null || !Eligible(entry.Source, owner) || string.IsNullOrWhiteSpace(entry.Text) || !sources.Add(entry.Source))
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
