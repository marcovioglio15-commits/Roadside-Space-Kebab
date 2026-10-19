using CatOnASkateboard.StudioIdentity;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Represents one independent draw candidate without duplicating the parent order's identity filters.</summary>
    internal readonly struct OrderCandidate
    {
        #region Fields

        internal readonly OrderEntry Entry;
        internal readonly OrderRecipeVariant Variant;
        internal readonly OrderRecipeVariant[] Combo;
        internal readonly string Name;
        private readonly int comboQuantity;

        #endregion
        #region Properties

        /// <summary>Exact catalog key used for spawn progress and dialogue eligibility.</summary>
        /// <summary>Board text chosen at extraction, independent of later deliveries.</summary>
        internal string Text => OrderComboCandidates.Text(Entry, Variant, Combo);
        /// <summary>Candidate's base weight, multiplied by its membership weight in each weighted pool.</summary>
        internal float Weight => Combo != null ? Entry.Combos.Weight : Variant != null ? Variant.Weight : Entry.Weight;
        /// <summary>Number of board slots required by this candidate.</summary>
        internal int Quantity => Combo != null ? comboQuantity : Variant is { UseDrawLimits: true, UseQuantity: true } ? Variant.Quantity : Entry.Quantity;
        /// <summary>More specific recipes consume matching tickets before individual variants and base orders.</summary>
        internal int Specificity => Combo != null ? Combo.Length : Variant != null ? 1 : 0;

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
            Combo = null;
            Name = variant != null ? variant.Name : entry.Name;
            comboQuantity = 0;
        }

        /// <summary>Captures a compatible combination and its spawn-specific quantity.</summary>
        /// <param name="entry">Parent identity and combination policies.</param>
        /// <param name="combo">Distinct variants in catalog order.</param>
        /// <param name="quantity">Resolved whole-draw slot cost.</param>
        internal OrderCandidate(OrderEntry entry, OrderRecipeVariant[] combo, int quantity)
        {
            Entry = entry;
            Variant = null;
            Combo = combo;
            Name = OrderComboCandidates.Name(entry, combo);
            comboQuantity = quantity;
        }

        /// <summary>Requires every combo member's additive and subtractive recipe conditions.</summary>
        /// <param name="ingredients">Immutable assembly ingredient receipt.</param>
        /// <returns>True when every selected recipe modifier matches.</returns>
        internal bool MatchesRecipe(ObjectFlag[] ingredients)
        {
            if (Combo == null)
                return Variant == null || Variant.Matches(ingredients);
            foreach (OrderRecipeVariant variant in Combo)
                if (!variant.Matches(ingredients))
                    return false;
            return true;
        }

        #endregion
        #endregion
    }
}
