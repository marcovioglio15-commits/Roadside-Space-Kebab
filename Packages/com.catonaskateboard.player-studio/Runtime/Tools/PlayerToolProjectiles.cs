using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Simulates bounded straight projectiles and timed impact effects without per-shot allocation.</summary>
    internal sealed class PlayerToolProjectiles : IDisposable
    {
        #region State

        /// <summary>Retains one bullet's independent world-space flight state.</summary>
        private struct Flight
        {
            internal float Speed;
            internal Vector3 Position;
            internal Vector3 Direction;
            internal float Remaining;
            internal Vector3 Origin;
            internal Guid ActionId;
            internal bool TriggersInteractions;
            internal PlayerToolShotPrediction Prediction;
        }

        private readonly PlayerToolWeaponSettings settings;
        private readonly PlayerTool tool;
        private readonly PlayerTools owner;
        private readonly PlayerToolPhysics physics;
        private readonly PlayerToolVisualPool bullets;
        private readonly PlayerToolImpactEffects impacts;
        private readonly Flight[] flights;
        private readonly float lookAhead;
        private int activeBullets;
        private bool disposed;

        #endregion

        #region Properties

        /// <summary>Whether a shot can reserve a bullet without expanding its pool.</summary>
        internal bool CanFire => !disposed && bullets.HasSpace;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Prepares bullet and optional impact pools in the firing player's scene.</summary>
        /// <param name="identity">Tool supplying validated projectile settings.</param>
        /// <param name="player">Owner excluded from collision queries.</param>
        /// <param name="queries">Shared non-allocating physics queries for this tool.</param>
        /// <param name="effects">Shared impact placement and playback owned by the weapon.</param>
        /// <param name="shooter">Tools component owning the emitted shot identities.</param>
        internal PlayerToolProjectiles(PlayerTool identity, Transform player, PlayerToolPhysics queries, PlayerToolImpactEffects effects, PlayerTools shooter)
        {
            tool = identity;
            owner = shooter;
            PlayerToolWeaponSettings weapon = settings = identity.Weapon;
            physics = queries;
            bullets = new PlayerToolVisualPool(weapon.Bullet, weapon.Capacity, player.gameObject.scene);
            flights = new Flight[weapon.Capacity];
            impacts = effects;
            lookAhead = effects.LookAhead;
        }

        /// <summary>Releases every visual when the player is disabled or leaves the scene.</summary>
        public void Dispose()
        {
            disposed = true;
            bullets.Dispose();
        }

        #endregion

        #region Simulation

        /// <summary>Starts one flight from the muzzle with no inherited movement or gravity.</summary>
        /// <param name="position">World muzzle point.</param>
        /// <param name="rotation">World firing orientation, with forward along positive Z.</param>
        /// <returns>True when a bullet slot was reserved and emitted.</returns>
        internal bool Fire(Vector3 position, Quaternion rotation)
        {
            if (disposed)
                return false;
            int slot = bullets.Rent(position, rotation);
            if (slot < 0)
                return false;
            Vector3 direction = rotation * Vector3.forward;
            flights[slot] = new Flight
            {
                Position = position, Origin = position, Direction = direction, Remaining = settings.Distance, Speed = settings.Speed,
                ActionId = owner != null ? owner.UseActionId : Guid.Empty, TriggersInteractions = tool.InteractOnBulletImpact,
                Prediction = impacts.Automatic ? Preview(position, direction, settings.Distance) : default
            };
            activeBullets++;
            return true;
        }

        /// <summary>Sweeps each live bullet across the complete physics step and expires pooled impacts.</summary>
        /// <param name="delta">Scaled physics step duration.</param>
        internal void Advance(float delta)
        {
            // Hit callbacks can release this pool during the preceding simulation step.
            if (disposed)
                return;
            if (activeBullets == 0)
                return;
            // Swept contacts prevent fast projectiles from passing through thin surfaces.
            for (int index = 0; index < flights.Length; index++)
            {
                if (flights[index].Remaining <= 0f)
                    continue;
                ref Flight flight = ref flights[index];
                float travel = Mathf.Min(flight.Speed * delta, flight.Remaining);
                bool ahead = physics.Cast(flight.Position, flight.Direction, Mathf.Min(flight.Remaining, travel + lookAhead), settings.Radius, settings.Layers,
                    settings.HitTriggers, out RaycastHit contact);
                bool hit = ahead && contact.distance <= travel;
                // Revalidate at visual arrival, after target removal or when a new surface enters the approach volume.
                if (impacts.Automatic && (flight.Prediction.HasImpact && (flight.Prediction.Hit && flight.Prediction.Contact.collider == null
                    || Vector3.Dot(flight.Prediction.Position - flight.Position, flight.Direction) <= travel)
                    || ahead && (hit || !flight.Prediction.Hit || contact.collider != flight.Prediction.Contact.collider
                        || (contact.point - flight.Prediction.Contact.point).sqrMagnitude > 0.000625f)))
                    flight.Prediction = Preview(flight.Position, flight.Direction, flight.Remaining);
                if (flight.Prediction.HasImpact && Vector3.Dot(flight.Prediction.Position - flight.Position, flight.Direction) <= travel)
                {
                    flight.Position = flight.Prediction.Position;
                    impacts.Emit(flight.Prediction);
                    Finish(index, flight.Prediction.Hit, flight.Prediction.Contact);
                }
                else
                {
                    flight.Position += flight.Direction * (hit ? contact.distance : travel);
                    flight.Remaining -= travel;
                    if (!hit && flight.Remaining > 0f)
                    {
                        bullets.Move(index, flight.Position);
                        continue;
                    }
                    if (!impacts.Automatic && (hit || settings.ImpactAtRange))
                        impacts.Emit(flight.Origin, hit ? contact.point : flight.Position, hit ? contact.normal : -flight.Direction, flight.Direction);
                    Finish(index, hit, contact);
                }
                // A hit listener may disable its shooter and release these pools.
                if (disposed)
                    return;
            }
        }

        /// <summary>Uses the live bullet's collision and impact fitting rules without emitting a shot or an interaction.</summary>
        /// <param name="position">Proposed world muzzle position.</param>
        /// <param name="direction">Normalized proposed shot direction.</param>
        /// <param name="distance">Remaining straight flight distance.</param>
        /// <returns>A value snapshot shared by shot alignment and live simulation.</returns>
        internal PlayerToolShotPrediction Preview(Vector3 position, Vector3 direction, float distance)
        {
            return impacts.Preview(position, direction, distance);
        }

        /// <summary>Returns one visible bullet and publishes only a currently confirmed physical target.</summary>
        /// <param name="index">Live bullet slot.</param>
        /// <param name="hit">Whether the final sweep confirmed a collider.</param>
        /// <param name="contact">Confirmed target surface, independent of visual clearance retreat.</param>
        private void Finish(int index, bool hit, RaycastHit contact)
        {
            // Capture the payload before callbacks can reuse the released slot or disable the shooter.
            PlayerToolHit result = new PlayerToolHit(owner, tool, flights[index].ActionId, flights[index].TriggersInteractions,
                contact, flights[index].Origin, flights[index].Direction, flights[index].Speed);
            flights[index].Remaining = 0f;
            bullets.Return(index);
            activeBullets--;
            if (hit)
                PlayerTools.PublishHit(result);
        }

        #endregion

        #endregion
    }
}
