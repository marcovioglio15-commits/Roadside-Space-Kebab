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
        private readonly SpawnFlowPosePlayback playback = new SpawnFlowPosePlayback();
        private bool world;
        private bool enabled;

        #endregion

        #region Properties

        /// <summary>Actual world travel per scaled second, excluding instant pose changes.</summary>
        internal float Speed { get; private set; }

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
            world = animation.Space == SpawnFlowSpace.World;
            enabled = animation.Enabled;
            Speed = 0f;
            playback.Begin(animation, world ? new PlayerToolPose
            {
                Position = target.position,
                Rotation = target.eulerAngles,
                Scale = target.localScale
            } : new PlayerToolPose
            {
                Position = Quaternion.Inverse(orientation) * (target.position - origin),
                Rotation = (Quaternion.Inverse(orientation) * target.rotation).eulerAngles,
                Scale = new Vector3(target.localScale.x / scale.x, target.localScale.y / scale.y, target.localScale.z / scale.z)
            });
            Tick(0f);
        }

        /// <summary>Consumes elapsed time across keyframe boundaries and applies the current eased pose.</summary>
        /// <param name="delta">Scaled seconds available for this update.</param>
        /// <returns>True when every enabled keyframe has finished.</returns>
        internal bool Tick(float delta)
        {
            Speed = 0f;
            // Overshoot is retained across delays and zero-duration frames, keeping low frame rates accurate.
            if (target == null || !enabled)
                return true;
            bool finished = playback.Tick(delta);
            PlayerToolPose pose = playback.Pose;
            Quaternion rotation = Quaternion.Euler(pose.Rotation);
            Speed = delta > 0f ? playback.Distance / delta : 0f;
            target.SetPositionAndRotation(world ? pose.Position : origin + orientation * pose.Position, world ? rotation : orientation * rotation);
            target.localScale = world ? pose.Scale : Vector3.Scale(scale, pose.Scale);
            return finished;
        }

        #endregion
        #endregion
    }
}
