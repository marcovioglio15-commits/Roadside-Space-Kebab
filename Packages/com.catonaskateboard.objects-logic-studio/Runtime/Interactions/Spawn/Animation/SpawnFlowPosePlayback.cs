using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Samples timed poses for travel paths and character tracks with the same interpolation rules.</summary>
    internal sealed class SpawnFlowPosePlayback
    {
        #region State

        private SpawnFlowAnimation path;
        private PlayerToolPose from;
        private Vector3 entryVelocity;
        private Vector3 exitVelocity;
        private int index;
        private float elapsed;

        #endregion

        #region Properties

        /// <summary>Current pose in the path's coordinate system.</summary>
        internal PlayerToolPose Pose { get; private set; }
        /// <summary>Distance covered by timed segments in the latest sample, excluding instant placement.</summary>
        internal float Distance { get; private set; }

        #endregion

        #region Methods
        #region Sampling

        /// <summary>Resets the cursor and applies any immediate starting keyframes.</summary>
        /// <param name="animation">Validated path to sample.</param>
        /// <param name="initial">Pose preceding its first destination.</param>
        internal void Begin(SpawnFlowAnimation animation, PlayerToolPose initial)
        {
            // Segment state is reused on every loop without allocating new playback objects.
            path = animation;
            from = initial;
            Pose = initial;
            index = 0;
            elapsed = 0f;
            entryVelocity = Vector3.zero;
            PrepareSegment();
            Tick(0f);
        }

        /// <summary>Consumes delays and segment overshoot without losing time at keyframe boundaries.</summary>
        /// <param name="delta">Scaled seconds available for this sample.</param>
        /// <returns>True when the enabled path has finished.</returns>
        internal bool Tick(float delta)
        {
            // Disabled paths do not modify the supplied starting pose.
            Distance = 0f;
            if (path == null || !path.Enabled)
                return true;
            elapsed += delta;
            while (index < path.Keyframes.Length)
            {
                SpawnFlowKeyframe frame = path.Keyframes[index];
                if (elapsed < frame.Delay)
                    return false;
                float amount = frame.Duration > 0f ? Mathf.Clamp01((elapsed - frame.Delay) / frame.Duration) : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, amount);
                Vector3 position = SpawnFlowPathSampling.Position(path.Interpolation, from.Position, frame.Pose.Position,
                    entryVelocity, exitVelocity, frame.Duration, amount);
                if (delta > 0f && frame.Duration > 0f)
                    Distance += Vector3.Distance(Pose.Position, position);
                Pose = new PlayerToolPose
                {
                    Position = position,
                    Rotation = TransformRotation.Sample(from.Rotation, frame.Pose.Rotation, eased, frame.Direction).eulerAngles,
                    Scale = Vector3.Lerp(from.Scale, frame.Pose.Scale, eased)
                };
                if (amount < 1f)
                    return false;
                from = frame.Pose;
                elapsed -= frame.Delay + frame.Duration;
                index++;
                entryVelocity = exitVelocity;
                PrepareSegment();
            }
            return true;
        }

        /// <summary>Caches the next continuous tangent only at segment boundaries.</summary>
        private void PrepareSegment()
        {
            // Sampling frames does not rescan the path to find neighboring destinations.
            exitVelocity = path != null && path.Enabled && index < path.Keyframes.Length
                ? SpawnFlowPathSampling.ExitVelocity(path, index, from.Position) : Vector3.zero;
        }

        #endregion
        #endregion
    }
}
