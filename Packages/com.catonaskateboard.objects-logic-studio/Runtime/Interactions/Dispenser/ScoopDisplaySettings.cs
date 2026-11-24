using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses local supply magnets or offsets from a triggering contact's docking slot.</summary>
    public enum ScoopMagnetMode { Custom, ContactMagnet }

    /// <summary>Configures floating stock around custom positions or a captured contact slot.</summary>
    [Serializable]
    public sealed class ScoopDisplaySettings
    {
        #region Fields

        [Header("Floating Supply")]
        [Tooltip("Use local magnets or the matching Modify By Contact slot of the item that loads this supply.")]
        public ScoopMagnetMode Placement;
        [Tooltip("Maximum portions this ingredient can supply in one scoop.")]
        [Range(1, 32)]
        public int ScoopLimit = 32;
        [Tooltip("Refill empty supply for a fresh compatible container. Also allows loading packs into Custom placement.")]
        public bool RestockForNewContainer;
        [Tooltip("Optional prefab used for restock. Leave empty to use the initial portion prefab.")]
        public GameObject RestockPrefab;
        [Tooltip("Uniform scale multiplier for the restock prefab. Use smaller values for densely spaced magnets.")]
        [Range(0.05f, 2f)]
        public float RestockScale = 1f;
        [Tooltip("Visible portions when supply is unlimited.")]
        [Range(1, 100)]
        public int VisibleCount = 1;
        [Tooltip("One position per visible portion. Relative to the dispenser pivot in Custom, or to the selected slot in Contact Magnet.")]
        public Vector3[] Magnets = { Vector3.zero };
        [Tooltip("Maximum drift in metres on each local axis.")]
        public Vector3 Drift = new Vector3(0.02f, 0.015f, 0.02f);
        [Tooltip("Approximate cycles per second of the floating motion.")]
        [Range(0.05f, 3f)]
        public float Speed = 0.45f;
        [Tooltip("Shape of the vertical bob. Higher values keep portions lower for longer and briefly reach the full height.")]
        [Range(1f, 5f)]
        public float BobCrestPower = 1f;
        [Tooltip("Remaining visible counts after successive scoops. A batch advances at most one step and may remove extra stock to reach it.")]
        public int[] Steps = Array.Empty<int>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks supply positions, floating motion and decreasing stock steps.</summary>
        /// <param name="count">Number of portions displayed after activation.</param>
        /// <param name="warning">Receives the first configuration problem.</param>
        /// <returns>True when the display can start.</returns>
        internal bool TryValidate(int count, out string warning)
        {
            // Every slot is required and all offsets must be safe for transform writes.
            warning = "Use a Scoop Limit from 1 to 32, valid supply positions, non-negative drift and positive floating timing and restock scale.";
            if (ScoopLimit is < 1 or > 32 || RestockForNewContainer && RestockPrefab != null && !InteractionValues.Positive(RestockScale)
                || count < 1 || Placement is not (ScoopMagnetMode.Custom or ScoopMagnetMode.ContactMagnet)
                || (Placement == ScoopMagnetMode.Custom || count > 1) && (Magnets == null || Magnets.Length != count)
                || !InteractionValues.Finite(Drift)
                || Drift.x < 0f || Drift.y < 0f || Drift.z < 0f || !InteractionValues.Positive(Speed)
                || !(BobCrestPower is >= 1f and <= 5f))
                return false;
            if (Placement == ScoopMagnetMode.Custom || count > 1)
                foreach (Vector3 magnet in Magnets)
                    if (!InteractionValues.Finite(magnet))
                        return false;

            // Steps describe visible stock, including zero for an exhausted interaction.
            warning = "Scoop steps must be non-negative and strictly decrease from the initial count.";
            if (Steps == null)
                return false;
            foreach (int step in Steps)
            {
                if (step < 0 || step >= count)
                    return false;
                count = step;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
