using UnityEngine;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Updates transforms only while an authored transition or pulse is active.</summary>
    public sealed class MenuMotion : MonoBehaviour
    {
        #region Fields
        private RectTransform target;
        private MenuButtonStyle style;
        private MenuVisualState state;
        private MenuVisualPhase phase;
        private Vector3 baselinePosition;
        private Vector3 baselineScale;
        private Quaternion baselineRotation;
        private Vector3 fromPosition;
        private Vector3 fromScale;
        private Quaternion fromRotation;
        private float elapsed;
        #endregion

        #region Methods
        #region Configuration
        /// <summary>Captures a preauthored transform before its first interaction.</summary>
        /// <param name="motionTarget">Existing button or content transform.</param>
        public void Initialize(RectTransform motionTarget)
        {
            // Baselines remain stable across repeated hover and press transitions.
            target = motionTarget;
            baselinePosition = target.localPosition;
            baselineScale = target.localScale;
            baselineRotation = target.localRotation;
            enabled = false;
        }

        /// <summary>Starts a transition using the current pose as its blend origin.</summary>
        /// <param name="profile">Owning menu's interaction profile.</param>
        /// <param name="visual">Requested state values.</param>
        /// <param name="requested">Requested interaction phase.</param>
        /// <param name="instant">Apply the end pose without animation.</param>
        public void Play(MenuButtonStyle profile, MenuVisualState visual, MenuVisualPhase requested, bool instant)
        {
            // No coroutine, new UI object or repeated component lookup is required.
            if (target == null)
                return;
            style = profile;
            state = visual;
            phase = requested;
            elapsed = 0f;
            fromPosition = target.localPosition;
            fromScale = target.localScale;
            fromRotation = target.localRotation;
            if (instant || style.Motion == MenuMotionMode.None)
            {
                Apply(1f, 0f);
                enabled = false;
                return;
            }
            enabled = true;
        }
        #endregion

        #region Animation
        /// <summary>Advances only active feedback using the profile's selected time source.</summary>
        private void Update()
        {
            // This component disables itself when a finite transition has completed.
            elapsed += style.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            bool pulse = phase == MenuVisualPhase.Hover && style.HoverPulse && style.PulseSeconds > 0f;
            if (pulse)
            {
                float cycles = elapsed / style.PulseSeconds;
                float weight = 0.5f - 0.5f * Mathf.Cos(cycles * Mathf.PI * 2f);
                Apply(weight, elapsed);
                if (!style.LoopPulse && cycles >= style.PulseCycles)
                {
                    Apply(0f, elapsed);
                    enabled = false;
                }
                return;
            }
            float progress = style.TransitionSeconds > 0f ? Mathf.Clamp01(elapsed / style.TransitionSeconds) : 1f;
            Apply(progress * progress * (3f - 2f * progress), elapsed);
            if (progress >= 1f && (state.Clip == null || elapsed >= state.Clip.length))
                enabled = false;
        }

        /// <summary>Samples clip feedback and blends authored transform offsets.</summary>
        /// <param name="weight">Blend weight within the current transition.</param>
        /// <param name="seconds">Elapsed clip time.</param>
        private void Apply(float weight, float seconds)
        {
            // Clips target an existing hierarchy; combined mode gives explicit transform offsets priority.
            if ((style.Motion == MenuMotionMode.Clips || style.Motion == MenuMotionMode.TransformAndClips) && state.Clip != null)
                state.Clip.SampleAnimation(target.gameObject, Mathf.Min(seconds, state.Clip.length));
            if (style.Motion != MenuMotionMode.Transform && style.Motion != MenuMotionMode.TransformAndClips)
                return;
            bool pulse = phase == MenuVisualPhase.Hover && style.HoverPulse;
            target.localPosition = Vector3.LerpUnclamped(pulse ? baselinePosition : fromPosition, baselinePosition + state.Offset, weight);
            target.localScale = Vector3.LerpUnclamped(pulse ? baselineScale : fromScale, Vector3.Scale(baselineScale, state.Scale), weight);
            target.localRotation = Quaternion.SlerpUnclamped(pulse ? baselineRotation : fromRotation, baselineRotation * Quaternion.Euler(state.Rotation), weight);
        }
        #endregion
        #endregion
    }
}
