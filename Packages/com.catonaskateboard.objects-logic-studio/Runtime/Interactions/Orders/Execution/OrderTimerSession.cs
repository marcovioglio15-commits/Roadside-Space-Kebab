using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Measures one customer's visible orders until Day Flow accepts its completion event.</summary>
    internal sealed class OrderTimerSession
    {
        #region State

        private static readonly List<OrderTimerSession> visible = new List<OrderTimerSession>();
        private ObjectAvailableOrders owner;
        private double started;
        private double stopped;
        private bool completed;

        #endregion
        #region Properties

        /// <summary>Scaled elapsed seconds; pause does not count towards order time.</summary>
        internal double Elapsed => (completed ? stopped : Time.timeAsDouble) - started;
        /// <summary>Whether the final Day Flow result has frozen this customer's time.</summary>
        internal bool Completed => completed;
        /// <summary>Order settings belonging to the measured customer.</summary>
        internal OrderSettings Settings => owner.Settings;

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Starts only when the first ticket actually acquires a board slot.</summary>
        /// <param name="customer">Owner of the newly displayed ticket.</param>
        internal void Begin(ObjectAvailableOrders customer)
        {
            // Remaining tickets and board reactivation never reset this same customer's clock.
            if (owner != null || !customer.Settings.UseTimer)
                return;
            owner = customer;
            started = Time.timeAsDouble;
            completed = false;
            visible.Add(this);
        }

        /// <summary>Releases this customer's clock at despawn or a new Play session.</summary>
        internal void Release()
        {
            visible.Remove(this);
            owner = null;
            completed = false;
        }

        /// <summary>Freezes elapsed time when this customer's order set succeeds or receives an unexpected delivery.</summary>
        internal void Complete()
        {
            if (owner == null || completed)
                return;
            stopped = Time.timeAsDouble;
            completed = true;
        }

        /// <summary>Freezes clocks only for the visitor whose actual Day Flow condition completed.</summary>
        /// <param name="visitor">Generated visitor root accepted by Day Flow.</param>
        internal static void Complete(GameObject visitor)
        {
            // Completion on another customer, consumption alone or dialogue interruption cannot stop this timer.
            foreach (OrderTimerSession timer in visible)
                if (!timer.completed && timer.owner != null && timer.owner.transform.IsChildOf(visitor.transform))
                    timer.Complete();
        }

        /// <summary>Returns the latest displayed customer in the HUD's scene.</summary>
        /// <param name="scene">Scene owning the authored HUD.</param>
        /// <returns>Most recently started live session, or null when no timer is visible.</returns>
        internal static OrderTimerSession Current(Scene scene)
        {
            // Registration order supplies deterministic ownership without scanning scene objects.
            for (int index = visible.Count - 1; index >= 0; index--)
                if (visible[index].owner != null && visible[index].owner.gameObject.scene == scene)
                    return visible[index];
            return null;
        }

        /// <summary>Clears static ownership before retained scene objects initialize again.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            visible.Clear();
        }

        #endregion
        #endregion
    }
}
