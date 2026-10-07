using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Acquires a fixed body set per pulse and restores it independently of other active generators.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Gravity Generator")]
    public sealed class ObjectGravityGenerator : ObjectRequestedInteraction
    {
        #region Fields

        [Header("Gravity Generator")]
        [Tooltip("Pulse interval, body filters, restoration policy and physical impulse.")]
        [SerializeField]
        private GravitySettings settings = new GravitySettings();

        /// <summary>Retains per-body force choices sampled only when a pulse begins.</summary>
        private readonly struct AffectedBody
        {
            #region Fields

            internal readonly Rigidbody Body;
            internal readonly Vector3 Force;
            internal readonly float Duration;

            #endregion
            #region Methods

            /// <summary>Captures one pulse's immutable force choices.</summary>
            /// <param name="body">Selected active body.</param>
            /// <param name="force">Sampled world-space impulse or acceleration.</param>
            /// <param name="duration">Duration of acceleration, or zero for one impulse.</param>
            internal AffectedBody(Rigidbody body, Vector3 force, float duration)
            {
                Body = body;
                Force = force;
                Duration = duration;
            }

            #endregion
        }

        private readonly List<AffectedBody> affected = new List<AffectedBody>();
        private float nextPulse;
        private float elapsed;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Locally imported pulse configuration.</summary>
        public GravitySettings Settings => settings;
        /// <summary>Whether this generator currently owns a suspension pulse.</summary>
        public bool IsSuspending { get; private set; }
        /// <summary>Whether restoration accepts a player request.</summary>
        public override bool UsesInput => settings.Restore != GravityRestoreMode.Timer;
        /// <summary>Targeting used by the shared observer for restoration requests.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Passive feature identifier retained by presets.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.GravityGenerator;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Validates once and schedules the first pulse when activated.</summary>
        protected override void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            base.OnEnable();
            Schedule();
        }

        /// <summary>Restores all owned bodies when disabled, destroyed or stored.</summary>
        protected override void OnDisable()
        {
            Restore(false);
            ready = false;
            base.OnDisable();
        }

        /// <summary>Recovers pulse scheduling when entering Play with retained scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectGravityGenerator generator in FindObjectsByType<ObjectGravityGenerator>())
                if (generator.isActiveAndEnabled)
                {
                    generator.OnDisable();
                    generator.OnEnable();
                }
        }

        /// <summary>Waits without scanning the scene between pulse boundaries.</summary>
        private void Update()
        {
            if (ready && !IsSuspending && Time.timeScale > 0f && Time.time >= nextPulse && Available(InteractionChannels.Passive))
                Begin();
        }

        /// <summary>Applies sustained acceleration only to the cached active body set.</summary>
        private void FixedUpdate()
        {
            if (!IsSuspending || Time.timeScale <= 0f)
                return;
            // Deactivated or destroyed bodies release their lease immediately; no scene search occurs here.
            for (int index = affected.Count - 1; index >= 0; index--)
            {
                AffectedBody entry = affected[index];
                if (entry.Body == null || !entry.Body.gameObject.activeInHierarchy)
                {
                    GravitySuspension.Release(entry.Body, this);
                    affected.RemoveAt(index);
                    continue;
                }
                if (!entry.Body.isKinematic && elapsed < entry.Duration)
                {
                    float remaining = settings.Restore == GravityRestoreMode.Interaction ? entry.Duration - elapsed
                        : Mathf.Min(entry.Duration - elapsed, settings.Duration - elapsed);
                    float fraction = Mathf.Min(Time.fixedDeltaTime, remaining) / Time.fixedDeltaTime;
                    entry.Body.AddForce(entry.Force * fraction, ForceMode.Acceleration);
                }
            }
            elapsed += Time.fixedDeltaTime;
            // Timer restoration remains available even if a later lock prevents new pulses or player input.
            if (settings.Restore != GravityRestoreMode.Interaction && elapsed >= settings.Duration)
                Restore(true);
        }

        #endregion
        #region Pulses

        /// <summary>Samples the interval once after restoration, never on ordinary frames.</summary>
        private void Schedule()
        {
            nextPulse = Time.time + (settings.RandomInterval ? Random.Range(settings.IntervalRange.x, settings.IntervalRange.y) : settings.Interval);
        }

        /// <summary>Acquires matching dynamic bodies and samples each body's force once.</summary>
        private void Begin()
        {
            elapsed = 0f;
            IsSuspending = true;
            foreach (Rigidbody body in FindObjectsByType<Rigidbody>())
            {
                if (body.isKinematic || !body.gameObject.activeInHierarchy || !settings.Filter.Matches(body, transform))
                    continue;
                Vector3 direction = settings.RandomDirection ? new Vector3(
                    Random.Range(settings.DirectionMinimum.x, settings.DirectionMaximum.x),
                    Random.Range(settings.DirectionMinimum.y, settings.DirectionMaximum.y),
                    Random.Range(settings.DirectionMinimum.z, settings.DirectionMaximum.z)) : settings.Direction;
                if (direction.sqrMagnitude < 0.000001f)
                    direction = settings.DirectionMaximum.sqrMagnitude > 0f ? settings.DirectionMaximum : settings.DirectionMinimum;
                direction.Normalize();
                if (settings.LocalDirection)
                    direction = transform.TransformDirection(direction);
                float intensity = settings.Push ? settings.RandomIntensity ? Random.Range(settings.IntensityRange.x, settings.IntensityRange.y) : settings.Intensity : 0f;
                float duration = settings.RandomForceDuration ? Random.Range(settings.ForceDurationRange.x, settings.ForceDurationRange.y) : settings.ForceDuration;
                GravitySuspension.Acquire(body, this);
                affected.Add(new AffectedBody(body, direction * intensity, duration));
                if (duration <= 0f && settings.Push)
                    body.AddForce(direction * intensity, ForceMode.Impulse);
            }
            Signal(InteractionMoment.Started);
        }

        /// <summary>Releases this generator's ownership and schedules its next pulse.</summary>
        /// <param name="completed">Publish completion for a timer or player restoration, not for cancellation.</param>
        private void Restore(bool completed)
        {
            if (!IsSuspending)
                return;
            foreach (AffectedBody entry in affected)
                GravitySuspension.Release(entry.Body, this);
            affected.Clear();
            IsSuspending = false;
            Schedule();
            if (completed)
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Offers the restoration button only during an eligible active pulse.</summary>
        /// <returns>True when a player request may restore this generator's bodies.</returns>
        internal override bool CanRequest()
        {
            return ready && UsesInput && IsSuspending && Available(InteractionChannels.Passive);
        }

        /// <summary>Restores gravity after the observer validates range and aim.</summary>
        /// <returns>True when the button completed an active pulse.</returns>
        internal override bool Request()
        {
            if (!CanRequest())
                return false;
            Restore(true);
            return true;
        }

        #endregion
        #region Validation

        /// <summary>Validates the applied pulse and optional input binding.</summary>
        /// <param name="warning">Receives missing or invalid settings.</param>
        /// <returns>True when this generator may schedule pulses.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, Action, out warning);
        }

        /// <summary>Validates a detached generator proposal before Apply.</summary>
        /// <param name="configuration">Proposed pulse settings.</param>
        /// <param name="button">Proposed restoration action.</param>
        /// <param name="warning">Receives invalid settings or a missing Button.</param>
        /// <returns>True when the proposal is complete.</returns>
        public bool TryValidate(GravitySettings configuration, InputActionReference button, out string warning)
        {
            warning = "Configure gravity generator settings.";
            return configuration != null && configuration.TryValidate(out warning)
                && ValidateInput(configuration.Restore != GravityRestoreMode.Timer, button, out warning);
        }

        #endregion
        #endregion
    }
}
