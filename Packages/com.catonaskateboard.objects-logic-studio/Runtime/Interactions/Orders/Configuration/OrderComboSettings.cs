using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Combines effective variant quantities or draw limits without changing their individual definitions.</summary>
    public enum OrderComboPolicy { RandomVariant, Highest, Lowest, Fixed }

    /// <summary>Defines compatible multi-variant orders that can be added to any catalog extraction pool.</summary>
    [Serializable]
    public sealed class OrderComboSettings
    {
        #region Fields

        [Header("Variant Combinations")]
        [Tooltip("Allow draws combining two or more compatible recipe variants. Their texts and ingredient requirements combine in catalog order.")]
        public bool Enabled;
        [Tooltip("Largest number of different variants in one combo. No variant is repeated within a combination.")]
        public int MaximumVariants = 2;
        [Tooltip("Default weight for each generated combo candidate. A pool entry can adjust a specific combo's weight.")]
        [Range(0f, 10f)]
        public float Weight = 1f;
        [Tooltip("Choose combo quantity from a random member, the greatest or smallest effective member quantity, or a fixed value. Random choices are sampled once per spawn.")]
        public OrderComboPolicy QuantityPolicy;
        [Tooltip("Slots required by each combo draw when Quantity Policy is Fixed.")]
        public int Quantity = 1;
        [Tooltip("Shared limits for this order's combo candidates, derived from a random eligible member, highest or lowest member limits, or fixed values. Parent limits supply members without overrides.")]
        public OrderComboPolicy DrawLimitsPolicy = OrderComboPolicy.Fixed;
        [Tooltip("Guaranteed total combo draws for this base order, not a reservation for every possible combination.")]
        public int MinimumDraws;
        [Tooltip("Randomize the shared guaranteed combo count once per spawn within Required Draw Range.")]
        public bool RandomRequiredDraws;
        [Tooltip("Inclusive range of required draws shared by all compatible combos of this order.")]
        public Vector2Int RequiredDrawRange = new Vector2Int(0, 1);
        [Tooltip("Maximum total combo draws for this base order. Each draw may select a different compatible combination; ordinary variants retain their own allowances.")]
        public int MaximumDraws = 1;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Rejects unsupported policies and invalid limits without rewriting the catalog.</summary>
        /// <param name="warning">Receives invalid combination settings.</param>
        /// <returns>True when the disabled or enabled combo configuration is usable.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Combos need at least two allowed variants, a finite non-negative weight, positive quantity and valid draw-limit policies.";
            if (MaximumVariants < 2 || !float.IsFinite(Weight) || Weight < 0f
                || !ValidPolicy(QuantityPolicy) || !ValidPolicy(DrawLimitsPolicy)
                || QuantityPolicy == OrderComboPolicy.Fixed && Quantity <= 0
                || DrawLimitsPolicy == OrderComboPolicy.Fixed && !OrderDrawGroup.ValidLimits(MinimumDraws, MaximumDraws, RandomRequiredDraws, RequiredDrawRange))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Accepts supported authored policies without runtime type inspection.</summary>
        /// <param name="policy">Serialized quantity or draw-limit policy.</param>
        /// <returns>True for a supported combination policy.</returns>
        private static bool ValidPolicy(OrderComboPolicy policy)
        {
            return policy is OrderComboPolicy.RandomVariant or OrderComboPolicy.Highest or OrderComboPolicy.Lowest or OrderComboPolicy.Fixed;
        }

        #endregion
        #endregion
    }
}
