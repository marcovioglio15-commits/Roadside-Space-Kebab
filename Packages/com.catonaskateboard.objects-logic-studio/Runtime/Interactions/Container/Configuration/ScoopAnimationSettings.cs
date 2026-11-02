using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses independent reference poses or a dispenser-first chain of relative offsets.</summary>
    public enum ScoopAnimationMode { ReferencePoses, RelativeChain }
    /// <summary>Reference used by an independent scoop keyframe.</summary>
    public enum ScoopPoseSpace { Dispenser, Initial, Carry }
    /// <summary>Interpolation applied during one scoop segment.</summary>
    public enum ScoopEasing { Linear, Smooth }

    /// <summary>Defines one target and its travel time from the previous target.</summary>
    [Serializable]
    public sealed class ScoopKeyframe
    {
        #region Fields

        [Header("Keyframe")]
        [Tooltip("Stable key assigned by the editor. Reordering preserves the fill marker.")]
        [HideInInspector]
        public string Id = string.Empty;
        [Tooltip("Short name shown in the sequence and fill marker menu.")]
        public string Name = "Scoop";
        [Tooltip("Reference used in Reference Poses mode.")]
        public ScoopPoseSpace Space;
        [Tooltip("Target offset. Relative Chain uses the dispenser for the first frame and the preceding target for subsequent frames.")]
        public Vector3 Position;
        [Tooltip("Interpolate rotation during this segment. Disabled preserves the preceding orientation.")]
        public bool Rotate;
        [Tooltip("Euler rotation relative to the reference or preceding keyframe.")]
        public Vector3 Rotation;
        [Tooltip("Scaled seconds to reach this target from the previous keyframe.")]
        [Range(0.01f, 10f)]
        public float Duration = 0.3f;
        [Tooltip("Movement interpolation for this segment.")]
        public ScoopEasing Easing = ScoopEasing.Smooth;
        [Tooltip("Interpolate the selected visual branch's local scale during this segment.")]
        public bool Resize;
        [Tooltip("Target local scale for the optional visual branch.")]
        public Vector3 Scale = Vector3.one;

        #endregion
    }

    /// <summary>Stores optional scoop movement and the exact fill commit boundary.</summary>
    [Serializable]
    public sealed class ScoopAnimationSettings
    {
        #region Fields

        [Header("Scoop Animation")]
        [Tooltip("Move the held container through the configured keyframes before resuming carry.")]
        public bool Enabled;
        [Tooltip("Use independent pose references or start at the dispenser offset and chain subsequent offsets.")]
        public ScoopAnimationMode Mode;
        [Tooltip("Consecutive target poses. Durations are measured from the preceding step.")]
        public ScoopKeyframe[] Keyframes = Array.Empty<ScoopKeyframe>();
        [Tooltip("Keyframe after which supply and fill change. Empty commits after the final keyframe, before any return segment.")]
        [HideInInspector]
        public string FillAfter = string.Empty;
        [Tooltip("Interpolate back to the world pose captured before this scoop.")]
        public bool ReturnToStart = true;
        [Tooltip("Scaled seconds for the optional return segment.")]
        [Range(0.01f, 10f)]
        public float ReturnDuration = 0.3f;
        [Tooltip("Extra scaled seconds allowed when a world obstacle prevents reaching a target. Expiry cancels the remaining movement.")]
        [Range(0.05f, 10f)]
        public float ObstacleMargin = 1f;
        [Tooltip("Optional visual child whose scale may change. Choose a branch without physics bodies or colliders.")]
        [HideInInspector]
        public string VisualPath = string.Empty;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active movement data without changing the sequence.</summary>
        /// <param name="warning">Receives an invalid pose, duration or fill marker.</param>
        /// <returns>True when the optional sequence is complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Scoop animation needs keyframes, a supported mode and positive finite durations.";
            if (Keyframes == null || Keyframes.Length == 0 || Mode is not (ScoopAnimationMode.ReferencePoses or ScoopAnimationMode.RelativeChain)
                || !InteractionValues.Positive(ObstacleMargin) || ReturnToStart && !InteractionValues.Positive(ReturnDuration))
                return false;
            bool found = string.IsNullOrEmpty(FillAfter);
            for (int index = 0; index < Keyframes.Length; index++)
            {
                ScoopKeyframe frame = Keyframes[index];
                if (frame == null || string.IsNullOrEmpty(frame.Id) || string.IsNullOrWhiteSpace(frame.Name)
                    || !InteractionValues.Positive(frame.Duration) || !InteractionValues.Finite(frame.Position)
                    || frame.Rotate && !InteractionValues.Finite(frame.Rotation)
                    || frame.Resize && (!InteractionValues.Finite(frame.Scale) || frame.Scale.x <= 0f || frame.Scale.y <= 0f || frame.Scale.z <= 0f)
                    || frame.Easing is not (ScoopEasing.Linear or ScoopEasing.Smooth)
                    || Mode == ScoopAnimationMode.ReferencePoses && frame.Space is not (ScoopPoseSpace.Dispenser or ScoopPoseSpace.Initial or ScoopPoseSpace.Carry))
                    return false;
                found |= frame.Id == FillAfter;
                for (int previous = 0; previous < index; previous++)
                    if (Keyframes[previous].Id == frame.Id)
                        return false;
            }
            warning = found ? string.Empty : "Select an existing Apply Fill After keyframe.";
            return found;
        }

        #endregion

        #endregion
    }
}
