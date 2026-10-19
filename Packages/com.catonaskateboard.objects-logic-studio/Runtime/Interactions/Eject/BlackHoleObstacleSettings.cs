using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Guides attracted objects around solid geometry without disabling world collisions.</summary>
    [Serializable]
    public sealed class BlackHoleObstacleSettings
    {
        #region Fields

        [Header("Obstacle Response")]
        [Tooltip("Find routes through free space using each attracted object's shapes, then follow them while retaining world collisions.")]
        public bool Enabled = true;
        [Tooltip("Distance in metres between free-space search samples. Smaller values resolve tighter passages at greater search cost.")]
        [Range(0.1f, 1f)]
        public float PathSpacing = 0.3f;
        [Tooltip("Extra clearance in metres around obstacle edges and contact surfaces.")]
        [Range(0.001f, 0.3f)]
        public float Padding = 0.025f;
        [Tooltip("Fraction of the speed limit retained while following a route around obstacles.")]
        [Range(0.1f, 1f)]
        public float SlideSpeed = 0.75f;
        [Tooltip("Fraction of Escape Speed applied away from a surface when contact stops progress. Zero disables the rebound.")]
        [Range(0f, 1f)]
        public float Bounce = 0.2f;
        [Tooltip("Seconds without movement before replanning and separating from contact. Repeated failed searches wait progressively longer.")]
        [Range(0.1f, 2f)]
        public float RecoveryDelay = 0.35f;
        [Tooltip("Separation speed in metres per second used to recover from an overlap or stalled contact.")]
        [Range(0.1f, 20f)]
        public float EscapeSpeed = 4f;
        [Tooltip("Temporary friction shared by attracted bodies. Original collider materials return at launch, pickup or cancellation.")]
        [Range(0f, 1f)]
        public float Friction = 0.02f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks active obstacle response without rewriting configured values.</summary>
        /// <param name="warning">Receives an unusable clearance, timing or response value.</param>
        /// <returns>True when disabled or configured with finite usable values.</returns>
        public bool TryValidate(out string warning)
        {
            // Ratios remain bounded while distances and times can exceed their suggested slider ranges.
            warning = string.Empty;
            if (Enabled && (!InteractionValues.Positive(PathSpacing) || !InteractionValues.Positive(Padding)
                || !InteractionValues.Positive(RecoveryDelay) || !InteractionValues.Positive(EscapeSpeed)
                || !InteractionValues.Positive(SlideSpeed) || SlideSpeed > 1f
                || !float.IsFinite(Bounce) || Bounce < 0f || Bounce > 1f || !float.IsFinite(Friction) || Friction < 0f || Friction > 1f))
                warning = "Obstacle response needs positive finite clearance, timing and speed, with slide, bounce and friction ratios between zero and one.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
