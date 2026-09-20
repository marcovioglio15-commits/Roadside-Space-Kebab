namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Retains one press and one support opportunity without allowing an extra jump after takeoff.</summary>
    public struct PlayerJumpState
    {
        #region State

        private double pressTime;
        private double supportTime;
        private bool hasPress;
        private bool hasSupport;

        #endregion

        #region Methods

        #region Decision

        /// <summary>Consumes a valid press once; call before the frame's vertical integration.</summary>
        /// <param name="pressed">New press latched by the input bridge, not a held value.</param>
        /// <param name="grounded">Support reported by the preceding movement while not ascending.</param>
        /// <param name="time">Monotonic scaled simulation time in seconds.</param>
        /// <param name="settings">Validated jump snapshot.</param>
        /// <returns>True when this tick may launch the player.</returns>
        public bool TryStart(bool pressed, bool grounded, double time, PlayerJumpSettings settings)
        {
            // A disabled jump cannot retain a press for a later configuration change.
            if (!settings.Enabled)
            {
                Reset();
                return false;
            }

            // Only actual support rearms the jump; coyote time cannot rearm a spent takeoff.
            if (grounded)
            {
                supportTime = time;
                hasSupport = true;
            }
            if (pressed)
            {
                pressTime = time;
                hasPress = true;
            }

            // Zero buffer still permits the current tick, while old airborne presses expire.
            if (hasPress && time - pressTime > settings.BufferTime)
                hasPress = false;
            if (!hasPress || !hasSupport || (!grounded && time - supportTime > settings.CoyoteTime))
                return false;

            hasPress = false;
            hasSupport = false;
            return true;
        }

        #endregion

        #region Reset

        /// <summary>Clears timing opportunities at activation boundaries without reading a clock or allocating.</summary>
        public void Reset()
        {
            // Timestamps need no sentinel value because the flags explicitly state whether they are usable.
            hasPress = false;
            hasSupport = false;
        }

        #endregion

        #endregion
    }
}
