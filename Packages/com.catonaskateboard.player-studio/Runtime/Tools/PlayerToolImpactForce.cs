using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Applies a weapon impulse at the confirmed surface in both firing modes.</summary>
    internal sealed class PlayerToolImpactForce
    {
        #region State

        private readonly List<IPlayerToolImpactTarget> interactions = new List<IPlayerToolImpactTarget>(4);
        private readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

        #endregion

        #region Methods

        #region Contact

        /// <summary>Starts a new shot so compound bodies receive one push.</summary>
        internal void Begin() => pushed.Clear();

        /// <summary>Pushes a hit body once when it passes every enabled filter group.</summary>
        /// <param name="settings">Impulse and target filters selected by the weapon.</param>
        /// <param name="collider">Confirmed primary or nearby surface.</param>
        /// <param name="point">World point on that surface.</param>
        /// <param name="direction">Normalized shot direction or direction out from the blast centre.</param>
        /// <param name="strength">Relative force after area falloff.</param>
        /// <param name="radial">Whether a nearby target already has an outward direction.</param>
        internal void Apply(PlayerToolImpactForceSettings settings, Collider collider, Vector3 point, Vector3 direction, float strength, bool radial)
        {
            // A body has to pass the shot's filters before it can move or react to the simulated collision.
            Rigidbody body = collider != null ? collider.attachedRigidbody : null;
            if (body == null || collider.isTrigger || pushed.Contains(body)
                || settings.FilterLayers && (settings.Layers.value & (1 << collider.gameObject.layer)) == 0)
                return;
            bool carried = (settings.IgnoreCarriedObjects || body.isKinematic)
                && body.TryGetComponent(out IPlayerToolShotOwner ownership) && ownership.IsCarried;
            if (settings.IgnoreCarriedObjects && carried || body.isKinematic && (!carried || !settings.AffectImpactDegradation))
                return;
            if (settings.FilterFlags && !MatchesFlags(settings, collider.transform, body.transform)
                || settings.Interactions != PlayerToolImpactInteraction.None && !MatchesInteractions(settings.Interactions, collider.transform, body.transform))
                return;
            pushed.Add(body);
            float impulse = settings.Impulse * strength;
            if (settings.ExplosiveResponse)
                impulse = Burst(settings, body, direction, strength, radial);
            else if (!body.isKinematic)
                body.AddForceAtPosition(direction * impulse, point, ForceMode.Impulse);
            // Scale the reported impact after applying the physical response and area falloff.
            if (settings.AffectImpactDegradation && settings.DegradationImpulseMultiplier > 0f
                && body.TryGetComponent(out IPlayerToolImpactReceiver receiver))
                receiver.ReceiveToolImpact(impulse * settings.DegradationImpulseMultiplier);
        }

        /// <summary>Gives a body a consistent launch and spin regardless of its mass or shape.</summary>
        /// <param name="settings">Burst speed, lift and tumble.</param>
        /// <param name="body">Dynamic body receiving the launch.</param>
        /// <param name="direction">Incoming shot or outward blast direction.</param>
        /// <param name="strength">Relative force after area falloff.</param>
        /// <param name="radial">Whether the launch must fan away from the impact centre.</param>
        /// <returns>The body's mass times the applied speed change.</returns>
        private static float Burst(PlayerToolImpactForceSettings settings, Rigidbody body, Vector3 direction, float strength, bool radial)
        {
            // Direct hits fan in every horizontal direction with a modest bias along the shot.
            Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (!radial || horizontal.sqrMagnitude < 0.001f)
                horizontal = (Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward + horizontal * 0.4f).normalized;
            settings.SampleBurst(horizontal, strength, out Vector3 velocity, out Vector3 tumble);
            if (!body.isKinematic)
            {
                body.AddForce(velocity, ForceMode.VelocityChange);
                if (tumble.sqrMagnitude > 0f)
                    body.AddTorque(tumble, ForceMode.VelocityChange);
            }
            return body.mass * velocity.magnitude;
        }

        /// <summary>Looks for matching identity flags in this collider's compound body.</summary>
        /// <param name="settings">Flags and Any or All matching rule.</param>
        /// <param name="hit">Transform of the struck collider.</param>
        /// <param name="root">Transform that owns the attached Rigidbody.</param>
        /// <returns>True when an identity on the hit path matches the selected flags.</returns>
        private static bool MatchesFlags(PlayerToolImpactForceSettings settings, Transform hit, Transform root)
        {
            // Stop at the dynamic body so its parent furniture cannot supply unrelated flags.
            for (Transform node = hit; node != null; node = node.parent)
            {
                if (node.TryGetComponent(out ObjectIdentity identity) && identity.Matches(settings.Flags, settings.FlagMatch))
                    return true;
                if (node == root)
                    break;
            }
            return false;
        }

        /// <summary>Looks for matching cards from the struck collider up to its compound body root.</summary>
        /// <param name="mask">Interaction categories accepted by this weapon.</param>
        /// <param name="hit">Transform of the struck collider.</param>
        /// <param name="root">Transform that owns the attached Rigidbody.</param>
        /// <returns>True when this collider or a parent on the same body has a matching active card.</returns>
        private bool MatchesInteractions(PlayerToolImpactInteraction mask, Transform hit, Transform root)
        {
            // Reuse one list for every contact and discard references after scanning each node.
            for (Transform node = hit; node != null; node = node.parent)
            {
                node.GetComponents(interactions);
                bool matches = false;
                foreach (IPlayerToolImpactTarget candidate in interactions)
                    if ((candidate.ImpactInteraction & mask) != 0)
                    {
                        matches = true;
                        break;
                    }
                interactions.Clear();
                if (matches || node == root)
                    return matches;
            }
            return false;
        }

        #endregion

        #endregion
    }
}
