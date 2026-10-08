namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Represents one independent draw candidate without duplicating the parent order's identity filters.</summary>
    internal readonly struct OrderCandidate
    {
        #region Fields

        internal readonly OrderEntry Entry;
        internal readonly OrderRecipeVariant Variant;

        #endregion
        #region Properties

        /// <summary>Exact catalog key used for spawn progress and dialogue eligibility.</summary>
        internal string Name => Variant != null ? Variant.Name : Entry.Name;
        /// <summary>Board text chosen at extraction, independent of later deliveries.</summary>
        internal string Text => Variant != null ? Variant.Text : Entry.Text;
        /// <summary>Independent candidate weight used by weighted extraction.</summary>
        internal float Weight => Variant != null ? Variant.Weight : Entry.Weight;
        /// <summary>Number of board slots required by this candidate.</summary>
        internal int Quantity => Entry.Quantity;

        #endregion
        #region Methods
        #region Construction

        /// <summary>Combines base requirements with an optional recipe-specific selection.</summary>
        /// <param name="entry">Parent identity and unit configuration.</param>
        /// <param name="variant">Recipe selection, or null for the base order.</param>
        internal OrderCandidate(OrderEntry entry, OrderRecipeVariant variant = null)
        {
            Entry = entry;
            Variant = variant;
        }

        #endregion
        #endregion
    }
}
