using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reuses bound local tracks and composes their offsets after controller animation.</summary>
    internal sealed class SpawnFlowTransformRun
    {
        #region State

        private readonly Track[] tracks;
        private readonly float duration;
        private float elapsed;
        private bool looping;

        #endregion

        #region Properties

        /// <summary>Whether the longest one-shot track has reached its final destination.</summary>
        internal bool Finished => !looping && elapsed >= duration;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Maps exact prefab transforms to clone transforms once, including duplicate names.</summary>
        /// <param name="settings">Validated parallel tracks for one role.</param>
        /// <param name="original">Prefab hierarchy in component order.</param>
        /// <param name="generated">Matching clone hierarchy before activation.</param>
        internal SpawnFlowTransformRun(SpawnFlowTransformTrack[] settings, Transform[] original, Transform[] generated)
        {
            // All role bindings and samplers are allocated before the visitor starts moving.
            foreach (SpawnFlowTransformTrack track in settings)
                duration = Mathf.Max(duration, SpawnFlowPathSampling.Duration(track.Animation));
            tracks = new Track[settings.Length];
            for (int index = 0; index < tracks.Length; index++)
            {
                int target = settings[index].Target != null ? Array.IndexOf(original, settings[index].Target) : 0;
                tracks[index] = new Track(target >= 0 && target < generated.Length ? generated[target] : null, settings[index].Animation,
                    settings[index].IndependentLoop ? SpawnFlowPathSampling.Duration(settings[index].Animation) : duration);
            }
        }

        /// <summary>Restarts every track from identity without changing the underlying visitor pose.</summary>
        /// <param name="repeat">Whether the role repeats until replaced.</param>
        internal void Begin(bool repeat)
        {
            // Each track retains its selected shared or independent cycle.
            Restore();
            elapsed = 0f;
            looping = repeat;
            foreach (Track track in tracks)
                track.Begin();
        }

        /// <summary>Removes only the previous additive pose before movement and physics can continue.</summary>
        internal void Restore()
        {
            // Local offsets can be removed in any hierarchy order without moving descendant local poses.
            foreach (Track track in tracks)
                track.Restore();
        }

        #endregion
        #region Playback

        /// <summary>Advances cached cursors while each track retains its selected loop timing.</summary>
        /// <param name="delta">Scaled seconds; zero retains the current pose during pause.</param>
        internal void Tick(float delta)
        {
            // Finished death tracks keep their last sample without further timing work.
            if (Finished)
                return;
            // Modulo handles long frames without replaying every skipped cycle.
            elapsed += delta;
            if (looping && elapsed >= duration)
                elapsed %= duration;
            foreach (Track track in tracks)
                track.Tick(delta, looping);
        }

        /// <summary>Applies sampled offsets after Unity has evaluated the Animator.</summary>
        internal void Apply()
        {
            // Sampling and transform writes stay separate so controllers cannot overwrite these offsets.
            foreach (Track track in tracks)
                track.Apply();
        }

        #endregion
        #endregion

        /// <summary>Retains one exact target and the reversible offset applied in the previous frame.</summary>
        private sealed class Track
        {
            #region State

            private readonly Transform target;
            private readonly SpawnFlowAnimation animation;
            private readonly SpawnFlowPosePlayback playback = new SpawnFlowPosePlayback();
            private readonly float cycle;
            private float elapsed;
            private Vector3 translation;
            private Quaternion rotation;
            private Vector3 scale;
            private bool applied;

            #endregion

            #region Methods
            #region Playback

            /// <summary>Captures a resolved target without retaining any hierarchy lookup.</summary>
            /// <param name="target">Clone root or child receiving local offsets.</param>
            /// <param name="animation">Validated relative keyframes.</param>
            /// <param name="cycle">Shared or independent loop duration; zero holds an immediate pose.</param>
            internal Track(Transform target, SpawnFlowAnimation animation, float cycle)
            {
                // Targets remain stable throughout this visitor's lifetime.
                this.target = target;
                this.animation = animation;
                this.cycle = cycle;
            }

            /// <summary>Resets the reused sampler to an identity offset.</summary>
            internal void Begin()
            {
                // Identity preserves the underlying path and controller pose.
                elapsed = 0f;
                playback.Begin(animation, PlayerToolPose.Identity);
            }

            /// <summary>Advances one track and skips complete cycles without allocating or losing time.</summary>
            /// <param name="delta">Scaled seconds available for the current cycle.</param>
            /// <param name="repeat">Whether this role loops until interrupted.</param>
            internal void Tick(float delta, bool repeat)
            {
                // Zero-duration tracks hold their immediate endpoint instead of creating an endless loop.
                elapsed += delta;
                if (repeat && cycle > 0f && elapsed >= cycle)
                {
                    elapsed %= cycle;
                    playback.Begin(animation, PlayerToolPose.Identity);
                    delta = elapsed;
                }
                playback.Tick(delta);
            }

            /// <summary>Adds the sampled offset to the current path or controller pose.</summary>
            internal void Apply()
            {
                // A target destroyed by an interaction cannot redirect animation to another branch.
                if (target == null || applied)
                    return;
                PlayerToolPose pose = playback.Pose;
                translation = target.localRotation * pose.Position;
                rotation = Quaternion.Euler(pose.Rotation);
                scale = pose.Scale;
                target.SetLocalPositionAndRotation(target.localPosition + translation, target.localRotation * rotation);
                target.localScale = Vector3.Scale(target.localScale, scale);
                applied = true;
            }

            /// <summary>Removes the applied offset while retaining independent root travel.</summary>
            internal void Restore()
            {
                // Subtracting the applied offset retains movement received since the last LateUpdate.
                if (!applied)
                    return;
                if (target != null)
                {
                    target.SetLocalPositionAndRotation(target.localPosition - translation, target.localRotation * Quaternion.Inverse(rotation));
                    target.localScale = new Vector3(target.localScale.x / scale.x, target.localScale.y / scale.y, target.localScale.z / scale.z);
                }
                applied = false;
            }

            #endregion
            #endregion
        }
    }
}
