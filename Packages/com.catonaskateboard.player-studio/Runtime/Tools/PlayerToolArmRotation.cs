using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Adds bounded rotational follow without filtering recoil, reload or idle keyframes.</summary>
    internal sealed class PlayerToolArmRotation
    {
        #region State

        private readonly Transform arm;
        private readonly PlayerToolArmSettings settings;
        private Quaternion followed;
        private Quaternion previous;
        private Quaternion original;
        private bool applied;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Starts following the current parent orientation without introducing an activation offset.</summary>
        /// <param name="target">Existing arm below the player.</param>
        /// <param name="configuration">Enabled rotation response and maximum delay.</param>
        internal PlayerToolArmRotation(Transform target, PlayerToolArmSettings configuration)
        {
            // Capture the clean parent before any presentation offset is applied.
            arm = target;
            settings = configuration;
            previous = followed = arm != null && arm.parent != null ? arm.parent.rotation : Quaternion.identity;
        }

        #endregion

        #region Presentation

        /// <summary>Removes the last additive follow offset before actions capture or restore their poses.</summary>
        internal void Restore()
        {
            // Action sampling always receives the same pose that preceded the last follow offset.
            if (applied && arm != null)
                arm.localRotation = original;
            applied = false;
        }

        /// <summary>Tracks parent turns in world space and applies only their remaining lag to the authored pose.</summary>
        /// <param name="delta">Positive scaled presentation interval.</param>
        internal void Sample(float delta)
        {
            if (arm == null || delta <= 0f)
                return;
            // Integrate parent motion between samples so the same turn behaves consistently across frame rates.
            Quaternion parent = arm.parent != null ? arm.parent.rotation : Quaternion.identity;
            PlayerCameraDamping.GetWeights((double)settings.RotationResponse * delta, out float targetWeight, out float motionWeight);
            followed = Quaternion.Slerp(followed, previous, targetWeight);
            followed = Quaternion.SlerpUnclamped(Quaternion.identity, parent * Quaternion.Inverse(previous), motionWeight) * followed;
            // Keep the hidden follow state below a half turn; the visible soft limit is already saturated here.
            followed = Quaternion.RotateTowards(parent, followed, settings.MaximumRotationLag * 4f);
            // A soft angular limit avoids a velocity corner when fast turns reach the maximum visible lag.
            Quaternion lag = Quaternion.Inverse(parent) * followed;
            float angle = Quaternion.Angle(Quaternion.identity, lag);
            float limited = settings.MaximumRotationLag * (float)System.Math.Tanh(angle / settings.MaximumRotationLag);
            previous = parent;
            original = arm.localRotation;
            arm.localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, lag, angle > 0.00001f ? limited / angle : 0f) * original;
            applied = true;
        }

        #endregion

        #endregion
    }
}
