using System;
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
                targets[index] = Resolve(model, source.Tracks[index].Path);
        }

        /// <summary>Resolves a model-local path without hierarchy searches during playback.</summary>
        /// <param name="model">Bound model root.</param>
        /// <param name="path">Relative child path, or empty for the model.</param>
        /// <returns>The existing target, or null for a missing binding.</returns>
        internal static Transform Resolve(Transform model, string path)
        {
            // Optional visual modules never cause implicit object creation.
            return model == null ? null : string.IsNullOrEmpty(path) ? model : model.Find(path);
        }

        /// <summary>Rejects missing targets before any visual pose is modified.</summary>
        /// <returns>True when every animated child exists.</returns>
        internal bool IsBound()
        {
            // Destroyed targets are also caught at the start of later switches.
            foreach (Transform target in targets)
                if (target == null)
                    return false;
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
