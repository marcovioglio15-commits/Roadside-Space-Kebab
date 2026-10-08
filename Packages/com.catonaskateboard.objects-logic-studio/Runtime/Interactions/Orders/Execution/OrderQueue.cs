using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one order independently of whether a scene text slot is currently free.</summary>
    internal sealed class OrderTicket
    {
        #region State

        internal readonly ObjectAvailableOrders Owner;
        internal readonly OrderCandidate Candidate;
        internal OrderEntry Entry => Candidate.Entry;
        internal string Name => Candidate.Name;
        internal readonly string Destination;
        internal string Text => Candidate.Text;
        internal OrderBoard Board;
        internal int Slot = -1;
        internal bool Completed;

        #endregion
        #region Methods

        /// <summary>Captures one customer-owned entry in its initial queue position.</summary>
        /// <param name="owner">Customer interaction retaining the ticket.</param>
        /// <param name="entry">Selected order identity requirements and authored text.</param>
        /// <param name="destination">Scene board ID.</param>
        internal OrderTicket(ObjectAvailableOrders owner, OrderCandidate entry, string destination)
        {
            // The owner matches its current consumption receipt against this entry's identity flags.
            Owner = owner;
            Candidate = entry;
            Destination = destination;
        }

        #endregion
    }

    /// <summary>Assigns shared text slots in arrival order only when customers or boards change.</summary>
    internal static class OrderQueue
    {
        #region State

        private static readonly List<OrderTicket> tickets = new List<OrderTicket>();
        private static readonly Dictionary<string, OrderBoard> boards = new Dictionary<string, OrderBoard>();
        internal static event System.Action BoardAvailable;
        /// <summary>Identifies static resets when scene and domain reload are disabled.</summary>
        internal static int Generation { get; private set; }

        #endregion
        #region Methods
        #region Membership

        /// <summary>Registers a unique scene board and resolves waiting entries immediately.</summary>
        /// <param name="board">Activated board with prepared slot state.</param>
        /// <returns>True when this board owns its requested ID.</returns>
        internal static bool Add(OrderBoard board)
        {
            // Duplicate IDs never steal slots from a board already presenting orders.
            if (!boards.TryAdd(board.Identity, board))
            {
                Debug.LogWarning("Another active Order Board already uses ID: " + board.Identity, board);
                return false;
            }
            Fill();
            BoardAvailable?.Invoke();
            return true;
        }

        /// <summary>Reads configured capacity without treating occupied slots as missing capacity.</summary>
        /// <param name="identity">Destination board ID.</param>
        /// <param name="capacity">Receives the authored slot count.</param>
        /// <returns>True when the destination board is active.</returns>
        internal static bool TryCapacity(string identity, out int capacity)
        {
            capacity = boards.TryGetValue(identity, out OrderBoard board) ? board.Capacity : 0;
            return capacity > 0;
        }

        /// <summary>Queues one customer order, including when all board slots are occupied.</summary>
        /// <param name="ticket">New entry in spawn order.</param>
        internal static void Add(OrderTicket ticket)
        {
            // A missing board does not discard an order; later activation fills it.
            tickets.Add(ticket);
            Fill();
        }

        /// <summary>Detaches a disappearing board while preserving live customer requests.</summary>
        /// <param name="board">Board leaving the active scene.</param>
        internal static void Remove(OrderBoard board)
        {
            // Releasing a board does not imply that its customers completed or departed.
            if (boards.TryGetValue(board.Identity, out OrderBoard current) && current == board)
                boards.Remove(board.Identity);
            foreach (OrderTicket ticket in tickets)
                if (ticket.Board == board)
                    board.Release(ticket);
        }

        /// <summary>Frees every visible and waiting entry belonging to a departed customer.</summary>
        /// <param name="owner">Interaction being despawned or destroyed.</param>
        internal static void Remove(ObjectAvailableOrders owner)
        {
            // Fill once after removing the whole customer, preserving FIFO order across its rows.
            for (int index = tickets.Count - 1; index >= 0; index--)
                if (tickets[index].Owner == owner)
                {
                    tickets[index].Board?.Release(tickets[index]);
                    tickets.RemoveAt(index);
                }
            Fill();
        }

        /// <summary>Matches waiting requests against existing free slots without per-frame scans.</summary>
        private static void Fill()
        {
            // Full boards leave subsequent tickets in place until a slot-release event.
            foreach (OrderTicket ticket in tickets)
                if (ticket.Board == null && ticket.Owner != null && boards.TryGetValue(ticket.Destination, out OrderBoard board))
                    board.TryAssign(ticket);
        }

        #endregion
        #region Play Entry

        /// <summary>Clears static membership before each Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Scene components restore membership after static state is reset.
            tickets.Clear();
            boards.Clear();
            BoardAvailable = null;
            Generation++;
        }

        /// <summary>Recovers authored boards and customers when scene reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery is limited to Play entry; normal operation remains event driven.
            foreach (OrderBoard board in Object.FindObjectsByType<OrderBoard>())
                if (board.isActiveAndEnabled)
                    board.Initialize();
            foreach (ObjectAvailableOrders owner in Object.FindObjectsByType<ObjectAvailableOrders>())
                if (owner.isActiveAndEnabled)
                    owner.Initialize();
        }

        #endregion
        #endregion
    }
}
