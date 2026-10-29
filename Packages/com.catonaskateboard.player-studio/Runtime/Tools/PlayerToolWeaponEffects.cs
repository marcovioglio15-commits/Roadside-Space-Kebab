using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Binds optional arm and firing-point bursts once and owns the shared impact effects.</summary>
    internal sealed class PlayerToolWeaponEffects : IDisposable
    {
        #region State

        private readonly PlayerToolWeaponSettings settings;
        private readonly Transform arm;
        private readonly Transform muzzle;
        private readonly PlayerToolEffectPool armEffects;
        private readonly PlayerToolEffectPool shotEffects;
        private readonly Quaternion armRotation;
        private readonly Quaternion shotRotation;

        #endregion

        #region Properties

        /// <summary>Impact placement and playback shared with physical flights and confirmed cursor interactions.</summary>
        internal PlayerToolImpactEffects Impacts { get; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Prepares only assigned effects after the complete player's bindings pass validation.</summary>
        /// <param name="weapon">Validated weapon settings.</param>
        /// <param name="root">Existing Tools hierarchy root.</param>
        /// <param name="player">Player owning the pools.</param>
        /// <param name="physics">Shared collision queries.</param>
        /// <param name="camera">Existing view rig used by impact fitting.</param>
        internal PlayerToolWeaponEffects(PlayerToolWeaponSettings weapon, Transform root, Transform player,
            PlayerToolPhysics physics, PlayerCameraRig camera)
        {
            settings = weapon;
            muzzle = PlayerHierarchy.Resolve(root, weapon.MuzzlePath);
            arm = weapon.MuzzleEffect?.Prefab != null ? PlayerHierarchy.Resolve(root, weapon.MuzzleEffect.Path) : null;
            armEffects = Prepare(weapon.MuzzleEffect, weapon.Capacity, player);
            shotEffects = Prepare(weapon.ShotEffect, weapon.Capacity, player);
            armRotation = armEffects != null ? Quaternion.Euler(weapon.MuzzleEffect.Rotation) : Quaternion.identity;
            shotRotation = shotEffects != null ? Quaternion.Euler(weapon.ShotEffect.Rotation) : Quaternion.identity;
            Impacts = new PlayerToolImpactEffects(weapon, player, physics, camera);
        }

        /// <summary>Creates one bounded burst pool only when its prefab is assigned.</summary>
        /// <param name="effect">Optional burst settings.</param>
        /// <param name="capacity">Shared validated pool capacity.</param>
        /// <param name="player">Player supplying scene ownership.</param>
        /// <returns>A prepared timed pool, or null for a disabled burst.</returns>
        private static PlayerToolEffectPool Prepare(PlayerToolEffectSettings effect, int capacity, Transform player)
        {
            return effect?.Prefab != null ? new PlayerToolEffectPool(new PlayerToolVisualPool(effect.Prefab, capacity,
                player.gameObject.scene, effect.Scale), capacity, effect.Lifetime) : null;
        }

        /// <summary>Releases every optional effect without modifying the source prefabs.</summary>
        public void Dispose()
        {
            armEffects?.Dispose();
            shotEffects?.Dispose();
            Impacts.Dispose();
        }

        #endregion

        #region Playback

        /// <summary>Plays the two independent shot bursts at their current attachment poses.</summary>
        /// <param name="position">Final world firing point.</param>
        /// <param name="rotation">Final world shot orientation.</param>
        internal void Emit(Vector3 position, Quaternion rotation)
        {
            // Parenting follows the existing animated arm without creating a transform updater.
            if (armEffects != null && arm != null)
                armEffects.Emit(arm.TransformPoint(settings.MuzzleEffect.Position), arm.rotation * armRotation, parent: arm);
            if (shotEffects != null && muzzle != null)
                shotEffects.Emit(position + rotation * settings.ShotEffect.Position, rotation * shotRotation, parent: muzzle);
        }

        /// <summary>Expires all visible bursts, including effects from parked tools.</summary>
        /// <param name="delta">Scaled physics interval.</param>
        internal void Advance(float delta)
        {
            armEffects?.Advance(delta);
            shotEffects?.Advance(delta);
            Impacts.Advance(delta);
        }

        #endregion

        #endregion
    }
}
