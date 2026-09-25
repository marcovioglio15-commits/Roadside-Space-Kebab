using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares player targeting and output placement between inventory interactions.</summary>
    [Serializable]
    public sealed class TransferTargetSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Maximum interaction distance from the tagged player, in metres.")]
        public float Distance = 3f;
        [Tooltip("Select an object near the view centre or under the unlocked cursor.")]
        public HoverTargetMode Mode;
        [Tooltip("Allowed screen distance from view centre as a fraction of viewport height.")]
        public float CenterRadius = 0.15f;
        [Tooltip("Local selection point; visible owned collider centres are also eligible.")]
        public Vector3 Offset;
        [Tooltip("Solid layers that obstruct interaction. Player and target geometry are excluded.")]
        public LayerMask ObstacleMask = ~0;
        [Tooltip("Display the selection anchor, reach and output placement when selected.")]
        public bool DrawGizmos = true;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects invalid targeting without changing authored values.</summary>
        /// <param name="warning">Receives the first invalid value.</param>
        /// <returns>True when player targeting is usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Centre tolerance has no effect on exact cursor selection.
            warning = string.Empty;
            if (!InteractionValues.Positive(Distance) || !InteractionValues.Finite(Offset)
                || Mode is not (HoverTargetMode.ViewCenter or HoverTargetMode.Cursor)
                || Mode == HoverTargetMode.ViewCenter && (!InteractionValues.Positive(CenterRadius) || CenterRadius > 1f))
                warning = "Use a positive finite distance, finite offset and centre radius in (0, 1].";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
