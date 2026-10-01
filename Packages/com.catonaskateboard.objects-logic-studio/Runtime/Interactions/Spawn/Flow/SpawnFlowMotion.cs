using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Samples a spawn path without creating components or allocating during animation frames.</summary>
    internal sealed class SpawnFlowMotion
    {
        #region State

        private readonly Transform target;
        private readonly Vector3 origin;
        private readonly Quaternion orientation;
        private readonly Vector3 scale;
        private SpawnFlowAnimation path;
        private PlayerToolPose from;
        private int index;
        private float elapsed;

        #endregion

        #region Methods
        #region Animation

        /// <summary>Captures a stable spawn frame used by both entry and exit.</summary>
        /// <param name="target">Created instance root.</param>
        internal SpawnFlowMotion(Transform target)
        {
            // Later movement of the manager does not bend an animation already in progress.
            this.target = target;
            origin = target.position;
            orientation = target.rotation;
            scale = target.localScale;
        }

        /// <summary>Starts a path from the object's actual pose, including movement during interaction.</summary>
        /// <param name="animation">Validated walk-in or walk-out settings.</param>
        internal void Begin(SpawnFlowAnimation animation)
        {
            // Departure begins from the current pose rather than snapping back to the spawn origin.
            path = animation;
            index = 0;
            elapsed = 0f;
            from = path.Space == SpawnFlowSpace.World ? new PlayerToolPose
            {
                Position = target.position,
                Rotation = target.eulerAngles,
                Scale = target.localScale
            } : new PlayerToolPose
            {
                Position = Quaternion.Inverse(orientation) * (target.position - origin),
                Rotation = (Quaternion.Inverse(orientation) * target.rotation).eulerAngles,
                Scale = new Vector3(target.localScale.x / scale.x, target.localScale.y / scale.y, target.localScale.z / scale.z)
            };
            Tick(0f);
        }

        /// <summary>Consumes elapsed time across keyframe boundaries and applies the current eased pose.</summary>
        /// <param name="delta">Scaled seconds available for this update.</param>
        /// <returns>True when every enabled keyframe has finished.</returns>
        internal bool Tick(float delta)
        {
            // Overshoot is retained across delays and zero-duration frames, keeping low frame rates accurate.
            if (target == null || path == null || !path.Enabled)
                return true;
            elapsed += delta;
            while (index < path.Keyframes.Length)
            {
                SpawnFlowKeyframe frame = path.Keyframes[index];
                if (elapsed < frame.Delay)
                    return false;
                float amount = frame.Duration > 0f ? Mathf.Clamp01((elapsed - frame.Delay) / frame.Duration) : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, amount);
                Vector3 position = Vector3.Lerp(from.Position, frame.Pose.Position, eased);
                Quaternion rotation = TransformRotation.Sample(from.Rotation, frame.Pose.Rotation, eased, frame.Direction);
                Vector3 size = Vector3.Lerp(from.Scale, frame.Pose.Scale, eased);
                bool world = path.Space == SpawnFlowSpace.World;
                target.SetPositionAndRotation(world ? position : origin + orientation * position, world ? rotation : orientation * rotation);
                target.localScale = world ? size : Vector3.Scale(scale, size);
                if (amount < 1f)
                    return false;
                from = frame.Pose;
                elapsed -= frame.Delay + frame.Duration;
                index++;
            }
            return true;
        }

        #endregion
        #endregion
    }
}
