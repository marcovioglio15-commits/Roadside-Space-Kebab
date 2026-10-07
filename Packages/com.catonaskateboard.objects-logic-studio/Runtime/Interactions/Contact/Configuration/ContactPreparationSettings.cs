using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Configures alignment, confirmation and two-endpoint motion before contact effects begin.</summary>
    [Serializable]
    public sealed class ContactPreparationSettings
    {
        #region Fields

        [Header("Snap")]
        [Tooltip("Align the contacted item with the owner's pivot before modifying it. Its body is temporarily kinematic.")]
        public bool Snap;
        [Tooltip("Destination relative to the interaction owner's pivot.")]
        public Vector3 Position;
        [Tooltip("Destination Euler orientation relative to the interaction owner.")]
        public Vector3 Rotation;
        [Tooltip("Apply the snap immediately instead of interpolating it.")]
        public bool Instant = true;
        [Tooltip("Seconds to interpolate the contacted item into its snap pose.")]
        public float Duration = 0.35f;
        [Tooltip("After alignment, wait for the interaction's bound Button before starting preparation or effects.")]
        public bool RequireInput;
        [Header("Preparation Animation")]
        [Tooltip("Move an existing owner transform from State A to State B before applying the modification.")]
        public bool Animate;
        [Tooltip("Owner-relative hierarchy target. Empty selects the owner's root; dynamic bodies must use a visual child.")]
        public string Path = string.Empty;
        [Tooltip("Local transform endpoint before preparation starts.")]
        public PlayerToolPose StateA = PlayerToolPose.Identity;
        [Tooltip("Local transform endpoint reached before the modification begins.")]
        public PlayerToolPose StateB = PlayerToolPose.Identity;
        [Tooltip("Seconds spent moving from State A to State B.")]
        public float AnimationDuration = 0.35f;
        [Tooltip("Direction taken by each changed rotation axis during preparation.")]
        public TransformRotationDirection ForwardRotation;
        [Tooltip("After the effects finish, return to State A before releasing blocked interactions.")]
        public bool Return;
        [Tooltip("Seconds spent returning to State A after the modification.")]
        public float ReturnDuration = 0.35f;
        [Tooltip("Direction taken by each changed rotation axis during the return.")]
        public TransformRotationDirection ReturnRotation;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled stages without modifying authored poses or durations.</summary>
        /// <param name="warning">Receives an invalid snap or animation setting.</param>
        /// <returns>True when each enabled stage can run.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Snap needs finite offsets and a positive interpolation duration.";
            if (Snap && (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation) || !Instant && !InteractionValues.Positive(Duration)))
                return false;
            warning = "Preparation animation needs valid poses, non-negative finite durations and supported rotation directions.";
            if (Animate && (Path == null || !StateA.IsValid() || !StateB.IsValid() || !float.IsFinite(AnimationDuration) || AnimationDuration < 0f
                || !TransformRotation.IsValid(ForwardRotation) || Return && (!float.IsFinite(ReturnDuration) || ReturnDuration < 0f || !TransformRotation.IsValid(ReturnRotation))))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
