using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Plays an overhead camera impact after the rig computes its ordinary pose.</summary>
    public static class PlayerCameraImpact
    {
        #region Types

        /// <summary>Captures one flight from the current view to a fixed impact point.</summary>
        private sealed class Flight
        {
            internal readonly Pose Start;
            internal readonly Pose Impact;
            internal readonly float Started;
            internal readonly float Duration;
            internal readonly float ArcHeight;
            internal bool Struck;
            private Action<bool> resolution;

            /// <summary>Prepares a downward impact with the camera's heading preserved from above.</summary>
            /// <param name="view">Camera supplying the starting pose and near clip.</param>
            /// <param name="target">World point on the dish.</param>
            /// <param name="duration">Seconds for the flight and return.</param>
            /// <param name="arcHeight">Height added at the middle of the flight.</param>
            /// <param name="clearance">Minimum distance above the target at impact.</param>
            /// <param name="resolution">Optional callback for a strike or cancellation.</param>
            internal Flight(Camera view, Vector3 target, float duration, float arcHeight, float clearance, Action<bool> resolution)
            {
                Transform frame = view.transform;
                Start = new Pose(frame.position, frame.rotation);
                Vector3 heading = Vector3.ProjectOnPlane(target - frame.position, Vector3.up);
                if (heading.sqrMagnitude < 0.0001f)
                    heading = Vector3.Cross(frame.right, Vector3.up);
                if (heading.sqrMagnitude < 0.0001f)
                    heading = Vector3.forward;
                Impact = new Pose(target + Vector3.up * Mathf.Max(clearance, view.nearClipPlane + 0.06f),
                    Quaternion.LookRotation(Vector3.down, heading));
                Started = Time.time;
                Duration = duration;
                ArcHeight = arcHeight;
                this.resolution = resolution;
            }

            /// <summary>Transfers the pending callback so the same flight can resolve only once.</summary>
            /// <returns>Callback to invoke after the pose is applied, or during cancellation.</returns>
            internal Action<bool> TakeResolution()
            {
                Action<bool> callback = resolution;
                resolution = null;
                return callback;
            }
        }

        #endregion

        #region State

        private const float strikeAt = 0.51f;
        private const float recoilAt = 0.65f;
        private static readonly Dictionary<Camera, Flight> flights = new Dictionary<Camera, Flight>();

        #endregion

        #region Methods

        #region Playback

        /// <summary>Starts or replaces the directed feedback on one gameplay camera.</summary>
        /// <param name="view">Camera controlled by the player rig.</param>
        /// <param name="target">World point under the strike.</param>
        /// <param name="duration">Seconds for the complete effect.</param>
        /// <param name="arcHeight">Height added above the straight flight path, in metres.</param>
        /// <param name="clearance">Minimum distance above the target at impact, in metres.</param>
        /// <param name="resolution">Receives true after the impact pose is applied, or false if cancelled first.</param>
        /// <returns>True when a flight was started.</returns>
        public static bool Play(Camera view, Vector3 target, float duration, float arcHeight, float clearance, Action<bool> resolution = null)
        {
            if (view == null || !view.isActiveAndEnabled || !float.IsFinite(duration) || duration <= 0f
                || !float.IsFinite(arcHeight) || arcHeight <= 0f || !float.IsFinite(clearance) || clearance <= 0f)
                return false;
            Stop(view);
            flights[view] = new Flight(view, target, duration, arcHeight, clearance, resolution);
            return true;
        }

        /// <summary>Clears feedback when a rig releases its camera.</summary>
        /// <param name="view">Camera leaving the rig.</param>
        public static void Stop(Camera view)
        {
            if (!ReferenceEquals(view, null) && flights.Remove(view, out Flight flight))
                flight.TakeResolution()?.Invoke(false);
        }

        /// <summary>Clears retained animation state before a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            flights.Clear();
        }

        /// <summary>Composes an arcing approach, downward strike and recoil over the live rig.</summary>
        /// <param name="view">Camera receiving the pose.</param>
        /// <param name="position">Current rig position, replaced while the effect is active.</param>
        /// <param name="rotation">Current rig orientation, restored during recovery.</param>
        /// <returns>One-shot callback for the frame reaching the strike, if assigned.</returns>
        internal static Action<bool> Apply(Camera view, ref Vector3 position, ref Quaternion rotation)
        {
            if (!flights.TryGetValue(view, out Flight flight))
                return null;
            float progress = (Time.time - flight.Started) / flight.Duration;

            // Apply the exact bottom pose for one frame even when a long frame crosses the strike.
            if (!flight.Struck && progress >= strikeAt)
            {
                flight.Struck = true;
                position = flight.Impact.position;
                rotation = flight.Impact.rotation;
                return flight.TakeResolution();
            }
            if (progress >= 1f)
            {
                flights.Remove(view);
                return null;
            }

            // Horizontal speed reaches zero above the dish. The vertical parabola keeps accelerating into the strike.
            if (progress < strikeAt)
            {
                float time = progress / strikeAt;
                float remaining = 1f - time;
                position = Vector3.LerpUnclamped(flight.Start.position, flight.Impact.position, 1f - remaining * remaining);
                position.y = Mathf.LerpUnclamped(flight.Start.position.y, flight.Impact.position.y, time)
                    + 4f * flight.ArcHeight * time * remaining;
                rotation = Quaternion.Slerp(flight.Start.rotation, flight.Impact.rotation, Mathf.SmoothStep(0f, 1f, time));
                return null;
            }

            // A short vertical recoil sells the hit, then the camera catches up with current player input.
            Vector3 rebound = flight.Impact.position + Vector3.up * (flight.ArcHeight * 0.23f);
            if (progress < recoilAt)
            {
                float time = (progress - strikeAt) / (recoilAt - strikeAt);
                position = Vector3.LerpUnclamped(flight.Impact.position, rebound, 1f - (1f - time) * (1f - time));
                rotation = flight.Impact.rotation;
                return null;
            }
            float recovery = Mathf.SmoothStep(0f, 1f, (progress - recoilAt) / (1f - recoilAt));
            position = Vector3.LerpUnclamped(rebound, position, recovery);
            rotation = Quaternion.Slerp(flight.Impact.rotation, rotation, recovery);
            return null;
        }

        #endregion

        #endregion
    }
}
