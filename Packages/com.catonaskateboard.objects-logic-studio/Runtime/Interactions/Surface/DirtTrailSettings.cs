using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses which collisions and sliding contacts can leave fading food or liquid residue.</summary>
    [Serializable]
    public sealed class DirtTrailSettings
    {
        #region Fields

        [Header("Contact")]
        [Tooltip("Surfaces on these layers may receive deposits.")]
        public LayerMask Layers = ~0;
        [Tooltip("Also require the receiver to carry one of the selected identity flags.")]
        public bool FilterFlags;
        [Tooltip("Alternative identity flags on the receiving object or its parent.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Minimum relative collision speed in metres per second for the first deposit.")]
        public float MinimumSpeed = 0.25f;
        [Tooltip("Continue leaving marks while the object slides or rolls along a surface.")]
        public bool Sliding = true;
        [Tooltip("Minimum movement between consecutive sliding deposits in metres.")]
        public float Spacing = 0.045f;
        [Tooltip("Minimum seconds between accepted contact deposits.")]
        public float Interval = 0.1f;
        [Header("Deposit")]
        [Tooltip("Shape, colour composition, liquidity, spread and gradual fade shared with Spray Sauce.")]
        public SurfaceTrailSettings Trail = new SurfaceTrailSettings();

        #endregion
        #region Methods
        #region Validation

        /// <summary>Validates only enabled contact restrictions and the shared deposit appearance.</summary>
        /// <param name="warning">Receives missing flags or invalid contact/mark settings.</param>
        /// <returns>True when contact emission can run.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Choose receiving layers, a finite non-negative impact speed, and positive interval and sliding spacing.";
            if (Layers.value == 0 || !SurfaceValues.Nonnegative(MinimumSpeed) || !InteractionValues.Positive(Interval)
                || Sliding && !InteractionValues.Positive(Spacing))
                return false;
            if (FilterFlags && !ObjectFlagRules.TryValidate(Flags, false, out warning))
                return false;
            warning = "Configure the surface deposit.";
            return Trail != null && Trail.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
