using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects legacy aiming, exact centre-collider input or automatic proximity activation.</summary>
    public enum TriggerAnimationActivation { Action, CenterCollider, Proximity }

    /// <summary>Controls an active proximity animation when the player leaves its exit radius.</summary>
    public enum TriggerAnimationExit { Pause, Reset, Finish, ReturnToStart }

    /// <summary>Defines two local transform states toggled by successive input interactions.</summary>
    [Serializable]
    public sealed class TriggerAnimationSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Action uses the existing aim settings. Center Collider requires a centre-screen hit. Proximity starts without an input action.")]
        public TriggerAnimationActivation Activation;
        [Tooltip("Player reach, aiming and obstruction settings for the input action.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Proximity")]
        [Tooltip("Player distance from the object pivot required to start the animation.")]
        [Range(0.1f, 20f)]
        public float ProximityDistance = 3f;
        [Tooltip("Player distance that ends proximity activation. Keep at least the start distance to prevent boundary flicker.")]
        [Range(0.1f, 25f)]
        public float ExitDistance = 3.2f;
        [Tooltip("Continue alternating endpoints while the player stays nearby. Automatic Return retains its own delay and duration.")]
        public bool Loop;
        [Tooltip("Pause in place, reset immediately, finish the current cycle, or animate back to the initial endpoint on exit.")]
        public TriggerAnimationExit OnExit;
        [Tooltip("Resume the paused segment and its elapsed time on reentry. Otherwise restart from the configured initial endpoint.")]
        public bool ResumeOnReentry;
        [Header("Animation")]
        [Tooltip("Object or child selected in the hierarchy menu. Empty selects the interaction root.")]
        public string Path = string.Empty;
        [Tooltip("Local transform pose used at the first endpoint.")]
        public PlayerToolPose StateA = PlayerToolPose.Identity;
        [Tooltip("Local transform pose used at the second endpoint.")]
        public PlayerToolPose StateB = new PlayerToolPose { Rotation = new Vector3(0f, 90f, 0f), Scale = Vector3.one };
        [Tooltip("Begin at State B instead of State A when activated.")]
        public bool StartAtB;
        [Tooltip("Seconds for each eased transition. Zero applies the destination immediately.")]
        [Range(0.01f, 10f)]
        public float Duration = 0.35f;
        [Tooltip("Rotation when entering State B. Clockwise uses negative angles around each changed local axis; Counter Clockwise uses positive angles.")]
        public TransformRotationDirection ForwardRotation = TransformRotationDirection.Shortest;
        [Tooltip("Rotation when returning to State A. Choose independently to reverse direction or keep turning the same way.")]
        public TransformRotationDirection ReturnRotation = TransformRotationDirection.Shortest;
        [Header("Automatic Return")]
        [Tooltip("Return to the configured initial state after entering the other state. The wait begins when the outward transition completes.")]
        public bool AutoReturn;
        [Tooltip("Scaled seconds spent at the altered state before returning automatically. Locks pause this countdown.")]
        [Range(0f, 30f)]
        public float ReturnDelay = 1f;
        [Tooltip("Seconds for the automatic return transition. Zero restores the initial state immediately.")]
        [Range(0.01f, 10f)]
        public float AutoReturnDuration = 0.35f;
        [Tooltip("Rotation direction used only by the automatic return, independent of manual toggles.")]
        public TransformRotationDirection AutoReturnRotation = TransformRotationDirection.Shortest;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates reusable endpoints independently of the destination prefab hierarchy.</summary>
        /// <param name="warning">Receives invalid timing, targeting or pose values.</param>
        /// <returns>True when both endpoints can be interpolated safely.</returns>
        public bool TryValidate(out string warning)
        {
            // Invalid authored values remain available for correction instead of being normalized.
            warning = "Configure animation targeting.";
            if (Activation == TriggerAnimationActivation.Action && (Target == null || !Target.TryValidate(out warning)))
                return false;
            warning = "Choose a supported activation mode and finite positive reach. Proximity exit distance must be at least its start distance.";
            if (Activation is not (TriggerAnimationActivation.Action or TriggerAnimationActivation.CenterCollider or TriggerAnimationActivation.Proximity)
                || Activation == TriggerAnimationActivation.CenterCollider && (Target == null || !float.IsFinite(Target.Distance) || Target.Distance <= 0f)
                || Activation == TriggerAnimationActivation.Proximity && (!float.IsFinite(ProximityDistance) || ProximityDistance <= 0f
                    || !float.IsFinite(ExitDistance) || ExitDistance < ProximityDistance
                    || OnExit is not (TriggerAnimationExit.Pause or TriggerAnimationExit.Reset or TriggerAnimationExit.Finish or TriggerAnimationExit.ReturnToStart)))
                return false;
            warning = "Choose a hierarchy target, finite poses with positive scales, a non-negative duration and valid rotation directions.";
            if (Path == null || !StateA.IsValid() || !StateB.IsValid() || !float.IsFinite(Duration) || Duration < 0f
                || !TransformRotation.IsValid(ForwardRotation) || !TransformRotation.IsValid(ReturnRotation))
                return false;
            warning = "Automatic return needs finite non-negative delay and duration, and a supported rotation direction.";
            if (AutoReturn && (!float.IsFinite(ReturnDelay) || ReturnDelay < 0f)
                || (AutoReturn || Activation == TriggerAnimationActivation.Proximity && OnExit == TriggerAnimationExit.ReturnToStart)
                && (!float.IsFinite(AutoReturnDuration) || AutoReturnDuration < 0f || !TransformRotation.IsValid(AutoReturnRotation)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
