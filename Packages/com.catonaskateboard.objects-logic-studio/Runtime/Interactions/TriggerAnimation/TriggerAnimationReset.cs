using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Returns a scene animation from its live pose using automatic-return timing, including inactive targets.</summary>
    internal sealed class TriggerAnimationReset : IDisposable
    {
        #region State

        private readonly ObjectTriggerAnimation source;
        private readonly Transform target;
        private readonly ObjectItem item;
        private readonly PlayerToolPose destination;
        private readonly TransformRotationDirection direction;
        private readonly float duration;
        private PlayerToolPose from;
        private float elapsed;
        private bool started;
        private bool completed;
        private bool disposed;

        #endregion

        #region Properties

        /// <summary>Whether this return is still waiting for ownership or moving toward its endpoint.</summary>
        internal bool IsRunning => !completed && !disposed && source != null && target != null;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Captures reset configuration once; pose capture waits until competing item ownership is released.</summary>
        /// <param name="source">Animation kept dormant until the day transition releases this handle.</param>
        /// <param name="target">Resolved transform, which may belong to an inactive hierarchy.</param>
        internal TriggerAnimationReset(ObjectTriggerAnimation source, Transform target)
        {
            this.source = source;
            this.target = target;
            item = source.Item;
            destination = source.Settings.StartAtB ? source.Settings.StateB : source.Settings.StateA;
            direction = source.Settings.AutoReturnRotation;
            duration = source.Settings.AutoReturnDuration;
        }

        /// <summary>Advances one scaled-time return without input, tool restrictions or ordinary interaction events.</summary>
        /// <param name="deltaTime">Scaled elapsed time supplied by the day manager.</param>
        /// <returns>True after arrival, or when destruction or consumption removes the target.</returns>
        internal bool Tick(float deltaTime)
        {
            if (!IsRunning || item == null || item.IsConsumed)
                return true;
            if (deltaTime <= 0f)
                return false;
            // Disabled items have no active transactions; active items retain their normal reservation boundary.
            if (item.isActiveAndEnabled && (target == item.transform && item.IsCarried
                || !item.IsOwnedBy(source) && !item.Acquire(source, target == item.transform ? InteractionChannels.Grab : InteractionChannels.None)))
                return false;
            if (!started)
            {
                from = PlayerToolPose.Read(target);
                PlayerToolPose endpoint = source.IsAtB ? source.Settings.StateB : source.Settings.StateA;
                if (Quaternion.Angle(Quaternion.Euler(from.Rotation), Quaternion.Euler(endpoint.Rotation)) < 0.001f)
                    from.Rotation = endpoint.Rotation;
                started = true;
                // An unchanged endpoint needs no artificial delay or full-circle rotation.
                completed = (from.Position - destination.Position).sqrMagnitude < 0.00000001f
                    && (from.Scale - destination.Scale).sqrMagnitude < 0.00000001f
                    && Quaternion.Angle(Quaternion.Euler(from.Rotation), Quaternion.Euler(destination.Rotation)) < 0.001f;
            }
            elapsed += deltaTime;
            completed |= elapsed >= duration;
            if (completed)
            {
                destination.Apply(target);
                item.Release(source);
                source.CompleteReset(this);
            }
            else
                PlayerToolPose.Interpolate(from, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration), direction).Apply(target);
            return completed;
        }

        /// <summary>Releases reset ownership without snapping an interrupted return to either endpoint.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            completed = true;
            if (item != null && source != null)
                item.Release(source);
            if (source != null)
                source.ReleaseReset(this);
        }

        #endregion
        #endregion
    }
}
