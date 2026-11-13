using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares continuous path sampling between visitor movement and scene guides.</summary>
    internal static class SpawnFlowPathSampling
    {
        #region Methods
        #region Sampling

        /// <summary>Finds the shared velocity at a keyframe that connects two moving segments.</summary>
        /// <param name="path">Validated walk path.</param>
        /// <param name="index">Segment ending at the current keyframe.</param>
        /// <param name="start">Position preceding this segment, in path coordinates.</param>
        /// <returns>A velocity in path units per second, or zero at a required stop.</returns>
        internal static Vector3 ExitVelocity(SpawnFlowAnimation path, int index, Vector3 start)
        {
            // Delays, instant poses, reversals and stationary turns interrupt continuous travel.
            if (path.Interpolation != SpawnFlowInterpolation.Continuous || index + 1 >= path.Keyframes.Length)
                return Vector3.zero;
            SpawnFlowKeyframe current = path.Keyframes[index];
            SpawnFlowKeyframe next = path.Keyframes[index + 1];
            Vector3 incoming = current.Pose.Position - start;
            Vector3 outgoing = next.Pose.Position - current.Pose.Position;
            if (current.Duration <= 0f || next.Duration <= 0f || next.Delay > 0f
                || incoming.sqrMagnitude < 0.000001f || outgoing.sqrMagnitude < 0.000001f
                || Vector3.Dot(incoming, outgoing) < 0f)
                return Vector3.zero;
            // Bound the shared tangent so very different segment timings cannot reverse a short segment.
            return Vector3.ClampMagnitude((next.Pose.Position - start) / (current.Duration + next.Duration),
                2f * Mathf.Min(incoming.magnitude / current.Duration, outgoing.magnitude / next.Duration));
        }

        /// <summary>Samples a segment while keeping adjacent continuous velocities equal.</summary>
        /// <param name="mode">Position interpolation selected for the path.</param>
        /// <param name="start">Previous keyframe position.</param>
        /// <param name="end">Current destination.</param>
        /// <param name="entryVelocity">Shared velocity from the preceding segment.</param>
        /// <param name="exitVelocity">Shared velocity into the following segment.</param>
        /// <param name="duration">Travel time for this segment.</param>
        /// <param name="amount">Normalized segment progress.</param>
        /// <returns>The interpolated position in path coordinates.</returns>
        internal static Vector3 Position(SpawnFlowInterpolation mode, Vector3 start, Vector3 end,
            Vector3 entryVelocity, Vector3 exitVelocity, float duration, float amount)
        {
            // Existing paths retain their original easing until another mode is selected.
            switch (mode)
            {
                case SpawnFlowInterpolation.Linear:
                    return Vector3.LerpUnclamped(start, end, amount);
                case SpawnFlowInterpolation.Continuous:
                    float squared = amount * amount;
                    float cubed = squared * amount;
                    return (2f * cubed - 3f * squared + 1f) * start + (cubed - 2f * squared + amount) * duration * entryVelocity
                        + (-2f * cubed + 3f * squared) * end + (cubed - squared) * duration * exitVelocity;
                default:
                    return Vector3.LerpUnclamped(start, end, Mathf.SmoothStep(0f, 1f, amount));
            }
        }

        #endregion
        #endregion
    }
}
