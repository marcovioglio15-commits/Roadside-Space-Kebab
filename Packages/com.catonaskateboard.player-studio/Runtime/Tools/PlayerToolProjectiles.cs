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
        private readonly PlayerToolVisualPool impacts;
        private readonly Flight[] flights;
        private readonly float[] lifetimes;
        private readonly Quaternion impactRotation;
        private readonly PlayerToolImpactPlacement placement;
        private readonly float lookAhead;
        private int activeBullets;
        private int activeImpacts;
        private int nextImpact;
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
        /// <param name="camera">Existing camera rig used by automatic impact placement.</param>
        /// <param name="shooter">Tools component owning the emitted shot identities.</param>
        internal PlayerToolProjectiles(PlayerTool identity, Transform player, PlayerToolPhysics queries, PlayerCameraRig camera, PlayerTools shooter)
        {
            tool = identity;
            owner = shooter;
            PlayerToolWeaponSettings weapon = settings = identity.Weapon;
            physics = queries;
            bullets = new PlayerToolVisualPool(weapon.Bullet, weapon.Capacity, player.gameObject.scene);
            flights = new Flight[weapon.Capacity];
            lifetimes = weapon.Impact != null ? new float[weapon.Capacity] : Array.Empty<float>();
            // Impact orientation is converted once; the source prefab is never modified.
            if (weapon.Impact != null)
            {
                bool faceCamera = weapon.AutomaticImpactPlacement && weapon.ImpactPlacementMode == PlayerToolImpactPlacementMode.FaceCamera;
                impacts = new PlayerToolVisualPool(weapon.Impact, weapon.Capacity, player.gameObject.scene, weapon.ImpactScale,
                    weapon.UsesImpactOrientation && weapon.ImpactSurfaceAligned && weapon.ImpactOrientation == PlayerToolImpactOrientation.SurfaceNormal,
                    weapon.AutomaticImpactPlacement ? weapon.ImpactLifetime : 0f, faceCamera);
                placement = weapon.AutomaticImpactPlacement ? new PlayerToolImpactPlacement(physics, camera, impacts.Envelope, faceCamera, weapon.ImpactPenetration) : null;
                lookAhead = placement != null ? impacts.Envelope.size.magnitude : 0f;
                impactRotation = weapon.UsesImpactOrientation ? (weapon.ImpactOrientation == PlayerToolImpactOrientation.PrefabRotation
                    ? weapon.Impact.transform.rotation : Quaternion.identity) * Quaternion.Euler(weapon.ImpactRotation) : Quaternion.identity;
            }
        }

        /// <summary>Releases every visual when the player is disabled or leaves the scene.</summary>
        public void Dispose()
        {
            disposed = true;
            bullets.Dispose();
            impacts?.Dispose();
        }

        #endregion

        #region Simulation

        /// <summary>Starts one flight from the muzzle with no inherited movement or gravity.</summary>
        /// <param name="position">World muzzle point.</param>
        /// <param name="rotation">World firing orientation, with forward along positive Z.</param>
        internal void Fire(Vector3 position, Quaternion rotation)
        {
            if (disposed)
                return;
            int slot = bullets.Rent(position, rotation);
            if (slot < 0)
                return;
            Vector3 direction = rotation * Vector3.forward;
            flights[slot] = new Flight
            {
                Position = position, Origin = position, Direction = direction, Remaining = settings.Distance, Speed = settings.Speed,
                ActionId = owner != null ? owner.UseActionId : Guid.Empty, TriggersInteractions = tool.InteractOnBulletImpact,
                Prediction = placement != null ? Preview(position, direction, settings.Distance) : default
            };
            activeBullets++;
        }

        /// <summary>Sweeps each live bullet across the complete physics step and expires pooled impacts.</summary>
        /// <param name="delta">Scaled physics step duration.</param>
        internal void Advance(float delta)
        {
            // Hit callbacks can release this pool during the preceding simulation step.
            if (disposed)
                return;
            // Effects spawned during this tick retain their full requested lifetime.
            if (activeImpacts > 0)
                for (int index = 0; index < lifetimes.Length; index++)
                    if (lifetimes[index] > 0f)
                    {
                        lifetimes[index] -= delta;
                        if (lifetimes[index] <= 0f)
                        {
                            impacts.Return(index);
                            activeImpacts--;
                        }
                    }
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
                if (placement != null && (flight.Prediction.HasImpact && (flight.Prediction.Hit && flight.Prediction.Contact.collider == null
                    || Vector3.Dot(flight.Prediction.Position - flight.Position, flight.Direction) <= travel)
                    || ahead && (hit || !flight.Prediction.Hit || contact.collider != flight.Prediction.Contact.collider
                        || (contact.point - flight.Prediction.Contact.point).sqrMagnitude > 0.000625f)))
                    flight.Prediction = Preview(flight.Position, flight.Direction, flight.Remaining);
                if (flight.Prediction.HasImpact && Vector3.Dot(flight.Prediction.Position - flight.Position, flight.Direction) <= travel)
                {
                    flight.Position = flight.Prediction.Position;
                    EmitImpact(flight.Prediction.Position, flight.Prediction.Rotation, flight.Prediction.Scale);
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
                    if (placement == null && (hit || settings.ImpactAtRange))
                        Impact(hit ? contact.point : flight.Position, hit ? contact.normal : -flight.Direction, flight.Direction);
                    Finish(index, hit, contact);
                }
                // A hit listener may disable its shooter and release these pools.
                if (disposed)
                    return;
            }
        }

        /// <summary>Places a bounded impact visual, recycling the oldest slot when all effects are occupied.</summary>
        /// <param name="position">Contact point or maximum-range endpoint.</param>
        /// <param name="normal">Surface normal, or reverse travel at maximum range.</param>
        /// <param name="direction">Bullet's world direction.</param>
        private void Impact(Vector3 position, Vector3 normal, Vector3 direction)
        {
            if (impacts == null)
                return;
            EmitImpact(position + normal * settings.ImpactOffset, OrientImpact(normal, direction), 1f);
        }

        /// <summary>Resolves the same authored surface, travel or prefab rotation for manual and automatic placement.</summary>
        /// <param name="normal">Confirmed contact normal, or reverse travel at maximum range.</param>
        /// <param name="direction">Normalized incoming shot direction.</param>
        /// <returns>World effect rotation including the authored additional Euler rotation.</returns>
        private Quaternion OrientImpact(Vector3 normal, Vector3 direction)
        {
            // Prefab orientation already includes its additional rotation in the cached offset.
            if (settings.ImpactOrientation == PlayerToolImpactOrientation.PrefabRotation)
                return impactRotation;
            Vector3 forward = settings.ImpactOrientation == PlayerToolImpactOrientation.SurfaceNormal ? normal : direction;
            return Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up) * impactRotation;
        }

        /// <summary>Uses the live bullet's collision and impact fitting rules without emitting a shot or an interaction.</summary>
        /// <param name="position">Proposed world muzzle position.</param>
        /// <param name="direction">Normalized proposed shot direction.</param>
        /// <param name="distance">Remaining straight flight distance.</param>
        /// <returns>A value snapshot shared by shot alignment and live simulation.</returns>
        internal PlayerToolShotPrediction Preview(Vector3 position, Vector3 direction, float distance)
        {
            // One physical sweep and the cached envelope serve both preparation and actual flight.
            bool hit = physics.Cast(position, direction, distance, settings.Radius, settings.Layers, settings.HitTriggers, out RaycastHit contact);
            Vector3 endpoint = position + direction * (hit ? contact.distance : distance);
            Quaternion rotation = Quaternion.identity;
            Vector3 impact = endpoint;
            float scale = 1f;
            bool fitted = placement != null && (hit || settings.ImpactAtRange) && placement.TryPlace(position, endpoint, direction,
                settings.UsesImpactOrientation ? OrientImpact(hit ? contact.normal : -direction, direction) : Quaternion.identity,
                out impact, out rotation, out scale);
            return new PlayerToolShotPrediction(hit, contact, fitted, fitted ? impact : endpoint, rotation, scale);
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

        /// <summary>Emits the already fitted visual at the same endpoint that removes its bullet.</summary>
        /// <param name="position">Verified world visual origin.</param>
        /// <param name="rotation">Final effect orientation.</param>
        /// <param name="scale">Additional fit scale relative to the pooled visual.</param>
        private void EmitImpact(Vector3 position, Quaternion rotation, float scale)
        {
            // Impact capacity cannot block or allocate a bullet shot.
            if (!impacts.HasSpace)
            {
                impacts.Return(nextImpact);
                lifetimes[nextImpact] = 0f;
                activeImpacts--;
                nextImpact = (nextImpact + 1) % lifetimes.Length;
            }
            int slot = impacts.Rent(position, rotation, scale);
            lifetimes[slot] = settings.ImpactLifetime;
            activeImpacts++;
        }

        #endregion

        #endregion
    }
}
