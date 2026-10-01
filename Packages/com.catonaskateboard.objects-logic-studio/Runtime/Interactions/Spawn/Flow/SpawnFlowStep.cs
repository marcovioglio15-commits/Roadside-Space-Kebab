using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses offsets in the spawn frame or absolute scene coordinates.</summary>
    public enum SpawnFlowSpace { Relative, World }

    /// <summary>Owns one prefab's placement, interaction completion and arrival/departure keyframes.</summary>
    [Serializable]
    public sealed class SpawnFlowStep
    {
        #region Fields

        [Header("Spawn Step")]
        [Tooltip("Name of this spawn template.")]
        public string Name = "Spawn";
        [Tooltip("Active prefab root created for this step.")]
        public GameObject Prefab;
        [Tooltip("Interaction on this prefab whose successful completion starts departure. Dialogue must finish its final page; interruption does not count.")]
        public ObjectInteraction Completion;
        [Tooltip("Request a separate dialogue after arrival, without a start action, distance or sight requirement.")]
        public bool ArrivalDialogueEnabled;
        [Tooltip("Dialogue component on this prefab to request once after walk-in. Its advance action still controls pages.")]
        public ObjectDialogue ArrivalDialogue;
        [Tooltip("Play the configured new-customer sound when the arrival dialogue actually opens.")]
        public bool NewCustomerSound;
        [Tooltip("Relative chance used only by Weighted Random selection.")]
        public float Weight = 1f;
        [Tooltip("Scaled seconds to wait before creating this step's instance.")]
        public float Delay;
        [Tooltip("Spawn origin relative to the persistent flow object's transform.")]
        public Vector3 Position;
        [Tooltip("Spawn orientation in degrees relative to the flow object.")]
        public Vector3 Rotation;
        [Tooltip("Optional transform path performed before interactions become available.")]
        public SpawnFlowAnimation WalkIn = new SpawnFlowAnimation();
        [Tooltip("Optional transform path performed after completion and before despawning.")]
        public SpawnFlowAnimation WalkOut = new SpawnFlowAnimation();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the exact prefab interaction and both transform paths.</summary>
        /// <param name="warning">Receives a missing source or invalid pose/timing.</param>
        /// <returns>True when the step can wait for its own instance's completion.</returns>
        public bool TryValidate(out string warning)
        {
            // A direct component reference distinguishes multiple dialogues on the same object.
            warning = "Select an active prefab with positive scale and an enabled completion interaction belonging to it.";
            if (!SpawnManagementSettings.Prefab(Prefab) || !Prefab.activeSelf || Completion == null
                || !PlayerToolPose.Read(Prefab.transform).IsValid()
                || !Completion.enabled || !Completion.transform.IsChildOf(Prefab.transform) || Completion is ObjectSpawnManager)
                return false;
            for (Transform branch = Completion.transform; branch != null; branch = branch.parent)
                if (!branch.gameObject.activeSelf)
                    return false;
            // Automatic arrival dialogue still belongs to the same generated prefab instance.
            if (ArrivalDialogueEnabled && (ArrivalDialogue == null || !ArrivalDialogue.enabled
                || !ArrivalDialogue.transform.IsChildOf(Prefab.transform)))
            {
                warning = "Choose an enabled arrival dialogue on the selected prefab.";
                return false;
            }
            warning = "Use finite placement, a nonnegative delay and a nonnegative selection weight.";
            if (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation)
                || !SpawnFlowPlan.Duration(Delay) || !SpawnFlowPlan.Duration(Weight))
                return false;
            warning = "Both animation settings must be present.";
            return WalkIn != null && WalkOut != null && WalkIn.TryValidate(out warning) && WalkOut.TryValidate(out warning);
        }

        #endregion
        #endregion
    }

    /// <summary>Stores transform destinations relative to the step's spawn origin and prefab scale.</summary>
    [Serializable]
    public sealed class SpawnFlowAnimation
    {
        #region Fields

        [Header("Walk Path")]
        [Tooltip("Run these keyframes while the object's interactions and physics are suspended.")]
        public bool Enabled;
        [Tooltip("World uses exact scene positions and rotations. Relative uses offsets rotated by the spawn orientation and scale multipliers.")]
        public SpawnFlowSpace Space;
        [Tooltip("Ordered destinations. Each Duration is travel time from the preceding pose, after its Delay. A zero-duration first frame sets an explicit starting pose.")]
        public SpawnFlowKeyframe[] Keyframes = Array.Empty<SpawnFlowKeyframe>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Rejects incomplete enabled paths without modifying their keyframes.</summary>
        /// <param name="warning">Receives the first unusable keyframe.</param>
        /// <returns>True when disabled or every frame has a valid pose and timing.</returns>
        public bool TryValidate(out string warning)
        {
            // Disabled paths preserve their authored data for later reuse.
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Enabled walk paths need keyframes with finite poses, positive scale and nonnegative timing.";
            if (Keyframes == null || Keyframes.Length == 0 || Space is not (SpawnFlowSpace.Relative or SpawnFlowSpace.World))
                return false;
            foreach (SpawnFlowKeyframe frame in Keyframes)
                if (frame == null || !frame.Pose.IsValid() || !SpawnFlowPlan.Duration(frame.Duration)
                    || !SpawnFlowPlan.Duration(frame.Delay) || !TransformRotation.IsValid(frame.Direction))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }

    /// <summary>Defines one destination and the time needed to reach it from the previous pose.</summary>
    [Serializable]
    public sealed class SpawnFlowKeyframe
    {
        #region Fields

        [Header("Keyframe")]
        [Tooltip("Destination in the path's selected coordinate space. World scale is absolute; Relative scale multiplies the prefab scale.")]
        public PlayerToolPose Pose = PlayerToolPose.Identity;
        [Tooltip("Scaled seconds held at the previous pose before movement starts.")]
        public float Delay;
        [Tooltip("Scaled seconds spent moving to this keyframe. Zero applies the destination immediately.")]
        public float Duration = 1f;
        [Tooltip("Choose signed clockwise/counterclockwise Euler travel, authored turns or the shortest orientation path.")]
        public TransformRotationDirection Direction = TransformRotationDirection.Shortest;

        #endregion
    }
}
