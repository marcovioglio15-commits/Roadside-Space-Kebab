using System;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses FIFO rows or rows bound to specific catalog orders.</summary>
    public enum OrderBoardMode { Queue, MatchingOrders }

    /// <summary>Owns pre-authored world-space text slots shared by all queued customer orders.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Order Board")]
    public sealed class OrderBoard : MonoBehaviour
    {
        #region Fields

        [Header("Scene Board")]
        [Tooltip("Unique active board ID used by Available Orders interactions.")]
        [SerializeField]
        private string identity = "Orders";
        [Tooltip("Existing 3D TextMeshPro or world-space Canvas text objects, one per order slot. Runtime creates no UI.")]
        [SerializeField]
        private TMP_Text[] slots = Array.Empty<TMP_Text>();
        [Tooltip("Paragraphs prepared per slot before Play. Count each variant fragment separated by ';'. Include base fragments when text contains ';' or uses custom typography.")]
        [SerializeField]
        [Range(1, 33)]
        private int paragraphCapacity = 9;

        [Header("Display Mode")]
        [Tooltip("Queue uses the next free slot. Matching Orders displays tickets on slots bound to their catalog names.")]
        [SerializeField]
        private OrderBoardMode mode;
        [Tooltip("One exact catalog name per text slot, in slot order. A base name also accepts its variants and combos.")]
        [SerializeField]
        private string[] orderNames = Array.Empty<string>();
        [Header("Slot Objects")]
        [Tooltip("Show each assigned scene object only while its text slot has an order.")]
        [SerializeField]
        private bool showSlotObjects;
        [Tooltip("Optional scene objects in text-slot order. Each assigned object is shown and tinted independently.")]
        [SerializeField]
        private GameObject[] slotObjects = Array.Empty<GameObject>();
        [Tooltip("Reveal occupied slot objects and their text in sequence. The first slot appears immediately.")]
        [SerializeField]
        private bool staggerSlotObjects;
        [Tooltip("Scaled seconds between successive slot reveals. Individual slots may override this interval.")]
        [SerializeField]
        [Range(0f, 10f)]
        private float slotObjectDelay = 0.25f;
        [Tooltip("Optional delay overrides in text-slot order. Unconfigured slots use the shared interval.")]
        [SerializeField]
        private OrderSlotRevealDelay[] slotObjectDelays = Array.Empty<OrderSlotRevealDelay>();
        [Tooltip("Multiply the assigned scene object's material colors by the order's delivery state.")]
        [SerializeField]
        private bool tintByState;
        [Tooltip("Object color multiplier while the order is waiting for delivery.")]
        [SerializeField]
        private Color pendingTint = new Color(0.78f, 0.60f, 0.43f, 1f);
        [Tooltip("Object color multiplier after the correct delivery.")]
        [SerializeField]
        private Color completedTint = new Color(0.46f, 0.88f, 0.49f, 1f);
        [Tooltip("Object color multiplier for a pending order after a wrong delivery.")]
        [SerializeField]
        private Color incorrectTint = new Color(1f, 0.39f, 0.35f, 1f);

        private OrderSlotObject[] objectViews = Array.Empty<OrderSlotObject>();
        private OrderBoardText[] formatting = Array.Empty<OrderBoardText>();
        private OrderTicket[] occupied = Array.Empty<OrderTicket>();
        private FontStyles[] styles = Array.Empty<FontStyles>();
        private string[] originals = Array.Empty<string>();
        private bool registered;
        private int generation = -1;
        private readonly OrderBoardReveal reveal = new OrderBoardReveal();

        #endregion
        #region Properties

        /// <summary>Scene destination selected by prefab order settings.</summary>
        public string Identity => identity;
        /// <summary>Number of authored order slots.</summary>
        public int Capacity => slots.Length;
        /// <summary>Number of independent text paragraphs to author for each slot before Play.</summary>
        public int ParagraphCapacity => paragraphCapacity;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Registers a valid board and immediately fills available slots from waiting orders.</summary>
        private void OnEnable()
        {
            // Slot ownership is established before any queue assignment can draw text.
            if (!Application.isPlaying)
                return;
            Initialize();
        }

        /// <summary>Captures authored text styles once and joins the shared board catalog.</summary>
        internal void Initialize()
        {
            // Rebuild is explicit for Play sessions that retain scene objects.
            if (generation != OrderQueue.Generation)
            {
                registered = false;
                Restore();
                generation = OrderQueue.Generation;
            }
            if (registered)
                return;
            if (!TryValidate(out string warning))
            {
                Debug.LogWarning(warning, this);
                return;
            }
            occupied = new OrderTicket[slots.Length];
            styles = new FontStyles[slots.Length];
            originals = new string[slots.Length];
            objectViews = showSlotObjects ? new OrderSlotObject[slots.Length] : Array.Empty<OrderSlotObject>();
            formatting = new OrderBoardText[slots.Length];
            reveal.Initialize(this, slots.Length);
            for (int index = 0; index < slots.Length; index++)
            {
                formatting[index] = slots[index].GetComponent<OrderBoardText>();
                formatting[index]?.Initialize(slots[index]);
                styles[index] = slots[index].fontStyle;
                originals[index] = slots[index].text;
                if (index < objectViews.Length && index < slotObjects.Length && slotObjects[index] != null)
                    objectViews[index] = new OrderSlotObject(slotObjects[index], tintByState);
                slots[index].text = string.Empty;
            }
            registered = OrderQueue.Add(this);
            if (!registered)
                Restore();
        }

        /// <summary>Returns occupied tickets to the queue when their board disappears.</summary>
        private void OnDisable()
        {
            // Customers outliving an additive board can resume on a replacement with the same ID.
            if (registered)
                OrderQueue.Remove(this);
            registered = false;
            Restore();
        }

        /// <summary>Restores authored presentation after leaving Play or disabling the board.</summary>
        private void Restore()
        {
            // Hidden tickets must never reappear after their board leaves the queue.
            reveal.Clear();
            // No text object is instantiated, destroyed or reparented during gameplay.
            foreach (OrderBoardText text in formatting)
                if (text != null)
                    text.Restore();
            formatting = Array.Empty<OrderBoardText>();
            foreach (OrderSlotObject view in objectViews)
                view?.Restore();
            objectViews = Array.Empty<OrderSlotObject>();
            for (int index = 0; index < originals.Length; index++)
                if (index < slots.Length && slots[index] != null)
                {
                    slots[index].text = originals[index];
                    slots[index].fontStyle = styles[index];
                }
            occupied = Array.Empty<OrderTicket>();
            originals = Array.Empty<string>();
        }

        #endregion
        #region Slots

        /// <summary>Assigns the earliest free authored slot to one waiting order.</summary>
        /// <param name="ticket">Queued customer entry.</param>
        /// <returns>True when a slot was reserved.</returns>
        internal bool TryAssign(OrderTicket ticket)
        {
            // Prefer a variant or combo's exact slot before using a base-order slot.
            int index = mode == OrderBoardMode.Queue ? FindSlot(null) : FindSlot(ticket.Name);
            if (index < 0 && mode == OrderBoardMode.MatchingOrders && ticket.Name != ticket.Entry.Name)
                index = FindSlot(ticket.Entry.Name);
            if (index < 0)
                return false;
            occupied[index] = ticket;
            ticket.Board = this;
            ticket.Slot = index;
            if (!showSlotObjects || !staggerSlotObjects || reveal.Schedule(ticket, RevealDelay(index)))
            {
                Draw(ticket);
                if (ticket.Board == this && ticket.Owner != null)
                    ticket.Owner.NotifyDisplayed();
            }
            return true;
        }

        /// <summary>Resolves one slot's optional interval without modifying its stored configuration.</summary>
        /// <param name="index">Assigned slot in text-slot order.</param>
        /// <returns>The per-slot override or shared board interval.</returns>
        private float RevealDelay(int index)
        {
            // Missing overrides inherit the common interval.
            return slotObjectDelays != null && index < slotObjectDelays.Length && slotObjectDelays[index] is { Override: true }
                ? slotObjectDelays[index].Delay : slotObjectDelay;
        }

        /// <summary>Finds a free slot that can present this catalog entry.</summary>
        /// <param name="order">Exact catalog name, or null in Queue mode.</param>
        /// <returns>Index of the first compatible free slot, or minus one.</returns>
        private int FindSlot(string order)
        {
            // A vacant row never borrows another order's named position.
            for (int index = 0; index < occupied.Length; index++)
                if (occupied[index] == null && slots[index] != null && (order == null || orderNames[index] == order))
                    return index;
            return -1;
        }

        /// <summary>Updates only the slot belonging to this exact ticket.</summary>
        /// <param name="ticket">Assigned order whose completion may have changed.</param>
        internal void Draw(OrderTicket ticket)
        {
            if (!OwnsSlot(ticket) || reveal.IsPending(ticket.Slot))
                return;
            int index = ticket.Slot;
            if (index < objectViews.Length)
                objectViews[index]?.Show(Tint(ticket));
            slots[index].text = OrderTextParagraphs.PlainText(ticket.Candidate.MainText);
            slots[index].fontStyle = styles[index];
            // The active spawn catalog also covers occurrence-specific catalog overrides.
            OrderTextStyle shared = ticket.Owner.ActiveCatalog?.TextStyle;
            if (formatting[index] == null && (ticket.Candidate.HasVariants || ticket.Candidate.MainText.IndexOf(';') >= 0
                || OrderTextStyle.Resolve(ticket.Entry.TextStyle, shared) != null))
                Debug.LogWarning("Prepare text paragraphs on this Order Board before Play for ';' separators, recipe variants or custom typography.", this);
            else
                formatting[index]?.Draw(ticket.Candidate, shared);
        }

        /// <summary>Refreshes the color of a visible order after gameplay has resolved the receipt.</summary>
        /// <param name="ticket">Ticket whose completion or incorrect state may have changed.</param>
        internal void RefreshFeedback(OrderTicket ticket)
        {
            if (OwnsSlot(ticket) && ticket.Slot < objectViews.Length)
                objectViews[ticket.Slot]?.ApplyTint(Tint(ticket));
        }

        /// <summary>Checks slot ownership before updating text or post-it colors.</summary>
        /// <param name="ticket">Ticket to display.</param>
        /// <returns>True when this board still displays the exact ticket.</returns>
        private bool OwnsSlot(OrderTicket ticket)
        {
            return ticket.Slot >= 0 && ticket.Slot < occupied.Length && occupied[ticket.Slot] == ticket && slots[ticket.Slot] != null;
        }

        /// <summary>Maps gameplay results to the board's configured color multipliers.</summary>
        /// <param name="ticket">Current order ticket.</param>
        /// <returns>Completed, incorrect or pending tint.</returns>
        private Color Tint(OrderTicket ticket)
        {
            return ticket.Completed ? completedTint : ticket.Incorrect ? incorrectTint : pendingTint;
        }

        /// <summary>Clears a departed customer's slot without shifting other visible orders.</summary>
        /// <param name="ticket">Ticket released by its owner.</param>
        internal void Release(OrderTicket ticket)
        {
            // Stable slot positions make a completed order remain recognisable until departure.
            int index = ticket.Slot;
            if (index >= 0 && index < occupied.Length && occupied[index] == ticket)
            {
                occupied[index] = null;
                reveal.Cancel(index);
                if (index < objectViews.Length)
                    objectViews[index]?.Hide();
                formatting[index]?.Clear();
                if (slots[index] != null)
                {
                    slots[index].text = string.Empty;
                    slots[index].fontStyle = styles[index];
                }
            }
            ticket.Board = null;
            ticket.Slot = -1;
        }

        #endregion
        #region Validation

        /// <summary>Checks scene presentation without silently generating or replacing missing text objects.</summary>
        /// <param name="warning">Receives a missing ID, duplicate slot or non-world-space text.</param>
        /// <returns>True when the board can accept orders.</returns>
        public bool TryValidate(out string warning)
        {
            // Both standalone 3D TMP and Canvas world-space TMP are supported.
            warning = "Order Board needs a unique ID and distinct assigned 3D text slots.";
            if (string.IsNullOrWhiteSpace(identity) || slots == null || slots.Length == 0)
                return false;
            System.Collections.Generic.HashSet<TMP_Text> unique = new System.Collections.Generic.HashSet<TMP_Text>();
            foreach (TMP_Text text in slots)
                if (text == null || !unique.Add(text) || !text.transform.IsChildOf(transform)
                    || text is TextMeshProUGUI && (text.canvas == null || text.canvas.renderMode != RenderMode.WorldSpace))
                    return false;
            // Named boards need a stable association for every pre-existing text slot.
            warning = "Choose Queue or Matching Orders and enter one catalog name per text slot in Matching Orders.";
            if (mode is not (OrderBoardMode.Queue or OrderBoardMode.MatchingOrders)
                || mode == OrderBoardMode.MatchingOrders && (orderNames == null || orderNames.Length != slots.Length
                    || Array.Exists(orderNames, string.IsNullOrWhiteSpace)))
                return false;
            if (showSlotObjects)
            {
                // Inactive delay options do not prevent a board from running.
                warning = "Slot reveal delays must be finite and non-negative.";
                if (staggerSlotObjects && (!float.IsFinite(slotObjectDelay) || slotObjectDelay < 0f
                    || slotObjectDelays != null && Array.Exists(slotObjectDelays,
                        delay => delay is { Override: true } && (!float.IsFinite(delay.Delay) || delay.Delay < 0f))))
                    return false;
                // A slot object can live anywhere in the scene as long as it cannot deactivate this board.
                warning = "Assign distinct scene objects in text-slot order. Do not assign the board or one of its parents.";
                if (slotObjects == null || slotObjects.Length > slots.Length)
                    return false;
                System.Collections.Generic.HashSet<GameObject> objects = new System.Collections.Generic.HashSet<GameObject>();
                foreach (GameObject target in slotObjects)
                    if (target != null && (!objects.Add(target) || transform.IsChildOf(target.transform)
                        || gameObject.scene.IsValid() && !target.scene.IsValid()))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
