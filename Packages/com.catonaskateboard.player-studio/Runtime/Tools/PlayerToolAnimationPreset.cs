using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores one transform key at an explicit time in a switch animation.</summary>
    [Serializable]
    public sealed class PlayerToolKey
    {
        #region Fields

        [Header("Key")]
        [Tooltip("Seconds from the start of the animation. Keys must be strictly ordered.")]
        [Range(0f, 10f)]
        public float Time;
        [Tooltip("Transform pose reached at this time in the track's selected coordinate space.")]
        public PlayerToolPose Pose = PlayerToolPose.Identity;
        [Tooltip("Travel into this key. Clockwise uses negative angles around changed local axes; Counter Clockwise uses positive angles. Authored full turns are retained.")]
        public TransformRotationDirection RotationDirection;

        #endregion
    }

    /// <summary>Animates one child resolved relative to the Tools hierarchy root.</summary>
    [Serializable]
    public sealed class PlayerToolTrack
    {
        #region Fields

        [Header("Track")]
        [Tooltip("Target below Tools Hierarchy Root for use/reload/switch, or below the selected arm for idle. Empty selects that root.")]
        public string Path = string.Empty;
        [Tooltip("Local uses the target's parent coordinates. World uses scene position and rotation. Scale stays local.")]
        public PlayerToolSpace Space;
        [Tooltip("Treat keys as offsets from the pose captured at playback start. Identity leaves the target unchanged.")]
        public bool RelativeToStart;
        [Tooltip("Transform keys in increasing time order. The current pose is used before the first key.")]
        public PlayerToolKey[] Keys = Array.Empty<PlayerToolKey>();

        #endregion

        #region Methods

        #region Sampling

        /// <summary>Samples a track with an implicit initial key from the live pose.</summary>
        /// <param name="time">Elapsed animation time.</param>
        /// <param name="initial">Pose captured when this transition began.</param>
        /// <returns>Interpolated pose, retaining the final key after its time.</returns>
        public PlayerToolPose Sample(float time, PlayerToolPose initial)
        {
            // An empty track leaves the target untouched.
            float previous = 0f;
            foreach (PlayerToolKey key in Keys)
            {
                if (key == null || !float.IsFinite(key.Time) || !key.Pose.IsValid())
                    return initial;
                if (time < key.Time)
                    return PlayerToolPose.Interpolate(initial, key.Pose, Mathf.SmoothStep(0f, 1f, (time - previous) / (key.Time - previous)), key.RotationDirection);
                previous = key.Time;
                initial = key.Pose;
            }
            return initial;
        }

        #endregion

        #endregion
    }

    /// <summary>Reusable transform tracks for switching, idle, tool use and sequential reload steps.</summary>
    [CreateAssetMenu(fileName = "Tool Animation", menuName = "Player Studio/Tool Animation Preset")]
    public sealed class PlayerToolAnimationPreset : ScriptableObject
    {
        #region Fields

        [Header("Animation")]
        [Tooltip("Total duration in seconds; all keys must fit inside this duration.")]
        [Range(0.01f, 10f)]
        public float Duration = 0.3f;
        [Tooltip("Independent transform tracks bound through the hierarchy selector.")]
        public PlayerToolTrack[] Tracks = Array.Empty<PlayerToolTrack>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Reports invalid direct Inspector edits without changing the animation.</summary>
        private void OnValidate()
        {
            // All authored keys use the same validation contract.
            if (!TryValidate(out string warning))
                Debug.LogWarning(warning, this);
        }

        /// <summary>Checks timings, paths and poses without rewriting animation data.</summary>
        /// <param name="warning">Receives the first invalid animation setting.</param>
        /// <returns>True when every track can be sampled safely.</returns>
        public bool TryValidate(out string warning)
        {
            // Duplicate paths would make the result depend on track order.
            warning = "Use a positive animation duration and unique tracks with ordered, finite keys inside that duration.";
            if (!float.IsFinite(Duration) || Duration <= 0f || Tracks == null)
                return false;
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerToolTrack track in Tracks)
            {
                if (track == null || track.Path == null || !paths.Add(track.Path) || track.Keys == null
                    || track.Space is not (PlayerToolSpace.Local or PlayerToolSpace.World))
                    return false;
                float previous = -1f;
                foreach (PlayerToolKey key in track.Keys)
                {
                    if (key == null || !float.IsFinite(key.Time) || key.Time < 0f || key.Time <= previous
                        || key.Time > Duration || !key.Pose.IsValid() || !TransformRotation.IsValid(key.RotationDirection))
                        return false;
                    previous = key.Time;
                }
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
