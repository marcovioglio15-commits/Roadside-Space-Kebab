using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the reference frame used by the held object's offset and rotation.</summary>
    public enum CarrySpace { Camera, Player }

    /// <summary>Configures targeting and the pose followed by one carried rigidbody.</summary>
    [Serializable]
    public sealed class GrabSettings
    {
        #region Fields

        [Header("Detection")]
        [Tooltip("Measure grab range from the Observer's tagged player, in metres.")]
        public float Distance = 3f;
        [Tooltip("Choose proximity to the view centre or an exact cursor hit on the object's colliders.")]
        public HoverTargetMode TargetMode;
        [Tooltip("Allowed distance from view centre as a fraction of viewport height.")]
        public float CenterRadius = 0.15f;
        [Tooltip("Local point used for grab range and view-centre targeting.")]
        public Vector3 TargetOffset;
        [Tooltip("Solid layers that prevent grabbing through walls. The player and this object are excluded.")]
        public LayerMask ObstacleMask = ~0;

        [Header("Carry Pose")]
        [Tooltip("Reference frame followed while carrying. Camera follows aim; Player follows the tagged root.")]
        public CarrySpace Space;
        [Tooltip("Carry position in metres along the reference frame's right, up and forward axes.")]
        public Vector3 Offset = new Vector3(0.35f, -0.2f, 1.5f);
        [Tooltip("Euler rotation in degrees relative to the selected carry frame.")]
        public Vector3 Rotation;
        [Tooltip("Move directly to the carry pose on pickup. Otherwise ease into it over Transition Duration.")]
        public bool Instant;
        [Tooltip("Seconds spent easing from the pickup pose to the moving carry pose.")]
        public float TransitionDuration = 0.2f;
        [Tooltip("Keep collisions with the world while carrying. Player collisions are always ignored.")]
        public bool WorldCollisions = true;
        [Tooltip("Maximum physical following speed in metres per second when world collisions are enabled.")]
        public float FollowSpeed = 12f;
        [Tooltip("Keep existing hover labels eligible for their normal detection while the object is carried.")]
        public bool ShowHover = true;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects invalid authored values without changing them.</summary>
        /// <param name="warning">Receives the first setting that needs correction.</param>
        /// <returns>True when targeting and the carry pose are usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Check only settings used by the selected modes.
            warning = string.Empty;
            if (!InteractionValues.Positive(Distance) || !InteractionValues.Finite(TargetOffset)
                || !InteractionValues.Finite(Offset) || !InteractionValues.Finite(Rotation))
                warning = "Grab needs a positive finite distance and finite position/rotation values.";
            else if (TargetMode is not (HoverTargetMode.ViewCenter or HoverTargetMode.Cursor)
                || Space is not (CarrySpace.Camera or CarrySpace.Player))
                warning = "Choose a supported grab target mode and carry space.";
            else if (TargetMode == HoverTargetMode.ViewCenter && (!InteractionValues.Positive(CenterRadius) || CenterRadius > 1f))
                warning = "Grab Center Radius must be greater than zero and at most one.";
            else if (!Instant && !InteractionValues.Positive(TransitionDuration))
                warning = "Grab Transition Duration must be positive and finite.";
            else if (WorldCollisions && !InteractionValues.Positive(FollowSpeed))
                warning = "Grab Follow Speed must be positive and finite.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
