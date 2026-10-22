using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks the requested endpoint or full return cycle, including interruption and effect handoff.</summary>
    internal sealed class TriggerAnimationPlayback
    {
        #region State

        private readonly ObjectTriggerAnimation source;
        private readonly bool waitForReturn;
        private readonly bool holdEndpoint;
        private readonly float duration;
        private float elapsed;
        private bool completed;
        private bool cancelled;
        private bool released;

        #endregion

        #region Properties

        /// <summary>Whether the requested transition reached its endpoint and its component is still available.</summary>
        internal bool Completed => completed && !Cancelled;
        /// <summary>Whether disabling, destruction or a reset invalidated this request.</summary>
        internal bool Cancelled => cancelled || source == null || !source.isActiveAndEnabled;
        /// <summary>Whether an Eject request still owns this animation cycle, including its return delay.</summary>
        internal bool Retained => !released && !Cancelled;
        /// <summary>Whether a completed endpoint is still retained by its requesting Eject.</summary>
        internal bool AwaitingEffect => Completed && Retained;
        /// <summary>Whether the first endpoint must remain stable until Eject requests a return.</summary>
        internal bool HoldEndpoint => holdEndpoint && Retained;
        /// <summary>Actual elapsed fraction of this requested animation cycle.</summary>
        internal float Progress => Completed ? 1f : duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
        /// <summary>Animation whose item reservation may coexist with a Black Hole overlap.</summary>
        internal ObjectTriggerAnimation Source => source;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Retains one requesting animation without allocating anything during its frame updates.</summary>
        /// <param name="source">Animation responsible for completing or cancelling this request.</param>
        /// <param name="waitForReturn">Whether a scheduled automatic return must also finish.</param>
        /// <param name="holdEndpoint">Whether the animation must stay at its first endpoint for a later forced return.</param>
        internal TriggerAnimationPlayback(ObjectTriggerAnimation source, bool waitForReturn, bool holdEndpoint)
        {
            // The source is also checked for Unity destruction before Eject consumes this result.
            this.source = source;
            this.waitForReturn = waitForReturn;
            this.holdEndpoint = holdEndpoint;
            duration = source.Settings.Duration + (waitForReturn && source.Settings.AutoReturn
                ? source.Settings.ReturnDelay + source.Settings.AutoReturnDuration : 0f);
        }

        /// <summary>Advances only while the source is actively simulating this request.</summary>
        /// <param name="deltaTime">Scaled time consumed by the source animation update.</param>
        internal void Advance(float deltaTime)
        {
            // Locks and endpoint holds contribute no simulated time to the overlap threshold.
            if (!completed && !Cancelled)
                elapsed += deltaTime;
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
            released = true;
            if (source != null)
                source.ReleaseRequest(this, holdEndpoint);
        }

        /// <summary>Allows an automatic return after Eject has consumed the completed prerequisite.</summary>
        internal void Release()
        {
            released = true;
            if (source != null)
                source.ReleaseRequest(this, false);
        }

        /// <summary>Requests the source's authored return motion after an effect reaches its chosen endpoint.</summary>
        internal void Return()
        {
            released = true;
            if (!Cancelled)
                source.ReleaseRequest(this, true);
        }

        #endregion

        #endregion
    }
}
