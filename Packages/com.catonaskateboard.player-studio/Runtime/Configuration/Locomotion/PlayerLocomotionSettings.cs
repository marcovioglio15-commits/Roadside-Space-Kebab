namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores a validated movement configuration independently of its shared preset.</summary>
    public readonly struct PlayerLocomotionSettings
    {
        #region Properties

        /// <summary>Maximum requested speed in metres per second at full input magnitude.</summary>
        public float Speed { get; }

        /// <summary>Velocity change per second while a direction is requested, including turns.</summary>
        public float Acceleration { get; }

        /// <summary>Velocity change per second after directional input is released.</summary>
        public float Deceleration { get; }

        /// <summary>Optional downward motion captured with the planar rates.</summary>
        public PlayerGravitySettings Gravity { get; }

        /// <summary>Optional fixed-height jump captured with gravity.</summary>
        public PlayerJumpSettings Jump { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Copies values after validation; runtime movement never reads the preset every frame.</summary>
        /// <param name="speed">Validated maximum speed.</param>
        /// <param name="acceleration">Validated response rate while input is present.</param>
        /// <param name="deceleration">Validated braking rate after release.</param>
        /// <param name="gravity">Validated optional downward motion.</param>
        /// <param name="jump">Validated optional jump.</param>
        private PlayerLocomotionSettings(float speed, float acceleration, float deceleration, PlayerGravitySettings gravity, PlayerJumpSettings jump)
        {
            // Keep only immutable numeric data in the runtime snapshot.
            Speed = speed;
            Acceleration = acceleration;
            Deceleration = deceleration;
            Gravity = gravity;
            Jump = jump;
        }

        #endregion

        #region Validation

        /// <summary>Validates preset or draft values without correcting the supplied numbers.</summary>
        /// <param name="speed">Requested speed; zero intentionally prevents movement.</param>
        /// <param name="acceleration">Positive velocity response rate.</param>
        /// <param name="deceleration">Positive braking rate.</param>
        /// <param name="gravity">Previously validated optional gravity snapshot.</param>
        /// <param name="jump">Previously validated jump snapshot.</param>
        /// <param name="settings">Receives an immutable configuration on success.</param>
        /// <param name="warning">Receives the reason unusable values were refused.</param>
        /// <returns>True when every number is finite and each rate can be used by the movement calculation.</returns>
        public static bool TryCreate(float speed, float acceleration, float deceleration, PlayerGravitySettings gravity, PlayerJumpSettings jump,
            out PlayerLocomotionSettings settings, out string warning)
        {
            // Zero rates would make stopping or reaching the target undefined for this movement policy.
            settings = default;
            warning = string.Empty;
            if (!float.IsFinite(speed) || !float.IsFinite(acceleration) || !float.IsFinite(deceleration)
                || speed < 0f || acceleration <= 0f || deceleration <= 0f)
            {
                warning = "Use finite locomotion values: Speed at least zero, Acceleration and Deceleration greater than zero.";
                return false;
            }

            settings = new PlayerLocomotionSettings(speed, acceleration, deceleration, gravity, jump);
            return true;
        }

        #endregion

        #endregion
    }
}
