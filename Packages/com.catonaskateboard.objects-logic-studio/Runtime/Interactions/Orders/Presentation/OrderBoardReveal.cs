using System;
using System.Collections;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Schedules occupied slots without running an idle frame loop or creating UI.</summary>
    internal sealed class OrderBoardReveal
    {
        #region State

        private OrderBoard board;
        private OrderTicket[] pending = Array.Empty<OrderTicket>();
        private double[] deadlines = Array.Empty<double>();
        private double latest = double.NegativeInfinity;
        private int count;
        private Coroutine routine;

        #endregion

        #region Methods
        #region Scheduling

        /// <summary>Prepares fixed slot storage when a board joins the queue.</summary>
        /// <param name="owner">Board receiving scheduled presentation callbacks.</param>
        /// <param name="capacity">Number of existing text slots.</param>
        internal void Initialize(OrderBoard owner, int capacity)
        {
            // Board restoration cancels a previous session before this storage is replaced.
            board = owner;
            pending = new OrderTicket[capacity];
            deadlines = new double[capacity];
            latest = double.NegativeInfinity;
        }

        /// <summary>Reserves the next reveal time while allowing the first slot to appear immediately.</summary>
        /// <param name="ticket">Already assigned ticket.</param>
        /// <param name="interval">Delay after the preceding slot.</param>
        /// <returns>True when the caller can draw immediately.</returns>
        internal bool Schedule(OrderTicket ticket, float interval)
        {
            // Absolute scaled times preserve ordering across frames and pause with the game.
            latest = Math.Max(Time.timeAsDouble, latest + interval);
            if (latest <= Time.timeAsDouble)
                return true;
            pending[ticket.Slot] = ticket;
            deadlines[ticket.Slot] = latest;
            count++;
            routine ??= board.StartCoroutine(Reveal());
            return false;
        }

        /// <summary>Checks whether an assigned slot is still hidden by its reveal delay.</summary>
        /// <param name="slot">Assigned text-slot index.</param>
        /// <returns>True while the original ticket is waiting for presentation.</returns>
        internal bool IsPending(int slot)
        {
            // Released and out-of-range slots never suppress presentation.
            return slot >= 0 && slot < pending.Length && pending[slot] != null;
        }

        /// <summary>Cancels a released ticket before its slot can be reused.</summary>
        /// <param name="slot">Slot leaving the board.</param>
        internal void Cancel(int slot)
        {
            // Recompute the tail only at release boundaries, preventing cancelled tickets from adding gaps.
            if (!IsPending(slot))
                return;
            pending[slot] = null;
            count--;
            latest = Time.timeAsDouble;
            for (int index = 0; index < pending.Length; index++)
                if (pending[index] != null)
                    latest = Math.Max(latest, deadlines[index]);
        }

        /// <summary>Stops pending reveals before board disable or a retained-scene Play reset.</summary>
        internal void Clear()
        {
            // Stop explicitly because disabling a component alone does not stop Unity coroutines.
            if (routine != null && board != null)
                board.StopCoroutine(routine);
            routine = null;
            count = 0;
            latest = double.NegativeInfinity;
            Array.Clear(pending, 0, pending.Length);
        }

        #endregion
        #region Presentation

        /// <summary>Advances only while hidden tickets exist and starts their customer clocks at actual display.</summary>
        /// <returns>A scaled-time presentation sequence.</returns>
        private IEnumerator Reveal()
        {
            // Yield before scanning so the caller finishes queue assignment and coroutine ownership first.
            while (count > 0)
            {
                yield return null;
                for (int index = 0; index < pending.Length; index++)
                {
                    OrderTicket ticket = pending[index];
                    if (ticket == null || Time.timeAsDouble < deadlines[index])
                        continue;
                    pending[index] = null;
                    count--;
                    board.Draw(ticket);
                    if (ticket.Board == board && ticket.Owner != null)
                        ticket.Owner.NotifyDisplayed();
                }
            }
            routine = null;
        }

        #endregion
        #endregion
    }
}
