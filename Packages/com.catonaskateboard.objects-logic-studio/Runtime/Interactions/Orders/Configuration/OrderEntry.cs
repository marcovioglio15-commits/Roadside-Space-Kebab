using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines one named order candidate and the identity accepted for each of its units.</summary>
    [Serializable]
    public sealed class OrderEntry
    {
        #region Fields

        [Header("Order")]
        [Tooltip("Unique order name offered by Consumption dialogue filters on this object.")]
        public string Name = "Order";
        [Tooltip("Order text shown until the object despawns. Completion adds strikethrough without freeing its slot.")]
        [TextArea(2, 5)]
        public string Text = "Order";
        [Tooltip("Independent board typography and paragraph spacing. Combo members retain their individual overrides.")]
        public OrderTextStyle TextStyle = new OrderTextStyle();
        [Tooltip("Number of individual orders activated by this draw. Every unit occupies a separate board slot.")]
        public int Quantity = 1;
        [Tooltip("Relative probability in Weighted Random mode. Zero excludes this candidate.")]
        [Range(0.01f, 10f)]
        public float Weight = 1f;
        [Header("Draw Limits")]
        [Tooltip("Randomize required draws once per spawn. Base and variants without their own limits share this allowance; each draw adds Quantity tickets.")]
        public bool RandomRequiredDraws;
        [Tooltip("Guaranteed draws before optional selection. Zero makes this order optional.")]
        public int MinimumDraws;
        [Tooltip("Inclusive range of guaranteed draws sampled once per spawn, before the total unit budget is selected.")]
        public Vector2Int RequiredDrawRange = new Vector2Int(0, 1);
        [Tooltip("Maximum total draws of this order and variants without their own limits, including guaranteed draws. Independently limited variants are excluded from this allowance.")]
        public int MaximumDraws = 1;
        [Header("Identity")]
        [Tooltip("Identity flags identifying items that fulfil this order, captured at consumption time.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require all selected flags on the same consumed item. Otherwise any selected flag qualifies.")]
        public bool RequireAllFlags;
        [Header("Recipe Modifiers")]
        [Tooltip("Add independently drawn variants requiring or excluding ingredient flags in a completed Assembly Product.")]
        public bool UseRecipeModifiers;
        [Tooltip("Keep the base order as a candidate alongside its variants. Base orders retain their normal identity filters; matching variant tickets are fulfilled first.")]
        public bool IncludeBaseOrder = true;
        [Tooltip("Recipe-specific names, board text, weights and ingredient filters. Variants inherit this order's identity and may override their draw limits and quantity.")]
        public OrderRecipeVariant[] Variants = Array.Empty<OrderRecipeVariant>();
        [Tooltip("Optional combinations of compatible recipe variants, with independent quantity, limits and selection weight.")]
        public OrderComboSettings Combos = new OrderComboSettings();

        #endregion
    }
}
