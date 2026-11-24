using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses each character role's playback source without discarding the other settings.</summary>
    public enum SpawnFlowAnimationMode { Animator, TransformKeyframes, None }

    /// <summary>Animates local offsets on one prefab transform alongside the visitor's travel path.</summary>
    [Serializable]
    public sealed class SpawnFlowTransformTrack
    {
        #region Fields

        [Header("Transform Track")]
        [Tooltip("Transform on the visitor prefab. Empty targets the root. Select a child to animate only that branch.")]
        public Transform Target;
        [Tooltip("Repeat this track at its own duration during Idle, Walk or Talk. Disable to share the longest track's cycle. One-shot roles always play each track once.")]
        public bool IndependentLoop;
        [Tooltip("Local offsets. Position follows the base orientation; rotation is added and scale is multiplied. Identity returns to the base pose.")]
        public SpawnFlowAnimation Animation = new SpawnFlowAnimation { Enabled = true };

        #endregion

        #region Methods
        #region Validation

        /// <summary>Rejects missing, repeated or invalid targets without modifying saved keyframes.</summary>
        /// <param name="tracks">Parallel tracks belonging to one animation role.</param>
        /// <param name="prefab">Visitor hierarchy supplying targets.</param>
        /// <param name="looping">Whether the role needs a positive loop duration.</param>
        /// <param name="warning">Receives the first target or timing issue.</param>
        /// <returns>True when every track is safe to sample.</returns>
        internal static bool TryValidate(SpawnFlowTransformTrack[] tracks, GameObject prefab, bool looping, out string warning)
        {
            // An explicit transform mode requires at least one usable track.
            warning = "Add a transform track with keyframes on this visitor prefab.";
            if (prefab == null || tracks == null || tracks.Length == 0)
                return false;
            float duration = 0f;
            for (int index = 0; index < tracks.Length; index++)
            {
                SpawnFlowTransformTrack track = tracks[index];
                warning = "Each track needs a target on this prefab and an enabled Relative animation.";
                if (track == null || track.Target != null && !track.Target.IsChildOf(prefab.transform)
                    || track.Animation == null || !track.Animation.Enabled || track.Animation.Space != SpawnFlowSpace.Relative)
                    return false;
                if (!track.Animation.TryValidate(out warning))
                    return false;
                // Root and an explicit root reference identify the same target.
                for (int previous = 0; previous < index; previous++)
                    if ((tracks[previous].Target != null ? tracks[previous].Target : prefab.transform)
                        == (track.Target != null ? track.Target : prefab.transform))
                    {
                        warning = "Use each transform only once within an animation role.";
                        return false;
                    }
                duration = Mathf.Max(duration, SpawnFlowPathSampling.Duration(track.Animation));
            }
            warning = "Use finite total track durations and a positive duration for looping animations.";
            if (!float.IsFinite(duration) || looping && duration <= 0f)
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
