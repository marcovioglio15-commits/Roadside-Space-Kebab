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
        private readonly PlayerToolPose[] restore;
        private readonly int[] order;
        private bool captured;

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
            restore = new PlayerToolPose[targets.Length];
            order = new int[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                targets[index] = PlayerHierarchy.Resolve(model, source.Tracks[index].Path);
                order[index] = index;
            }
            // Parents are sampled first so world-space children keep their authored world pose.
            Array.Sort(order, CompareDepth);
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
                {
                    initial[index] = PlayerToolPose.Read(targets[index], preset.Tracks[index].Space);
                    restore[index] = PlayerToolPose.Read(targets[index]);
                }
            captured = true;
        }

        /// <summary>Applies all tracks at an elapsed animation time.</summary>
        /// <param name="time">Seconds since this animation began.</param>
        internal void Sample(float time)
        {
            // Missing children cannot redirect animation to another object.
            foreach (int index in order)
                if (targets[index] != null)
                {
                    PlayerToolTrack track = preset.Tracks[index];
                    PlayerToolPose pose = track.Sample(time, track.RelativeToStart ? PlayerToolPose.Identity : initial[index]);
                    if (track.RelativeToStart)
                    {
                        pose.Position += initial[index].Position;
                        pose.Rotation = (Quaternion.Euler(initial[index].Rotation) * Quaternion.Euler(pose.Rotation)).eulerAngles;
                        pose.Scale = Vector3.Scale(initial[index].Scale, pose.Scale);
                    }
                    pose.Apply(targets[index], track.Space);
                }
        }

        /// <summary>Restores local poses captured before temporary idle, use or reload motion.</summary>
        internal void Restore()
        {
            // Restore only after a successful Begin; repeated cleanup is harmless.
            if (!captured)
                return;
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    restore[index].Apply(targets[index]);
            captured = false;
        }

        /// <summary>Orders bindings by hierarchy depth once, before any animation sampling.</summary>
        /// <param name="left">First track index.</param>
        /// <param name="right">Second track index.</param>
        /// <returns>Parent tracks before descendants, retaining authored order at equal depth.</returns>
        private int CompareDepth(int left, int right)
        {
            // Missing targets are rejected by CollectTargets before playback.
            int comparison = Depth(targets[left]).CompareTo(Depth(targets[right]));
            return comparison != 0 ? comparison : left.CompareTo(right);
        }

        /// <summary>Counts parents only while building the playback binding.</summary>
        /// <param name="target">Optional bound transform.</param>
        /// <returns>Number of ancestors in the hierarchy.</returns>
        private static int Depth(Transform target)
        {
            int depth = 0;
            // No hierarchy traversal is needed while sampling a frame.
            for (Transform parent = target != null ? target.parent : null; parent != null; parent = parent.parent)
                depth++;
            return depth;
        }

        #endregion

        #endregion
    }
}
