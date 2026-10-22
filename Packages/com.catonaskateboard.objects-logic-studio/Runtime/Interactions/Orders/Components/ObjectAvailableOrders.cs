using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Publishes selected consumption orders and retains completed rows until its customer disappears.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Available Orders")]
    public sealed class ObjectAvailableOrders : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Order Requests")]
        [Tooltip("Shared order catalog, destination board and extraction policy for this object's current spawn.")]
        [SerializeField]
        private OrderSettings settings = new OrderSettings();
        private readonly OrderTimerSession timer = new OrderTimerSession();
        private OrderTicket[] tickets = Array.Empty<OrderTicket>();
        private bool registered;
        private bool ready;
        private bool presentationHeld;
        private bool drawn;
        private bool presentationCompleted;
        private ObjectContactModifier contact;
        private int generation = -1;
        private int consumptionRevision = -1;
        private bool consumedPendingOrder;

        #endregion
        #region Properties

        /// <summary>Shared catalog and per-spawn extraction configuration.</summary>
        public OrderSettings Settings => settings;
        /// <summary>One-based occurrence shared by clones of the same source prefab during this play session.</summary>
        public int SpawnIndex { get; private set; }
        /// <summary>Catalog selected once for this spawn, including any matching occurrence override.</summary>
        public OrderCatalog ActiveCatalog { get; private set; }
        /// <summary>Changes only when the active ticket set or consumption result changes.</summary>
        public int Revision { get; private set; }
        /// <summary>Whether the latest consumption supplied units that matched no unfinished order.</summary>
        public bool UnexpectedConsumption { get; private set; }
        /// <summary>Whether a nonempty extracted ticket set has been completely consumed.</summary>
        public bool AllCompleted { get; private set; }
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AvailableOrders;

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
            // A new Play session also resets customers retained by disabled scene reload.
            if (generation != OrderQueue.Generation)
            {
                Release();
                presentationHeld = false;
                generation = OrderQueue.Generation;
            }
            ObjectInteraction.Signaled -= Observe;
            ObjectInteraction.Signaled += Observe;
            OrderQueue.BoardAvailable -= Draw;
            OrderQueue.BoardAvailable += Draw;
            if (registered)
                return;
            PrepareSpawn(ObjectSpawnHistory.Source, ObjectSpawnHistory.Occurrence);
            if (!presentationHeld)
            {
                ready = TryValidate(out string warning);
                if (!ready)
                    Debug.LogWarning(warning, this);
            }
            contact = GetComponent<ObjectContactModifier>();
            Draw();
            Publish();
        }

        /// <summary>Waits for an initial availability lock without scanning boards or text slots.</summary>
        private void Update()
        {
            // Once published, completed and locked orders retain their slots until this object leaves.
            if (!registered && ready)
                Publish();
        }

        /// <summary>Starts the optional clock at the first actual board assignment for this customer.</summary>
        internal void NotifyDisplayed()
        {
            // Queued orders and additional rows never restart the same spawn's stopwatch.
            timer.Begin(this);
        }

        /// <summary>Queues one ticket per selected unit while retaining the spawn's fixed selection.</summary>
        private void Publish()
        {
            // Day Flow presentation is allowed during walk paths while gameplay physics is suspended.
            if (registered || !ready || settings.WaitForCompletion && !presentationCompleted
                || !presentationHeld && !Available(InteractionChannels.Passive))
                return;
            if (!drawn || tickets.Length == 0)
                return;
            registered = true;
            foreach (OrderTicket ticket in tickets)
                OrderQueue.Add(ticket);
            Signal(InteractionMoment.Started);
        }

        /// <summary>Draws once when the destination board can provide its authored capacity.</summary>
        private void Draw()
        {
            // Board registration may follow customer activation during scene loading.
            if (drawn || !ready || !OrderQueue.TryCapacity(settings.Board, out int capacity))
                return;
            tickets = OrderDraw.Select(this, capacity);
            drawn = true;
            OrderQueue.BoardAvailable -= Draw;
            Revision++;
            Publish();
        }

        /// <summary>Freezes occurrence and catalog before activation, including inactive Day Flow staging.</summary>
        /// <param name="source">Original prefab supplied by the spawn boundary, or null for scene activation.</param>
        /// <param name="occurrence">Reserved prefab occurrence, or zero to start a direct scene/pool lifetime.</param>
        internal void PrepareSpawn(GameObject source, int occurrence)
        {
            if (generation != OrderQueue.Generation)
            {
                Release();
                generation = OrderQueue.Generation;
            }
            if (SpawnIndex > 0)
                return;
            ObjectItem item = GetComponent<ObjectItem>();
            item.PrepareSpawn(source, occurrence);
            SpawnIndex = item.Spawn.Index;
            ActiveCatalog = settings?.ResolveCatalog(SpawnIndex);
        }

        /// <summary>Distinguishes an animation's temporary deactivation from actual customer despawn.</summary>
        /// <param name="hold">Whether the current flow transition retains board ownership.</param>
        internal void HoldPresentation(bool hold)
        {
            // A staged clone can be suspended before its first OnEnable in the current Play session.
            if (hold && generation != OrderQueue.Generation)
            {
                Release();
                generation = OrderQueue.Generation;
                ready = false;
            }
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
            timer.Release();
            // Empty tickets cover invalid, unpublished and already released interactions.
            ObjectInteraction.Signaled -= Observe;
            OrderQueue.BoardAvailable -= Draw;
            if (registered)
                OrderQueue.Remove(this);
            registered = false;
            drawn = false;
            presentationCompleted = false;
            UnexpectedConsumption = false;
            AllCompleted = false;
            consumptionRevision = -1;
            consumedPendingOrder = false;
            Revision++;
            tickets = Array.Empty<OrderTicket>();
            SpawnIndex = 0;
            ActiveCatalog = null;
        }

        #endregion
        #region Completion

        /// <summary>Fulfils unfinished tickets from the local contact interaction's exact consumption receipt.</summary>
        /// <param name="source">Interaction publishing a lifecycle event.</param>
        /// <param name="moment">Successful start or completion boundary.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Presentation is independent of extraction and may complete before a late-loaded board exists.
            if (moment == InteractionMoment.Completed && settings.WaitForCompletion && source == settings.CompletionSource)
            {
                presentationCompleted = true;
                Publish();
            }
            // Each consumed unit fulfils at most one ticket; completed tickets retain their queue positions.
            if (!drawn || source != contact || moment != InteractionMoment.Completed || contact.ConsumedUnits <= 0)
                return;
            bool changed = false;
            bool complete = true;
            int remaining = contact.ConsumedUnits;
            // Specific recipe tickets take precedence over a base order accepting the same root identity.
            int specificity = 0;
            foreach (OrderTicket ticket in tickets)
                specificity = Mathf.Max(specificity, ticket.Candidate.Specificity);
            for (int pass = specificity; pass >= 0 && remaining > 0; pass--)
                foreach (OrderTicket ticket in tickets)
                {
                    if (remaining <= 0)
                        break;
                    if (ticket.Completed || ticket.Candidate.Specificity != pass || !contact.ConsumedMatches(ticket.Candidate))
                        continue;
                    ticket.Completed = true;
                    contact.OverrideConsumedPrice(ticket.Candidate.Entry);
                    ticket.Board?.Draw(ticket);
                    changed = true;
                    remaining--;
                }
            foreach (OrderTicket ticket in tickets)
                complete &= ticket.Completed;
            UnexpectedConsumption = remaining > 0;
            AllCompleted = tickets.Length > 0 && complete;
            // Stop immediately on a fulfilled set or a delivery that matched no open ticket.
            if (UnexpectedConsumption || changed && complete)
                timer.Complete();
            // Classify the receipt against slots that were open before fulfilment, including repeated orders.
            consumedPendingOrder = changed;
            consumptionRevision = Item.ConsumptionRevision;
            Revision++;
            if (changed && complete)
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Classifies the latest delivery using the ticket state before that delivery crossed out any slots.</summary>
        /// <param name="pending">True when at least one open slot accepted the consumed object.</param>
        /// <returns>True only when this result belongs to the item's latest consumption receipt.</returns>
        internal bool TryLatestDelivery(out bool pending)
        {
            // A later receipt from another source must never reuse this interaction's previous result.
            pending = consumedPendingOrder;
            return drawn && Item != null && Item.HasConsumption && consumptionRevision == Item.ConsumptionRevision;
        }

        /// <summary>Reads progress for one named candidate in this spawn's fixed ticket set.</summary>
        /// <param name="order">Exact catalog name selected by a dialogue filter.</param>
        /// <param name="completed">Whether every active unit of that order is complete.</param>
        /// <returns>True when this order was drawn for the current spawn.</returns>
        public bool TryProgress(string order, out bool completed)
        {
            bool active = false;
            completed = true;
            foreach (OrderTicket ticket in tickets)
                if (ticket.Name == order)
                {
                    active = true;
                    completed &= ticket.Completed;
                }
            return active;
        }

        /// <summary>Checks the catalog and the object's local consumption capability.</summary>
        /// <param name="warning">Receives an invalid catalog, board ID or consuming interaction.</param>
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
