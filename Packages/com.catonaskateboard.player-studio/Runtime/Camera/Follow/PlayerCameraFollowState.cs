using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Follows a moving focus with an exponential response that accounts for motion between frame samples.</summary>
    internal struct PlayerCameraFollowState
    {
        #region State

        private Vector3 position;
        private Vector3 previousTarget;

        #endregion

        #region Methods

        #region Integration

        /// <summary>Starts from the confirmed focus when the rig is initialized or explicitly repositioned.</summary>
        /// <param name="target">Initial focus in world space.</param>
        public void Reset(Vector3 target)
        {
            // Initialization has no inherited velocity or lag from a previous camera configuration.
            position = previousTarget = target;
        }

        /// <summary>Integrates a linearly moving target over the frame instead of treating it as an instantaneous step.</summary>
        /// <param name="target">Focus after this frame's player and model movement.</param>
        /// <param name="response">Validated positive exponential response per second.</param>
        /// <param name="duration">Positive elapsed simulation time for this frame.</param>
        /// <returns>The filtered focus, without altering the target or the look direction.</returns>
        public Vector3 Advance(Vector3 target, float response, float duration)
        {
            // Translation and look use the same integration and precision policy.
            PlayerCameraDamping.GetWeights(response * (double)duration, out float targetWeight, out float motionWeight);
            position += (previousTarget - position) * targetWeight + (target - previousTarget) * motionWeight;
            previousTarget = target;
            return position;
        }

        #endregion

        #endregion
    }
}
