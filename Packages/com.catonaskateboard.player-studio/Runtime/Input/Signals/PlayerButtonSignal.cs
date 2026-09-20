namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Transfers latched button edges to one consumer, including a press and release between motor ticks.</summary>
    public readonly struct PlayerButtonSignal
    {
        #region Properties

        /// <summary>The action and its owner were active when this signal was consumed.</summary>
        public bool IsActive { get; }

        /// <summary>The action was disabled or reconnected since consumption; drop older buffered requests.</summary>
        public bool Interrupted { get; }

        /// <summary>At least one new press was received since the preceding consumption.</summary>
        public bool Pressed { get; }

        /// <summary>At least one release was received in the same interval.</summary>
        public bool Released { get; }

        /// <summary>Most recent held state; it never causes a new jump by itself.</summary>
        public bool Held { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Copies edge flags before the bridge clears them for the next motor tick.</summary>
        /// <param name="pressed">Whether a press occurred.</param>
        /// <param name="released">Whether a release occurred.</param>
        /// <param name="held">Current held state.</param>
        /// <param name="interrupted">Whether an ownership interruption invalidated older requests.</param>
        public PlayerButtonSignal(bool pressed, bool released, bool held, bool interrupted)
        {
            // A quick tap can set both edge flags while leaving Held false.
            IsActive = true;
            Interrupted = interrupted;
            Pressed = pressed;
            Released = released;
            Held = held;
        }

        #endregion

        #endregion
    }
}
