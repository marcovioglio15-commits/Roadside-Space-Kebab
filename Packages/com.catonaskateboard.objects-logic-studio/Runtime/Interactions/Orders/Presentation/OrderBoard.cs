using System;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
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
        private OrderTicket[] occupied = Array.Empty<OrderTicket>();
        private FontStyles[] styles = Array.Empty<FontStyles>();
        private string[] originals = Array.Empty<string>();
        private bool registered;
        private int generation = -1;

        #endregion
        #region Properties

        /// <summary>Scene destination selected by prefab order settings.</summary>
        public string Identity => identity;
        /// <summary>Number of authored order slots.</summary>
        public int Capacity => slots.Length;

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
            for (int index = 0; index < slots.Length; index++)
            {
                styles[index] = slots[index].fontStyle;
                originals[index] = slots[index].text;
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
            // No text object is instantiated, destroyed or reparented during gameplay.
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
            // Completed waiting orders are assigned already crossed out.
            for (int index = 0; index < occupied.Length; index++)
                if (occupied[index] == null && slots[index] != null)
                {
                    occupied[index] = ticket;
                    ticket.Board = this;
                    ticket.Slot = index;
                    Draw(ticket);
                    return true;
                }
            return false;
        }

        /// <summary>Updates only the slot belonging to this exact ticket.</summary>
        /// <param name="ticket">Assigned order whose completion may have changed.</param>
        internal void Draw(OrderTicket ticket)
        {
            // TMP generates a real line through the text, including multiline layouts.
            int index = ticket.Slot;
            if (index < 0 || index >= occupied.Length || occupied[index] != ticket || slots[index] == null)
                return;
            slots[index].text = ticket.Text;
            slots[index].fontStyle = ticket.Completed ? styles[index] | FontStyles.Strikethrough
                : styles[index] & ~FontStyles.Strikethrough;
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
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
