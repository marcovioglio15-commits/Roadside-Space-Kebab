using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses the modifying object's pivot or the scene origin for preparation coordinates.</summary>
    public enum ContactPoseSpace { Owner, World }

    /// <summary>Configures alignment, confirmation and two-endpoint motion before contact effects begin.</summary>
    [Serializable]
    public sealed class ContactPreparationSettings
    {
        #region Fields

        [Header("Snap")]
        [Tooltip("Align the contacted item with the owner's pivot before modifying it. Its body is temporarily kinematic.")]
        public bool Snap;
        [Tooltip("Reference for the snap position and rotation. Owner follows the modifying object's pivot; World uses scene coordinates.")]
        public ContactPoseSpace SnapSpace;
        [Tooltip("Destination position in the selected snap space.")]
        public Vector3 Position;
        [Tooltip("Destination Euler orientation in the selected snap space.")]
        public Vector3 Rotation;
        [Tooltip("Apply the snap immediately instead of interpolating it.")]
        public bool Instant = true;
        [Tooltip("Seconds to interpolate the contacted item into its snap pose.")]
        public float Duration = 0.35f;
        [Tooltip("After alignment, wait for the interaction's bound Button before starting preparation or effects.")]
        public bool RequireInput;
        [Header("Preparation Animation")]
        [Tooltip("Move an existing transform from State A to State B before applying the modification.")]
        public bool Animate;
        [Tooltip("Animate the contacted item instead of the interaction owner. Its body and Grab are suspended until the animation and optional return finish.")]
        public bool AnimateOther;
        [Tooltip("Hierarchy target relative to the selected participant. Empty selects its root; a dynamic owner must use a visual child.")]
        public string Path = string.Empty;
        [Tooltip("Reference for both animation endpoints. Owner uses the modifying object's pivot, captured before motion when animating that pivot itself. World uses scene coordinates.")]
        public ContactPoseSpace AnimationSpace;
        [Tooltip("First endpoint in the selected animation space. Scale is the animated transform's local scale.")]
        public PlayerToolPose StateA = PlayerToolPose.Identity;
        [Tooltip("Final endpoint in the selected animation space. Scale is the animated transform's local scale.")]
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
        #region Properties

        /// <summary>Whether preparation owns the counterpart's motion throughout the transaction.</summary>
        internal bool HoldsOther => Snap || Animate && AnimateOther;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled stages without modifying authored poses or durations.</summary>
        /// <param name="warning">Receives an invalid snap or animation setting.</param>
        /// <returns>True when each enabled stage can run.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Snap needs finite offsets and a positive interpolation duration.";
            if (Snap && (SnapSpace is not (ContactPoseSpace.Owner or ContactPoseSpace.World)
                || !InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation) || !Instant && !InteractionValues.Positive(Duration)))
                return false;
            warning = "Preparation animation needs valid poses, non-negative finite durations and supported rotation directions.";
            if (Animate && (AnimationSpace is not (ContactPoseSpace.Owner or ContactPoseSpace.World)
                || Path == null || !StateA.IsValid() || !StateB.IsValid() || !float.IsFinite(AnimationDuration) || AnimationDuration < 0f
                || !TransformRotation.IsValid(ForwardRotation) || Return && (!float.IsFinite(ReturnDuration) || ReturnDuration < 0f || !TransformRotation.IsValid(ReturnRotation))))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks the chosen participant's existing hierarchy before the transaction starts.</summary>
        /// <param name="item">Participant supplying the animation target.</param>
        /// <param name="warning">Receives an unavailable or independently owned target.</param>
        /// <returns>True when animation is disabled or its target can be controlled safely.</returns>
        internal bool ValidateTarget(ObjectItem item, out string warning)
        {
            warning = string.Empty;
            if (!Animate)
                return true;
            Transform target = item != null ? PlayerHierarchy.Resolve(item.transform, Path) : null;
            if (target == null || !item.Owns(target) || target.TryGetComponent(out Rigidbody body)
                && !body.isKinematic && (!AnimateOther || target != item.transform))
                warning = "Select an owned animation transform; dynamic owner bodies and nested bodies require a visual child. The contacted root can be animated with temporary physics suspension.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
