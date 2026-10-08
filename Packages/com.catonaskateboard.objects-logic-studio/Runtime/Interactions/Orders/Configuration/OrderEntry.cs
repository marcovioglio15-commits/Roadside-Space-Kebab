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
        [Tooltip("Number of individual orders activated by this draw. Every unit occupies a separate board slot.")]
        public int Quantity = 1;
        [Tooltip("Relative probability in Weighted Random mode. Zero excludes this candidate.")]
        public float Weight = 1f;
        [Header("Identity")]
        [Tooltip("Identity flags identifying items that fulfil this order, captured at consumption time.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require all selected flags on the same consumed item. Otherwise any selected flag qualifies.")]
        public bool RequireAllFlags;
        [Header("Recipe Modifiers")]
        [Tooltip("Add independently drawn variants requiring flags from ingredients inserted into a completed Assembly Product.")]
        public bool UseRecipeModifiers;
        [Tooltip("Keep the base order as a candidate alongside its variants. Base orders retain their normal identity filters; matching variant tickets are fulfilled first.")]
        public bool IncludeBaseOrder = true;
        [Tooltip("Recipe-specific names, board text, weights and ingredient flag filters. Each variant inherits this order's identity flags and quantity.")]
        public OrderRecipeVariant[] Variants = Array.Empty<OrderRecipeVariant>();

        #endregion
    }
}
