using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Adds a separately selectable recipe variant while retaining its parent order's identity and quantity.</summary>
    [Serializable]
    public sealed class OrderRecipeVariant
    {
        #region Fields

        [Header("Recipe Variant")]
        [Tooltip("Unique catalog name selected independently by Consumption dialogue filters.")]
        public string Name = "Variant";
        [Tooltip("Board text for this variant. The parent order's quantity still occupies separate slots.")]
        [TextArea(2, 5)]
        public string Text = "Variant";
        [Tooltip("Display the base order text followed by this variant's text, separated by a space. Disable to use only the variant text.")]
        public bool AppendToBaseText = true;
        [Tooltip("Independent selection weight when Available Orders uses Weighted Random. Zero excludes this variant.")]
        public float Weight = 1f;
        [Header("Draw Limits")]
        [Tooltip("Give this variant independent draw limits instead of sharing the parent order's draw allowance. Quantity and identity still come from the parent.")]
        public bool UseDrawLimits;
        [Tooltip("Randomize the required draws once per spawn within Required Draw Range.")]
        public bool RandomRequiredDraws;
        [Tooltip("Guaranteed draws of this variant before optional selection. Zero makes it optional.")]
        public int MinimumDraws;
        [Tooltip("Inclusive range of required draws, sampled before choosing the total unit budget.")]
        public Vector2Int RequiredDrawRange = new Vector2Int(0, 1);
        [Tooltip("Maximum draws of this variant, including required draws. Zero excludes it from extraction.")]
        public int MaximumDraws = 1;
        [Header("Recipe Modifiers")]
        [Tooltip("Ingredient identity flags captured when inserted into a completed Assembly Product. Product-root flags alone never satisfy these requirements.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require all selected modifier flags across the assembled ingredients. Otherwise any selected modifier qualifies.")]
        public bool RequireAllFlags = true;
        [Tooltip("Ingredient flags that must all be absent from the assembled product. These exclusions combine with the additive flags above.")]
        public ObjectFlag[] ExcludedFlags = Array.Empty<ObjectFlag>();

        #endregion
        #region Methods
        #region Matching

        /// <summary>Checks an immutable ingredient receipt captured before consumption disables the assembled product.</summary>
        /// <param name="ingredients">All inserted ingredient flags, or an empty array for non-assembled items.</param>
        /// <returns>True when the assembled recipe satisfies this variant's modifiers.</returns>
        internal bool Matches(ObjectFlag[] ingredients)
        {
            // Null marks a non-assembled receipt; an empty assembled receipt can still satisfy exclusions.
            if (ingredients == null)
                return false;
            foreach (ObjectFlag flag in ExcludedFlags)
                if (flag != null && Array.IndexOf(ingredients, flag) >= 0)
                    return false;
            if (Flags.Length == 0)
                return ExcludedFlags.Length > 0;
            foreach (ObjectFlag flag in Flags)
                if ((flag != null && Array.IndexOf(ingredients, flag) >= 0) != RequireAllFlags)
                    return !RequireAllFlags;
            return RequireAllFlags;
        }

        #endregion
        #region Validation

        /// <summary>Shares unique-name, display-text and weight checks with base order definitions.</summary>
        /// <param name="name">Candidate name used by dialogue filters.</param>
        /// <param name="text">Board display text.</param>
        /// <param name="weight">Independent selection weight.</param>
        /// <param name="names">Names already reserved in the catalog.</param>
        /// <param name="warning">Receives invalid metadata.</param>
        /// <returns>True when this candidate has complete, unambiguous metadata.</returns>
        internal static bool ValidateMetadata(string name, string text, float weight, HashSet<string> names, out string warning)
        {
            warning = "Orders and recipe variants need unique names, board text and finite, non-negative weights.";
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name) || string.IsNullOrWhiteSpace(text) || !float.IsFinite(weight) || weight < 0f)
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
