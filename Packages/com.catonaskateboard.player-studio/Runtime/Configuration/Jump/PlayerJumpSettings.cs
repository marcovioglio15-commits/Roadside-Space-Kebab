namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Captures a fixed-height jump and optional timing allowances without reading assets during movement.</summary>
    public readonly struct PlayerJumpSettings
    {
        #region Properties

        /// <summary>Whether this motor may consume a press to jump.</summary>
        public bool Enabled { get; }

        /// <summary>Upward launch speed calculated from height and downward acceleration.</summary>
        public float LaunchSpeed { get; }

        /// <summary>Seconds a press may wait for landing; zero accepts only the current motor tick.</summary>
        public float BufferTime { get; }

        /// <summary>Seconds after leaving support during which one jump remains available.</summary>
        public float CoyoteTime { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Stores values after validation, including the derived launch speed.</summary>
        /// <param name="enabled">Whether jumping is active.</param>
        /// <param name="launchSpeed">Validated upward speed.</param>
        /// <param name="bufferTime">Nonnegative press retention interval.</param>
        /// <param name="coyoteTime">Nonnegative support grace interval.</param>
        private PlayerJumpSettings(bool enabled, float launchSpeed, float bufferTime, float coyoteTime)
        {
            // The runtime needs launch speed, not a repeated square root of the authored height.
            Enabled = enabled;
            LaunchSpeed = launchSpeed;
            BufferTime = bufferTime;
            CoyoteTime = coyoteTime;
        }

        #endregion

        #region Validation

        /// <summary>Refuses incompatible jumping without changing hidden or visible preset values.</summary>
        /// <param name="enabled">Whether the jump configuration is used.</param>
        /// <param name="height">Positive requested height above takeoff in free space.</param>
        /// <param name="bufferTime">Nonnegative time a press can wait for support.</param>
        /// <param name="coyoteTime">Nonnegative time support remains usable after leaving an edge.</param>
        /// <param name="gravity">Previously validated gravity required for this jump model.</param>
        /// <param name="settings">Receives usable settings, or disabled defaults.</param>
        /// <param name="warning">Receives a configuration warning without repairing any value.</param>
        /// <returns>True for disabled jumping or valid jumping with active gravity.</returns>
        public static bool TryCreate(bool enabled, float height, float bufferTime, float coyoteTime,
            PlayerGravitySettings gravity, out PlayerJumpSettings settings, out string warning)
        {
            // Disabled values stay in the preset; they do not block another movement mode.
            settings = default;
            warning = string.Empty;
            if (!enabled)
                return true;
            if (!gravity.Enabled || !float.IsFinite(height) || height <= 0f || !float.IsFinite(bufferTime)
                || bufferTime < 0f || !float.IsFinite(coyoteTime) || coyoteTime < 0f)
            {
                warning = "Jump requires active gravity, positive finite Height and nonnegative finite Buffer and Coyote times.";
                return false;
            }

            // Double precision prevents an unnecessary intermediate overflow before the square root.
            float launchSpeed = (float)System.Math.Sqrt(2d * gravity.Acceleration * height);
            if (!float.IsFinite(launchSpeed))
            {
                warning = "Jump Height and Gravity produce an unsupported launch speed. Reduce these values explicitly.";
                return false;
            }

            settings = new PlayerJumpSettings(true, launchSpeed, bufferTime, coyoteTime);
            return true;
        }

        #endregion

        #endregion
    }
}
