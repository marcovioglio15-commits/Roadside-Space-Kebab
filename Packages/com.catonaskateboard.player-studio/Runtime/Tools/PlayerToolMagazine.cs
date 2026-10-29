namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Keeps ammunition local to one player and refills only after a completed reload.</summary>
    internal sealed class PlayerToolMagazine
    {
        #region State

        private readonly int capacity;

        #endregion

        #region Properties

        /// <summary>Whether this weapon spends a finite supply of rounds.</summary>
        internal bool Limited => capacity > 0;
        /// <summary>Remaining rounds, or minus one for unlimited use.</summary>
        internal int Remaining { get; private set; }
        /// <summary>Whether at least one shot can be emitted.</summary>
        internal bool CanFire => !Limited || Remaining > 0;
        /// <summary>Whether a limited magazine has space for a refill.</summary>
        internal bool NeedsReload => Limited && Remaining < capacity;

        #endregion

        #region Methods

        #region Ammunition

        /// <summary>Starts with a full magazine without writing ammunition into the shared tool asset.</summary>
        /// <param name="tool">Validated tool supplying the optional magazine size.</param>
        internal PlayerToolMagazine(PlayerTool tool)
        {
            capacity = tool.IsWeapon && tool.LimitedMagazine ? tool.MagazineSize : 0;
            Remaining = Limited ? capacity : -1;
        }

        /// <summary>Spends one round only after a shot has actually been emitted.</summary>
        internal void Spend()
        {
            // Unlimited tools never change their sentinel value.
            if (Limited && Remaining > 0)
                Remaining--;
        }

        /// <summary>Commits the refill after every step in the reload combo has completed.</summary>
        internal void Refill()
        {
            Remaining = Limited ? capacity : -1;
        }

        #endregion

        #endregion
    }
}
