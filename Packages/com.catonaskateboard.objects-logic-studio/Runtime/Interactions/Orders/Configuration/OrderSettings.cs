using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Associates one selected consuming contact interaction with one board entry.</summary>
    [Serializable]
    public sealed class OrderEntry
    {
        #region Fields

        [Header("Order")]
        [Tooltip("Modify By Contact on this same object. Each consumption fulfils its first matching unfinished order in the list; several orders may share this source.")]
        public ObjectContactModifier Source;
        [Tooltip("Order text shown until the object despawns. Completion adds strikethrough without freeing its slot.")]
        [TextArea(2, 5)]
        public string Text = "Order";
        [Header("Consumption")]
        [Tooltip("Complete this order only when the consumed item had one of the selected identity flags. Otherwise any consumption by the selected action qualifies.")]
        public bool FilterConsumed;
        [Tooltip("Alternative flags accepted at consumption time. One matching flag is enough; later identity changes do not affect this order's completion.")]
        public ObjectFlag[] ConsumedFlags = Array.Empty<ObjectFlag>();

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
        [Tooltip("Independent orders in queue order. Reuse a consuming interaction for multiple rows; each consumption completes only its first matching unfinished row.")]
        public OrderEntry[] Entries = Array.Empty<OrderEntry>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks each order independently, allowing several rows to share the same consuming action.</summary>
        /// <param name="owner">Object owning this order interaction.</param>
        /// <param name="warning">Receives an incomplete row, missing source or invalid consumption filter.</param>
        /// <returns>True when every row has an eligible source, text and valid enabled filter.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Empty or removed sources stay visible instead of being replaced by similarly named interactions.
            warning = "Select at least one enabled Modify By Contact with Consume on this object, a board ID and nonempty order text.";
            if (string.IsNullOrWhiteSpace(Board) || Entries == null || Entries.Length == 0)
                return false;
            // Disabled filters retain their authored selection without affecting validation or completion.
            for (int index = 0; index < Entries.Length; index++)
            {
                OrderEntry entry = Entries[index];
                if (entry == null || !Eligible(entry.Source, owner) || string.IsNullOrWhiteSpace(entry.Text))
                {
                    warning = $"Order {index + 1} needs an enabled local Modify By Contact with Consume and nonempty text.";
                    return false;
                }
                if (entry.FilterConsumed && !ObjectFlagRules.TryValidate(entry.ConsumedFlags, false, out warning))
                {
                    warning = $"Order {index + 1}, Consumed Flags: {warning}";
                    return false;
                }
            }
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
