using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Separates accumulated look input from the displayed angles when optional smoothing is enabled.</summary>
    internal struct PlayerCameraLookState
    {
        #region State

        private Vector2 target;
        private Vector2 angles;

        #endregion

        #region Methods

        #region Integration

        /// <summary>Starts without pending motion when a camera configuration is activated.</summary>
        /// <param name="initial">Validated starting yaw and pitch.</param>
        public void Reset(Vector2 initial)
        {
            // Reinitialization cannot replay displacement from a previous input connection.
            target = angles = initial;
        }

        /// <summary>Discards the unpresented tail when cursor capture is explicitly released.</summary>
        public void DiscardPending()
        {
            // Releasing the pointer stops camera rotation at its current displayed pose.
            target = angles;
        }

        /// <summary>Integrates look over the elapsed frame, retaining the same response when frame durations vary.</summary>
        /// <param name="change">Finite yaw and pitch displacement already scaled by input sensitivity.</param>
        /// <param name="settings">Validated look limits and response options.</param>
        /// <param name="duration">Positive elapsed simulation time, shared with rate input and follow.</param>
        /// <returns>Displayed yaw and pitch, used by both the camera and movement heading.</returns>
        public Vector2 Advance(Vector2 change, in PlayerCameraSettings settings, float duration)
        {
            // Clamp the requested pose, so smoothing cannot accumulate hidden motion beyond a limit.
            Vector2 previousTarget = target;
            target += change;
            target.y = Mathf.Clamp(target.y, settings.PitchLimits.x, settings.PitchLimits.y);
            if (settings.LimitYaw)
                target.x = Mathf.Clamp(target.x, settings.YawLimits.x, settings.YawLimits.y);
            else
            {
                // Shift both representations together to cross the yaw seam without reversing the turn.
                float turns = Mathf.Floor((target.x + 180f) / 360f) * 360f;
                target.x -= turns;
                previousTarget.x -= turns;
                angles.x -= turns;
            }

            // A delta describes movement across the interval, not a target held at its final pose for the whole frame.
            if (settings.SmoothLook)
            {
                float response = settings.AdaptiveLookSmoothing
                    ? 1f + change.magnitude / (duration * settings.LookSmoothingSpeed) : 1f;
                PlayerCameraDamping.GetWeights((double)duration * response / settings.LookSmoothingTime,
                    out float targetWeight, out float motionWeight);
                angles += (previousTarget - angles) * targetWeight + (target - previousTarget) * motionWeight;
            }
            else
                angles = target;
            return angles;
        }

        #endregion

        #endregion
    }
}
