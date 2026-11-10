using System;
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
        /// <summary>Main slot text retained separately from variant paragraphs.</summary>
        internal string MainText => Variant == null || Variant.AppendToBaseText || string.IsNullOrWhiteSpace(Variant.Text)
            ? OrderComboCandidates.BaseText(Entry) : string.Empty;
        /// <summary>Whether this candidate adds one or more recipe paragraphs to its main slot.</summary>
        internal bool HasVariants => Variant != null || Combo != null;
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

        #region Delivery feedback

        /// <summary>Counts distinct delivered ingredient flags requested by the base order or its selected variants.</summary>
        /// <param name="ingredients">Unique ingredient flags captured from the assembled product.</param>
        /// <returns>The number of shared flags. Exclusions never contribute.</returns>
        internal int IngredientOverlap(ObjectFlag[] ingredients)
        {
            // Non-assembled items have no ingredient receipt.
            if (ingredients == null)
                return 0;
            int shared = 0;
            foreach (ObjectFlag flag in ingredients)
                if (flag != null && Requests(flag))
                    shared++;
            return shared;
        }

        /// <summary>Checks positive flags across the base order and every active recipe variant.</summary>
        /// <param name="flag">Ingredient flag from the delivered product.</param>
        /// <returns>True when at least one requirement asks for the flag.</returns>
        private bool Requests(ObjectFlag flag)
        {
            // Stop at the first requirement so repeated flags count once.
            if (Array.IndexOf(Entry.Flags, flag) >= 0 || Variant != null && Array.IndexOf(Variant.Flags, flag) >= 0)
                return true;
            if (Combo != null)
                foreach (OrderRecipeVariant variant in Combo)
                    if (Array.IndexOf(variant.Flags, flag) >= 0)
                        return true;
            return false;
        }

        #endregion
        #endregion
    }
}
