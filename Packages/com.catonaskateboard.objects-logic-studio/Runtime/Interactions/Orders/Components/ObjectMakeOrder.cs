using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Publishes selected consumption orders and retains completed rows until its customer disappears.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Make an Order")]
    public sealed class ObjectMakeOrder : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Order Requests")]
        [Tooltip("Shared scene board and selected consuming interactions on this same object.")]
        [SerializeField]
        private OrderSettings settings = new OrderSettings();
        private OrderTicket[] tickets = Array.Empty<OrderTicket>();
        private bool registered;
        private bool ready;
        private bool presentationHeld;

        #endregion
        #region Properties

        /// <summary>Selected consumption sources and their authored order text.</summary>
        public OrderSettings Settings => settings;
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.MakeOrder;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Subscribes before consuming interactions can publish their completion.</summary>
        private void OnEnable()
        {
            // Animation suspension may reactivate the same instance without creating new tickets.
            Initialize();
        }

        /// <summary>Validates once and queues orders as soon as this passive interaction is available.</summary>
        internal void Initialize()
        {
            // Exact event-source references disambiguate multiple consuming actions on one customer.
            ObjectInteraction.Signaled -= Observe;
            ObjectInteraction.Signaled += Observe;
            if (registered)
                return;
            if (!presentationHeld)
            {
                ready = TryValidate(out string warning);
                if (!ready)
                    Debug.LogWarning(warning, this);
            }
            Publish();
        }

        /// <summary>Waits for an initial availability lock without scanning boards or text slots.</summary>
        private void Update()
        {
            // Once published, completed and locked orders retain their slots until this object leaves.
            if (!registered && ready)
                Publish();
        }

        /// <summary>Queues one ticket per order row, including rows sharing a consuming action.</summary>
        private void Publish()
        {
            // Day Flow presentation is allowed during walk paths while gameplay physics is suspended.
            if (registered || !ready || !presentationHeld && !Available(InteractionChannels.Passive))
                return;
            registered = true;
            tickets = new OrderTicket[settings.Entries.Length];
            for (int index = 0; index < tickets.Length; index++)
            {
                tickets[index] = new OrderTicket(this, settings.Entries[index], settings.Board);
                OrderQueue.Add(tickets[index]);
            }
            Signal(InteractionMoment.Started);
        }

        /// <summary>Distinguishes an animation's temporary deactivation from actual customer despawn.</summary>
        /// <param name="hold">Whether the current flow transition retains board ownership.</param>
        internal void HoldPresentation(bool hold)
        {
            // Walk-out keeps crossed-out entries visible until the customer is actually destroyed.
            if (hold && !ready)
                ready = TryValidate(out _);
            presentationHeld = hold;
        }

        /// <summary>Releases slots on pooling or deactivation, except during explicit flow suspension.</summary>
        private void OnDisable()
        {
            // Destroy always releases below, even when a walk path is interrupted.
            if (!presentationHeld)
                Release();
        }

        /// <summary>Unconditionally removes a destroyed customer's visible and waiting orders.</summary>
        private void OnDestroy()
        {
            // External destruction during a walk path must not strand board slots.
            Release();
        }

        /// <summary>Removes queue membership and event callbacks once.</summary>
        private void Release()
        {
            // Empty tickets cover invalid, unpublished and already released interactions.
            ObjectInteraction.Signaled -= Observe;
            if (registered)
                OrderQueue.Remove(this);
            registered = false;
            tickets = Array.Empty<OrderTicket>();
        }

        #endregion
        #region Completion

        /// <summary>Crosses out the first unfinished order matching this exact consuming action.</summary>
        /// <param name="source">Interaction publishing a lifecycle event.</param>
        /// <param name="moment">Successful start or completion boundary.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // One consumption fulfils one row; completed and waiting rows retain their queue positions.
            if (!registered || moment != InteractionMoment.Completed)
                return;
            bool changed = false;
            bool complete = true;
            foreach (OrderTicket ticket in tickets)
            {
                if (!changed && !ticket.Completed && ticket.Entry.Source == source)
                {
                    ticket.Completed = true;
                    ticket.Board?.Draw(ticket);
                    changed = true;
                }
                complete &= ticket.Completed;
            }
            if (changed && complete)
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Checks that selected local contact actions really perform consumption.</summary>
        /// <param name="warning">Receives a missing source, board or text.</param>
        /// <returns>True when the order interaction can publish its entries.</returns>
        public override bool TryValidate(out string warning)
        {
            // The scene board can be loaded later; its absence never discards waiting orders.
            warning = "Order settings are missing.";
            return settings != null && settings.TryValidate(gameObject, out warning);
        }

        #endregion
        #endregion
    }
}
