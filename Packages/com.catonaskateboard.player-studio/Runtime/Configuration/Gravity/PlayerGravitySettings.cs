namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores optional downward gravity separately from planar movement rates.</summary>
    public readonly struct PlayerGravitySettings
    {
        #region Properties

        /// <summary>Whether the motor requests vertical movement and refreshes ground contact while idle.</summary>
        public bool Enabled { get; }

        /// <summary>Downward acceleration in metres per second squared.</summary>
        public float Acceleration { get; }

        /// <summary>Maximum downward speed in metres per second.</summary>
        public float TerminalSpeed { get; }

        /// <summary>Small downward request used after contact to keep the next Move checking the ground.</summary>
        public float GroundSpeed { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures validated values without referring to a shared asset.</summary>
        /// <param name="enabled">Whether vertical movement is requested.</param>
        /// <param name="acceleration">Positive downward acceleration.</param>
        /// <param name="terminalSpeed">Positive fall speed limit.</param>
        /// <param name="groundSpeed">Positive contact refresh speed no greater than the fall speed limit.</param>
        private PlayerGravitySettings(bool enabled, float acceleration, float terminalSpeed, float groundSpeed)
        {
            // Disabled settings retain their numbers for a later explicit edit.
            Enabled = enabled;
            Acceleration = acceleration;
            TerminalSpeed = terminalSpeed;
            GroundSpeed = groundSpeed;
        }

        #endregion

        #region Validation

        /// <summary>Validates only enabled gravity without repairing hidden or visible fields.</summary>
        /// <param name="enabled">Whether the numeric gravity fields will be used.</param>
        /// <param name="acceleration">Requested downward acceleration.</param>
        /// <param name="terminalSpeed">Requested fall speed limit.</param>
        /// <param name="groundSpeed">Requested downward contact refresh speed.</param>
        /// <param name="settings">Receives the immutable snapshot on success.</param>
        /// <param name="warning">Receives the first incompatible numeric requirement.</param>
        /// <returns>True when disabled gravity or valid active settings can be captured.</returns>
        public static bool TryCreate(bool enabled, float acceleration, float terminalSpeed, float groundSpeed,
            out PlayerGravitySettings settings, out string warning)
        {
            // Inactive options cannot prevent planar-only movement from being used.
            settings = default;
            warning = string.Empty;
            if (enabled && (!float.IsFinite(acceleration) || !float.IsFinite(terminalSpeed) || !float.IsFinite(groundSpeed)
                || acceleration <= 0f || terminalSpeed <= 0f || groundSpeed <= 0f || groundSpeed > terminalSpeed))
            {
                warning = "Use positive finite gravity values, with Ground Speed no greater than Terminal Speed.";
                return false;
            }

            settings = new PlayerGravitySettings(enabled, acceleration, terminalSpeed, groundSpeed);
            return true;
        }

        #endregion

        #endregion
    }
}
