using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses cumulative order progress, surplus units or the latest delivery's open-slot match.</summary>
    public enum DialogueOrderResult { Completed, UnexpectedDelivery, NotPendingOrder, PendingOrder }

    /// <summary>Shares consumption eligibility between a complete dialogue and its individual entries.</summary>
    [Serializable]
    public sealed class DialogueConsumptionFilter
    {
        #region Fields

        [Header("Consumption Filter")]
        [Tooltip("Restrict this dialogue or entry using consumption history.")]
        public bool Enabled;
        [Tooltip("Consumed flag quantities used only on objects without Available Orders.")]
        public ItemFlagRequirement[] RequiredFlags = Array.Empty<ItemFlagRequirement>();
        [Tooltip("Available order names used on this object. Only orders drawn for the current spawn participate.")]
        public string[] Orders = Array.Empty<string>();
        [Tooltip("Require every selected active order or flag requirement. Otherwise any one match is sufficient.")]
        public bool RequireAll = true;
        [Tooltip("Completed waits for every extracted slot to be consumed. Unexpected Delivery checks surplus units. Pending Order accepts a delivery matching an open slot before consumption; Not Pending Order accepts only deliveries matching none. Only Unexpected Delivery uses selected order names.")]
        public DialogueOrderResult Result;

        /// <summary>Whether this filter classifies the latest receipt against all open slots instead of named orders.</summary>
        public bool UsesPendingDelivery => Result is DialogueOrderResult.NotPendingOrder or DialogueOrderResult.PendingOrder;

        #endregion
        #region Methods
        #region Eligibility

        /// <summary>Evaluates the enabled filter against the appropriate source of consumption progress.</summary>
        /// <param name="item">Item retaining ordinary consumption receipts.</param>
        /// <param name="orders">Optional catalog with this spawn's actual ticket progress.</param>
        /// <returns>True when the filter is disabled or its selected conditions are satisfied.</returns>
        internal bool Matches(ObjectItem item, ObjectAvailableOrders orders)
        {
            if (!Enabled)
                return true;
            // Ticket fulfilment runs before dialogue polling; its receipt snapshot retains the pre-fulfilment classification.
            if (UsesPendingDelivery)
                return orders != null && orders.TryLatestDelivery(out bool pending) && pending == (Result == DialogueOrderResult.PendingOrder);
            if (orders != null && Result == DialogueOrderResult.Completed)
                return orders.AllCompleted;
            if (orders == null)
            {
                // Each receipt is counted once even when it carries several accepted flags.
                foreach (ItemFlagRequirement requirement in RequiredFlags)
                    if ((item.CountConsumed(requirement.Flags) >= requirement.Count) != RequireAll)
                        return !RequireAll;
                return RequireAll && RequiredFlags.Length > 0;
            }
            bool active = false;
            foreach (string name in Orders)
            {
                // Undrawn catalog entries neither satisfy nor block a spawn-specific filter.
                if (!orders.TryProgress(name, out bool complete))
                    continue;
                active = true;
                bool match = Result == DialogueOrderResult.UnexpectedDelivery ? orders.UnexpectedConsumption : complete;
                if (match != RequireAll)
                    return !RequireAll;
            }
            return active && RequireAll;
        }

        #endregion
        #region Validation

        /// <summary>Checks only the filter branch used by the current object or standalone preset.</summary>
        /// <param name="catalog">Local orders, or null for flag-based objects and unbound presets.</param>
        /// <param name="warning">Receives missing selections or invalid quantities.</param>
        /// <param name="unbound">Allows portable presets to retain order names before a destination is selected.</param>
        /// <returns>True when the enabled filter is complete.</returns>
        public bool TryValidate(OrderSettings catalog, out string warning, bool unbound = true)
        {
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (UsesPendingDelivery)
            {
                if (catalog != null || unbound)
                    return true;
                warning = "Pending Order and Not Pending Order require Available Orders on this object.";
                return false;
            }
            // Order completion is global; flag receipts remain available on objects without orders.
            if (Result == DialogueOrderResult.Completed && (catalog != null || unbound && (RequiredFlags == null || RequiredFlags.Length == 0)))
                return true;
            if (catalog != null || unbound && Orders != null && Orders.Length > 0)
            {
                warning = "Select at least one available order and a supported consumption result.";
                if (Orders == null || Orders.Length == 0 || Result is not (DialogueOrderResult.Completed or DialogueOrderResult.UnexpectedDelivery))
                    return false;
                foreach (string name in Orders)
                    if (string.IsNullOrWhiteSpace(name) || catalog != null && !catalog.ContainsOrder(name))
                    {
                        warning = "An order selected by the dialogue is missing from Available Orders: " + name;
                        return false;
                    }
            }
            else
            {
                warning = "Add at least one consumed-flag requirement or disable this filter.";
                if (RequiredFlags == null || RequiredFlags.Length == 0)
                    return false;
                foreach (ItemFlagRequirement requirement in RequiredFlags)
                    if (requirement == null || !requirement.TryValidate(out warning))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
