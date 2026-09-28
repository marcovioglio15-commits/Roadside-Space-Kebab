using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Caches animation targets once and samples only while a switch is running.</summary>
    internal sealed class PlayerToolAnimationRun
    {
        #region State

        private readonly PlayerToolAnimationPreset preset;
        private readonly Transform[] targets;
        private readonly PlayerToolPose[] initial;

        #endregion

        #region Properties

        /// <summary>Duration of the optional animation.</summary>
        internal float Duration => preset != null ? preset.Duration : 0f;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Resolves all child paths before the first switch.</summary>
        /// <param name="source">Optional animation asset.</param>
        /// <param name="model">Visual model supplying child transforms.</param>
        internal PlayerToolAnimationRun(PlayerToolAnimationPreset source, Transform model)
        {
            // Empty animations need no arrays or model binding.
            preset = source;
            targets = source != null ? new Transform[source.Tracks.Length] : Array.Empty<Transform>();
            initial = source != null ? new PlayerToolPose[targets.Length] : Array.Empty<PlayerToolPose>();
            for (int index = 0; index < targets.Length; index++)
                targets[index] = PlayerHierarchy.Resolve(model, source.Tracks[index].Path);
        }

        /// <summary>Rejects missing targets before any visual pose is modified.</summary>
        /// <param name="moved">Unique transforms whose original poses must be retained.</param>
        /// <param name="player">Player root, which must never be animated by Tools.</param>
        /// <returns>True when every animated child exists outside the player root.</returns>
        internal bool CollectTargets(HashSet<Transform> moved, Transform player)
        {
            // Destroyed targets are also caught at the start of later switches.
            foreach (Transform target in targets)
            {
                if (target == null || target == player)
                    return false;
                moved.Add(target);
            }
            return true;
        }

        #endregion

        #region Playback

        /// <summary>Captures the current pose for seamless interpolation into the first key.</summary>
        internal void Begin()
        {
            // Reuse fixed buffers for every switch.
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    initial[index] = PlayerToolPose.Read(targets[index]);
        }

        /// <summary>Applies all tracks at an elapsed animation time.</summary>
        /// <param name="time">Seconds since this animation began.</param>
        internal void Sample(float time)
        {
            // Missing children cannot redirect animation to another object.
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    preset.Tracks[index].Sample(time, initial[index]).Apply(targets[index]);
        }

        #endregion

        #endregion
    }
}
