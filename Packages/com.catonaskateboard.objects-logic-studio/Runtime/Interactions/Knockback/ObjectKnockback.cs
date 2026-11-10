using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reacts to a physical contact from a player-thrown object with damped motion around its resting pose.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Objects Logic Studio/Knockback")]
    public sealed class ObjectKnockback : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Knockback")]
        [Tooltip("Incoming throw limits, optional filters and physical reaction.")]
        [SerializeField]
        private KnockbackSettings settings = new KnockbackSettings();

        private readonly List<ObjectInteraction> filterBuffer = new List<ObjectInteraction>();
        private Rigidbody body;
        private Pose rest;
        private Vector3 direction;
        private Vector3 axis;
        private float distance;
        private float tilt;
        private float duration;
        private float cycles;
        private float damping;
        private float elapsed;
        private bool reacting;

        #endregion

        #region Properties

        /// <summary>Reaction configuration used by Objects Logic Studio.</summary>
        public KnockbackSettings Settings => settings;
        /// <summary>Passive interaction identifier used by presets and Unlock rules.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Knockback;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Binds the receiver once before Unity starts delivering collisions.</summary>
        private void OnEnable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            // An invalid component stays available for editing without handling physics.
            if (TryValidate(out string warning))
                body = GetComponent<Rigidbody>();
            else
                Debug.LogWarning(warning, this);
        }

        /// <summary>Clears a recoil if the receiver is disabled, pooled or consumed.</summary>
        private void OnDisable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            // Restore only a pose this reaction still controls.
            if (reacting && CanWobble())
            {
                body.position = rest.position;
                body.rotation = rest.rotation;
            }
            reacting = false;
            body = null;
        }

        /// <summary>Validates the applied reaction against the receiver's Rigidbody.</summary>
        /// <param name="warning">Receives the invalid setting or missing dependency.</param>
        /// <returns>True when the current configuration can run.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, out warning);
        }

        /// <summary>Validates a pending configuration against this receiver before Apply.</summary>
        /// <param name="configuration">Reaction settings in the current draft.</param>
        /// <param name="warning">Receives the first configuration issue.</param>
        /// <returns>True when the receiver supports the proposed reaction.</returns>
        public bool TryValidate(KnockbackSettings configuration, out string warning)
        {
            warning = "Configure Knockback settings.";
            return configuration != null && configuration.TryValidate(GetComponent<Rigidbody>(), out warning);
        }

        #endregion

        #region Reaction

        /// <summary>Starts one reaction from a solver-confirmed contact with a live player throw.</summary>
        /// <param name="collision">Contact data for the thrown body and this receiver.</param>
        private void OnCollisionEnter(Collision collision)
        {
            // Unity sends collision messages to disabled behaviours too.
            if (reacting || !CanWobble() || !Available(InteractionChannels.Passive)
                || collision.contactCount == 0 || collision.rigidbody == null
                || !collision.rigidbody.TryGetComponent(out ObjectGrab thrown) || !thrown.isActiveAndEnabled || thrown.IsHeld
                || Time.timeAsDouble - thrown.LastThrowTime > settings.ThrowWindow
                || settings.FilterThrows && !settings.Filter.Matches(collision.rigidbody, transform, filterBuffer))
                return;

            // The strongest normal contact drives the recoil. Sliding along a surface contributes little.
            Vector3 velocity = collision.relativeVelocity;
            Vector3 normal = Vector3.zero;
            float closing = 0f;
            for (int index = 0; index < collision.contactCount; index++)
            {
                Vector3 candidate = collision.GetContact(index).normal;
                float speed = Mathf.Abs(Vector3.Dot(velocity, candidate));
                if (speed <= closing)
                    continue;
                closing = speed;
                normal = candidate;
            }
            if (closing <= 0.01f)
                return;

            // Size can amplify a large, light object without replacing the measured physical impulse.
            float impact = Mathf.Max(collision.impulse.magnitude, closing * collision.rigidbody.mass);
            if (settings.VolumeInfluence > 0f)
                impact *= KnockbackImpact.VolumeMultiplier(thrown.Colliders, collision.collider, settings);
            if (impact < settings.MinimumImpact)
                return;
            BeginWobble(normal, Mathf.InverseLerp(settings.MinimumImpact, settings.MaximumImpact, impact));
        }

        /// <summary>Captures the rest pose and samples motion once at the collision boundary.</summary>
        /// <param name="normal">World contact normal pointing toward the thrown object.</param>
        /// <param name="impact">Effective impact normalized between the configured thresholds.</param>
        private void BeginWobble(Vector3 normal, float impact)
        {
            // A top-down hit still produces a readable sway using the receiver's forward direction.
            rest = new Pose(body.position, body.rotation);
            Vector3 up = rest.rotation * Vector3.up;
            direction = Vector3.ProjectOnPlane(-normal, up).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = rest.rotation * Vector3.forward;
            axis = Vector3.Cross(up, direction);
            distance = settings.Distance.Sample(impact);
            tilt = settings.Tilt.Sample(impact);
            duration = settings.Duration.Sample(impact);
            cycles = settings.Cycles.Sample(impact);
            damping = settings.Damping.Sample(impact);
            elapsed = 0f;
            reacting = true;
            Signal(InteractionMoment.Started);
        }

        /// <summary>Runs the short damped motion through the Rigidbody so colliders follow the wobble.</summary>
        private void FixedUpdate()
        {
            if (!reacting)
                return;
            if (!CanWobble())
            {
                reacting = false;
                return;
            }

            // The envelope reaches zero at the endpoint regardless of the number of sway cycles.
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Min(1f, elapsed / duration);
            float envelope = Mathf.Exp(-damping * progress) * (1f - Mathf.SmoothStep(0f, 1f, progress));
            float sway = Mathf.Sin(progress * cycles * (2f * Mathf.PI)) * envelope;
            body.Move(rest.position + direction * (distance * sway),
                Quaternion.AngleAxis(tilt * sway, axis) * rest.rotation);
            if (progress < 1f)
                return;
            reacting = false;
            Signal(InteractionMoment.Completed);
        }

        /// <summary>Checks that another interaction has not taken ownership of this body.</summary>
        /// <returns>True while the receiver can move as a kinematic body.</returns>
        private bool CanWobble()
        {
            return body != null && body.isKinematic && Item != null && !Item.IsCarried && !Item.IsReserved
                && !Item.IsConsumed && !BlackHoleRun.Contains(body);
        }

        #endregion

        #endregion
    }
}

