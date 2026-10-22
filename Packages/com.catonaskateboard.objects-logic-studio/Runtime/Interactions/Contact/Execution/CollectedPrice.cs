using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains session earnings independently of HUD activation and deferred customer presentation.</summary>
    public static class CollectedPrice
    {
        #region Properties

        /// <summary>Paid earnings plus pending receipts; forfeited pending amounts are removed when their lifetime ends.</summary>
        public static decimal Total { get; private set; }
        /// <summary>Earnings released for presentation; unrelated pending batches remain hidden.</summary>
        public static decimal DisplayedTotal { get; private set; }
        /// <summary>Raised only when the displayed amount changes, avoiding per-frame text allocation.</summary>
        public static event Action Changed;
        /// <summary>Session identity used to discard retained batches before a new Play session.</summary>
        internal static int Generation { get; private set; }

        #endregion

        #region Methods

        #region Accounting

        /// <summary>Accepts finite nonnegative prices within the authoring slider's supported range.</summary>
        /// <param name="price">Recipe or order unit price.</param>
        /// <returns>True for a price from zero to ten thousand.</returns>
        internal static bool Valid(float price)
        {
            // Use the same domain for recipe and catalog validation before decimal conversion.
            return float.IsFinite(price) && price >= 0f && price <= 10000f;
        }

        /// <summary>Converts authored prices once per receipt using two decimal places.</summary>
        /// <param name="price">Validated recipe or order price.</param>
        /// <returns>The monetary value without binary floating-point accumulation.</returns>
        internal static decimal Amount(float price)
        {
            // Rounding each authored unit price avoids drift across repeated deliveries.
            return decimal.Round((decimal)price, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Records a successful receipt without exposing another collector's pending balance.</summary>
        /// <param name="amount">Nonnegative earnings committed by one exact consumption.</param>
        internal static void Collect(decimal amount)
        {
            // Pending money belongs to its collector until release or forfeiture.
            Total += amount;
        }

        /// <summary>Removes one unfinished collector's pending money without touching completed payouts or HUD text.</summary>
        /// <param name="amount">Previously recorded pending amount belonging to the ending lifetime.</param>
        internal static void Discard(decimal amount)
        {
            // Each collector clears its private pending balance before requesting this adjustment.
            Total -= amount;
        }

        /// <summary>Releases one collector's completed batch to every existing counter.</summary>
        /// <param name="amount">Previously collected amount becoming visible.</param>
        internal static void Publish(decimal amount)
        {
            // Zero-price orders complete normally without allocating a new HUD string.
            if (amount <= 0m)
                return;
            DisplayedTotal += amount;
            Changed?.Invoke();
        }

        #endregion

        #region Play Entry

        /// <summary>Starts each Play session from zero, including when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Keep retained collectors and HUDs from reusing a previous Play session's balance.
            Total = DisplayedTotal = 0m;
            Changed = null;
            Generation++;
        }

        #endregion

        #endregion
    }
}
