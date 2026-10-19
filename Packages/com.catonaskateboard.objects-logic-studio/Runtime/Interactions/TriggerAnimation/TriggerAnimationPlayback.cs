namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks the requested endpoint or full return cycle, including interruption and effect handoff.</summary>
    internal sealed class TriggerAnimationPlayback
    {
        #region State

        private readonly ObjectTriggerAnimation source;
        private readonly bool waitForReturn;
        private bool completed;
        private bool cancelled;
        private bool released;

        #endregion

        #region Properties

        /// <summary>Whether the requested transition reached its endpoint and its component is still available.</summary>
        internal bool Completed => completed && !Cancelled;
        /// <summary>Whether disabling, destruction or a reset invalidated this request.</summary>
        internal bool Cancelled => cancelled || source == null || !source.isActiveAndEnabled;
        /// <summary>Whether the endpoint must remain stable until Eject processes its next physics tick.</summary>
        internal bool AwaitingEffect => Completed && !released;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Retains one requesting animation without allocating anything during its frame updates.</summary>
        /// <param name="source">Animation responsible for completing or cancelling this request.</param>
        /// <param name="waitForReturn">Whether a scheduled automatic return must also finish.</param>
        internal TriggerAnimationPlayback(ObjectTriggerAnimation source, bool waitForReturn)
        {
            // The source is also checked for Unity destruction before Eject consumes this result.
            this.source = source;
            this.waitForReturn = waitForReturn;
        }

        /// <summary>Records arrival at the requested endpoint before completion listeners are notified.</summary>
        /// <param name="returnPending">Whether another automatic leg is scheduled for this cycle.</param>
        internal void Complete(bool returnPending)
        {
            // A full-cycle request leaves the turnaround free to start its automatic return.
            completed = !waitForReturn || !returnPending;
        }

        /// <summary>Prevents a cancelled transition from releasing a delayed physical effect.</summary>
        internal void Cancel()
        {
            cancelled = true;
        }

        /// <summary>Allows an automatic return after Eject has consumed the completed prerequisite.</summary>
        internal void Release()
        {
            released = true;
        }

        #endregion

        #endregion
    }
}
