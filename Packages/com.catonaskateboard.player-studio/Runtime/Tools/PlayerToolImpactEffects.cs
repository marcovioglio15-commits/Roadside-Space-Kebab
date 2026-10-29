using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shares impact orientation, clearance and pooled playback between both weapon modes.</summary>
    internal sealed class PlayerToolImpactEffects : IDisposable
    {
        #region State

        private readonly PlayerToolWeaponSettings settings;
        private readonly PlayerToolPhysics physics;
        private readonly PlayerToolEffectPool effects;
        private readonly PlayerToolImpactPlacement placement;
        private readonly Quaternion rotationOffset;

        #endregion

        #region Properties

        /// <summary>Whether physical flights use a fitted visual endpoint before their surface contact.</summary>
        internal bool Automatic => placement != null;
        /// <summary>Additional collision look-ahead needed by the measured effect envelope.</summary>
        internal float LookAhead => Automatic ? effects.Envelope.size.magnitude : 0f;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Prewarms an optional impact pool and measures clearance only when automatic placement is enabled.</summary>
        /// <param name="weapon">Validated impact settings.</param>
        /// <param name="player">Player whose scene owns the effects.</param>
        /// <param name="queries">Shared owner-filtered physics queries.</param>
        /// <param name="camera">Existing rig supplying camera-facing placement.</param>
        internal PlayerToolImpactEffects(PlayerToolWeaponSettings weapon, Transform player, PlayerToolPhysics queries, PlayerCameraRig camera)
        {
            settings = weapon;
            physics = queries;
            if (weapon.Impact == null)
                return;
            bool faceCamera = weapon.AutomaticImpactPlacement && weapon.ImpactPlacementMode == PlayerToolImpactPlacementMode.FaceCamera;
            effects = new PlayerToolEffectPool(new PlayerToolVisualPool(weapon.Impact, weapon.Capacity, player.gameObject.scene,
                weapon.ImpactScale, weapon.UsesImpactOrientation && weapon.ImpactSurfaceAligned
                    && weapon.ImpactOrientation == PlayerToolImpactOrientation.SurfaceNormal,
                weapon.AutomaticImpactPlacement ? weapon.ImpactLifetime : 0f, faceCamera), weapon.Capacity, weapon.ImpactLifetime);
            placement = weapon.AutomaticImpactPlacement
                ? new PlayerToolImpactPlacement(physics, camera, effects.Envelope, faceCamera, weapon.ImpactPenetration) : null;
            rotationOffset = weapon.UsesImpactOrientation ? (weapon.ImpactOrientation == PlayerToolImpactOrientation.PrefabRotation
                ? weapon.Impact.transform.rotation : Quaternion.identity) * Quaternion.Euler(weapon.ImpactRotation) : Quaternion.identity;
        }

        /// <summary>Releases impact instances at player shutdown.</summary>
        public void Dispose()
        {
            effects?.Dispose();
        }

        /// <summary>Expires visible impacts independently of the currently selected tool.</summary>
        /// <param name="delta">Scaled physics interval.</param>
        internal void Advance(float delta)
        {
            effects?.Advance(delta);
        }

        #endregion

        #region Placement

        /// <summary>Places a confirmed impact, retaining the same penetration tolerance in either weapon mode.</summary>
        /// <param name="origin">World muzzle position at emission.</param>
        /// <param name="position">Confirmed contact point or projectile range endpoint.</param>
        /// <param name="normal">Surface normal or reverse projectile travel.</param>
        /// <param name="direction">Normalized incoming shot direction.</param>
        internal void Emit(Vector3 origin, Vector3 position, Vector3 normal, Vector3 direction)
        {
            if (effects == null)
                return;
            // Fitting affects only the visual; interaction ownership keeps the original contact.
            if (placement == null)
                effects.Emit(position + normal * settings.ImpactOffset, Orient(normal, direction));
            else if (placement.TryPlace(origin, position, direction, settings.UsesImpactOrientation ? Orient(normal, direction) : Quaternion.identity,
                out Vector3 fitted, out Quaternion rotation, out float scale))
                effects.Emit(fitted, rotation, scale);
        }

        /// <summary>Replays a verified projectile prediction without repeating clearance queries.</summary>
        /// <param name="prediction">Fitted endpoint shared by the flight and its visible impact.</param>
        internal void Emit(PlayerToolShotPrediction prediction)
        {
            effects?.Emit(prediction.Position, prediction.Rotation, prediction.Scale);
        }

        /// <summary>Predicts a physical flight using the same geometry and fitting as live simulation.</summary>
        /// <param name="position">Proposed world muzzle position.</param>
        /// <param name="direction">Normalized proposed shot direction.</param>
        /// <param name="distance">Remaining projectile range.</param>
        /// <returns>Physical contact and optional fitted visual endpoint.</returns>
        internal PlayerToolShotPrediction Preview(Vector3 position, Vector3 direction, float distance)
        {
            bool hit = physics.Cast(position, direction, distance, settings.Radius, settings.Layers, settings.HitTriggers, out RaycastHit contact);
            Vector3 endpoint = position + direction * (hit ? contact.distance : distance);
            Vector3 impact = endpoint;
            Quaternion rotation = Quaternion.identity;
            float scale = 1f;
            bool fitted = placement != null && (hit || settings.ImpactAtRange) && placement.TryPlace(position, endpoint, direction,
                settings.UsesImpactOrientation ? Orient(hit ? contact.normal : -direction, direction) : Quaternion.identity,
                out impact, out rotation, out scale);
            return new PlayerToolShotPrediction(hit, contact, fitted, fitted ? impact : endpoint, rotation, scale);
        }

        /// <summary>Resolves the configured surface, travel or prefab rotation for manual and fitted placement.</summary>
        /// <param name="normal">Confirmed surface normal.</param>
        /// <param name="direction">Normalized incoming direction.</param>
        /// <returns>World orientation including the additional Euler offset.</returns>
        private Quaternion Orient(Vector3 normal, Vector3 direction)
        {
            if (settings.ImpactOrientation == PlayerToolImpactOrientation.PrefabRotation)
                return rotationOffset;
            Vector3 forward = settings.ImpactOrientation == PlayerToolImpactOrientation.SurfaceNormal ? normal : direction;
            return Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up) * rotationOffset;
        }

        #endregion

        #endregion
    }
}
