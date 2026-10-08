using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Integrates overlapping suspension forces without adding a Rigidbody or moving the Transform directly.</summary>
    internal sealed class PlayerFloatingMotion
    {
        #region State

        /// <summary>Retains one source's sampled acceleration until its authored force duration expires.</summary>
        private sealed class Lease
        {
            internal readonly Object Owner;
            internal readonly PlayerFloatSettings Settings;
            internal readonly Vector3 Force;
            internal float Remaining;

            #region Methods

            /// <summary>Captures one source at the pulse boundary.</summary>
            /// <param name="owner">Source responsible for releasing suspension.</param>
            /// <param name="settings">Validated virtual-body settings.</param>
            /// <param name="force">Sampled world acceleration.</param>
            /// <param name="duration">Remaining sustained-force time.</param>
            internal Lease(Object owner, PlayerFloatSettings settings, Vector3 force, float duration)
            {
                Owner = owner;
                Settings = settings;
                Force = force;
                Remaining = duration;
            }

            #endregion
        }

        private readonly List<Lease> leases = new List<Lease>();
        private Vector3 velocity;

        #endregion
        #region Properties

        /// <summary>Whether at least one source still suspends normal gravity.</summary>
        internal bool Active => leases.Count > 0;
        /// <summary>Integrated floating velocity after force and contact response.</summary>
        internal Vector3 Velocity => velocity;
        /// <summary>Body and control policy of the most recently acquired active source.</summary>
        internal PlayerFloatSettings Settings => Active ? leases[leases.Count - 1].Settings : null;

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Adds one independent suspension; only the first acquisition imports locomotion momentum.</summary>
        /// <param name="owner">Unique suspension source.</param>
        /// <param name="settings">Validated body policy.</param>
        /// <param name="initial">Current achieved player velocity.</param>
        /// <param name="force">World impulse or acceleration, before the player multiplier.</param>
        /// <param name="duration">Zero for an impulse, otherwise sustained-force seconds.</param>
        /// <returns>True when a new source was acquired.</returns>
        internal bool Acquire(Object owner, PlayerFloatSettings settings, Vector3 initial, Vector3 force, float duration)
        {
            foreach (Lease lease in leases)
                if (lease.Owner == owner)
                    return false;
            if (!Active)
                velocity = initial * settings.InitialMomentum;
            force *= settings.ForceMultiplier;
            if (duration <= 0f)
                velocity += force / settings.Mass;
            velocity = Vector3.ClampMagnitude(velocity, settings.MaximumSpeed);
            leases.Add(new Lease(owner, settings, force, duration));
            return true;
        }

        /// <summary>Removes only one source, retaining floating motion until all owners have released it.</summary>
        /// <param name="owner">Suspension source being restored or cancelled.</param>
        /// <param name="restored">Receives momentum for ordinary locomotion when the final source leaves.</param>
        /// <returns>True when the last source was removed.</returns>
        internal bool Release(Object owner, out Vector3 restored)
        {
            restored = Vector3.zero;
            for (int index = leases.Count - 1; index >= 0; index--)
                if (leases[index].Owner == owner)
                {
                    restored = velocity * Settings.RestoredMomentum;
                    leases.RemoveAt(index);
                    return !Active;
                }
            return false;
        }

        /// <summary>Abandons all sources when the motor is disabled or explicitly reinitialized.</summary>
        internal void Reset()
        {
            leases.Clear();
            velocity = Vector3.zero;
        }

        #endregion
        #region Simulation

        /// <summary>Integrates cached forces and input; collision resolution remains the motor's single Move call.</summary>
        /// <param name="command">World-space movement intent, bounded to unit length.</param>
        /// <param name="jump">Existing Jump signal used only when thrust is enabled.</param>
        /// <param name="delta">Positive scaled frame duration.</param>
        /// <returns>Requested displacement for the current frame.</returns>
        internal Vector3 Advance(Vector3 command, PlayerButtonSignal jump, float delta)
        {
            PlayerFloatSettings policy = Settings;
            Vector3 acceleration = policy.DriftAcceleration + Vector3.ClampMagnitude(command, 1f) * policy.ControlAcceleration;
            if (policy.JumpThrust && jump.IsActive && !jump.Interrupted && (jump.Held || jump.Pressed))
                acceleration += Vector3.up * policy.JumpAcceleration;
            // A force ending inside this frame contributes only its remaining time.
            foreach (Lease lease in leases)
                if (lease.Remaining > 0f)
                {
                    float duration = Mathf.Min(delta, lease.Remaining);
                    acceleration += lease.Force * (duration / delta);
                    lease.Remaining -= duration;
                }
            Vector3 previous = velocity;
            float decay = Mathf.Exp(-policy.Damping * delta);
            velocity = previous * decay + acceleration * (policy.Damping > 0.00001f ? (1f - decay) / policy.Damping : delta);
            velocity = Vector3.ClampMagnitude(velocity, policy.MaximumSpeed);
            return (previous + velocity) * (0.5f * delta);
        }

        /// <summary>Resolves inward velocity against the actual controller contact normal.</summary>
        /// <param name="normal">World normal from an existing CharacterController collision.</param>
        /// <param name="body">Contacted body, or null for static geometry.</param>
        internal void Contact(Vector3 normal, Rigidbody body = null)
        {
            if (!Active)
                return;
            if (Push(body, normal))
                return;
            float inward = Vector3.Dot(velocity, normal);
            if (inward >= 0f)
                return;
            velocity = (velocity - normal * inward) * (1f - Settings.ContactFriction) - normal * inward * Settings.Bounce;
        }

        /// <summary>Transfers closing speed to an explicitly suspended body using a scaled collision mass.</summary>
        /// <param name="body">Contacted body, including null for static surfaces.</param>
        /// <param name="normal">Outward world normal toward the player.</param>
        /// <returns>True when the suspended-body policy handled this contact.</returns>
        private bool Push(Rigidbody body, Vector3 normal)
        {
            // Never infer suspension from disabled gravity: scenery and authored floating obstacles still block.
            if (!Settings.PushSuspendedBodies || body == null || body.isKinematic
                || (body.constraints & RigidbodyConstraints.FreezePosition) == RigidbodyConstraints.FreezePosition)
                return false;
            foreach (Lease lease in leases)
                if (lease.Owner is IPlayerSuspensionSource source && source.IsBodySuspended(body))
                {
                    float closing = -Vector3.Dot(velocity - body.linearVelocity, normal);
                    if (closing > 0f)
                    {
                        // Updating velocity once makes repeated compound contacts observe the transferred speed.
                        float share = (float)(Settings.Mass / (Settings.Mass + (double)body.mass * Settings.SuspendedMassScale));
                        body.linearVelocity -= normal * (closing * share * Settings.SuspendedPushSpeed);
                        velocity += normal * (closing * (1f - share));
                        body.WakeUp();
                    }
                    return true;
                }
            return false;
        }

        #endregion
        #endregion
    }
}
