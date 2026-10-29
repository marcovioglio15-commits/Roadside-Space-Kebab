using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Owns one player's action, cursor aim and projectile state for a reusable tool identity.</summary>
    internal sealed class PlayerToolRuntime : IDisposable
    {
        #region State

        private readonly PlayerTool tool;
        private readonly PlayerToolActionRun action;
        private readonly PlayerToolAim aim;
        private readonly PlayerToolPhysics physics;
        private readonly PlayerCameraRig camera;
        private readonly Transform root;
        private readonly PlayerToolMagazine magazine;
        private PlayerToolProjectiles projectiles;
        private PlayerToolWeaponEffects effects;
        private PlayerTools owner;
        private float nextUse;
        private float nextShot;
        private int remainingShots;
        private bool autoReloadPending;
        private bool preparingShot;
        private bool alignmentReady;

        #endregion

        #region Properties

        /// <summary>Whether a use still owns unspent shots in its firing sequence.</summary>
        private bool PendingShot => remainingShots > 0;
        /// <summary>Rounds available in this player's magazine, or minus one for unlimited use.</summary>
        internal int Ammunition => magazine.Remaining;
        /// <summary>Whether a temporary use or reload sequence currently owns the tool.</summary>
        internal bool IsBusy => action.IsBusy || PendingShot || autoReloadPending;
        /// <summary>Whether the reload combo is playing.</summary>
        internal bool IsReloading => action.IsReloading;
        /// <summary>Whether another use is available under cooldown and pool limits.</summary>
        internal bool CanUse => !IsBusy && magazine.CanFire && Time.time >= nextUse && (projectiles == null || projectiles.CanFire);
        /// <summary>Whether a manual reload can start and, for a limited magazine, has ammunition to refill.</summary>
        internal bool CanReload => !IsBusy && tool.ReloadEnabled && (!magazine.Limited || magazine.NeedsReload);

        #endregion

        #region Methods

        #region Binding

        /// <summary>Binds animation and weapon targets without allocating projectile visuals yet.</summary>
        /// <param name="identity">Tool identity and behavior.</param>
        /// <param name="root">Tools hierarchy root.</param>
        /// <param name="player">Player root used to exclude self collisions.</param>
        /// <param name="camera">Existing camera rig used by optional auto aim.</param>
        internal PlayerToolRuntime(PlayerTool identity, Transform root, Transform player, PlayerCameraRig camera)
        {
            tool = identity;
            this.root = root;
            this.camera = camera;
            magazine = new PlayerToolMagazine(identity);
            action = new PlayerToolActionRun(identity, root);
            if (!identity.IsWeapon)
                return;
            physics = new PlayerToolPhysics(player, identity.Weapon.Capacity);
            aim = new PlayerToolAim(identity.Weapon, root, camera, physics);
        }

        /// <summary>Validates every animation and muzzle binding before a pool is created.</summary>
        /// <param name="moved">Unique transforms restored when the module stops.</param>
        /// <param name="player">Protected player root.</param>
        /// <returns>True when all enabled bindings resolve.</returns>
        internal bool CollectTargets(HashSet<Transform> moved, Transform player)
        {
            // Cursor mode needs a view even when the visible arm does not rotate toward it.
            return action.CollectTargets(moved, player) && (aim == null || aim.CollectTargets(moved, player))
                && (!tool.IsWeapon || (tool.Weapon.Mode != PlayerToolWeaponMode.CursorInteraction || camera != null && camera.View != null)
                    && (tool.Weapon.MuzzleEffect?.Prefab == null || PlayerHierarchy.Resolve(root, tool.Weapon.MuzzleEffect.Path) != null));
        }

        /// <summary>Prewarms visuals only after the complete player configuration has passed binding.</summary>
        /// <param name="player">Player whose scene owns the pools.</param>
        internal void Prepare(Transform player)
        {
            if (tool.IsWeapon)
            {
                owner = player.GetComponent<PlayerTools>();
                effects = new PlayerToolWeaponEffects(tool.Weapon, root, player, physics, camera);
                if (tool.Weapon.Mode == PlayerToolWeaponMode.Projectile)
                    projectiles = new PlayerToolProjectiles(tool, player, physics, effects.Impacts, owner);
            }
        }

        /// <summary>Releases temporary poses and owned projectile pools at module shutdown.</summary>
        public void Dispose()
        {
            // Cancel queued work before releasing the visual state.
            remainingShots = 0;
            autoReloadPending = preparingShot = alignmentReady = false;
            aim?.Restore();
            action.Stop();
            projectiles?.Dispose();
            effects?.Dispose();
        }

        #endregion

        #region Commands and Playback

        /// <summary>Starts use keyframes and schedules the actual muzzle shot after camera following.</summary>
        internal void Use()
        {
            // Keep the tool reserved until its optional automatic combo has completed.
            nextUse = Time.time + tool.UseInterval;
            remainingShots = tool.IsWeapon ? tool.Weapon.ShotsPerUse : 0;
            nextShot = Time.time;
            preparingShot = tool.IsWeapon && tool.Weapon.UsesShotAlignment;
            alignmentReady = false;
            autoReloadPending = tool.ReloadEnabled && tool.AutoReload && !magazine.Limited;
            action.Begin(false);
        }

        /// <summary>Starts a manual reload without refilling ammunition before its final animation completes.</summary>
        /// <returns>True when the combo started.</returns>
        internal bool Reload()
        {
            if (!CanReload)
                return false;
            BeginReload();
            return true;
        }

        /// <summary>Starts the combo and handles a valid zero-duration reload in the same call.</summary>
        private void BeginReload()
        {
            action.Begin(true);
            if (!action.IsReloading)
                magazine.Refill();
        }

        /// <summary>Removes the previous frame's procedural aim before action keyframes are sampled.</summary>
        internal void RestoreAim()
        {
            aim?.Restore();
        }

        /// <summary>Clears presentation smoothing when switching away from this tool.</summary>
        internal void Park()
        {
            aim?.Reset();
        }

        /// <summary>Advances action tracks and starts a queued reload after use and muzzle emission finish.</summary>
        /// <param name="delta">Scaled frame duration.</param>
        internal void Advance(float delta)
        {
            // Hold the configured first use pose until the arm has reached its prepared shooting position.
            if (preparingShot)
            {
                if (alignmentReady)
                {
                    preparingShot = false;
                    nextUse = Time.time + tool.UseInterval;
                }
                return;
            }
            bool reloading = action.IsReloading;
            action.Advance(delta);
            if (reloading && !action.IsReloading)
                magazine.Refill();
            // Even an instant use must emit its shot before reload moves the muzzle.
            if (!autoReloadPending || action.IsBusy || PendingShot)
                return;
            autoReloadPending = false;
            BeginReload();
        }

        /// <summary>Samples aiming and emits a pending shot from this frame's final muzzle pose.</summary>
        /// <param name="delta">Scaled frame duration.</param>
        internal void Present(float delta)
        {
            alignmentReady = aim == null || aim.Sample(delta, PendingShot && tool.Weapon.UsesShotAlignment ? projectiles : null);
            if (PendingShot && tool.Weapon.UsesShotAlignment && !alignmentReady)
                preparingShot = true;
            if (preparingShot)
                return;
            if (!PendingShot || Time.time < nextShot)
                return;
            // One emission per presentation avoids catch-up bursts after a slow frame.
            if (aim != null && aim.TryGetShot(out Vector3 position, out Quaternion rotation))
            {
                if (projectiles != null && !projectiles.Fire(position, rotation))
                {
                    remainingShots = 0;
                    return;
                }
                magazine.Spend();
                remainingShots = magazine.CanFire ? remainingShots - 1 : 0;
                nextShot = Time.time + tool.Weapon.ShotInterval;
                autoReloadPending |= tool.ReloadEnabled && tool.AutoReload && !magazine.CanFire;
                effects.Emit(position, rotation);
                if (tool.Weapon.Mode == PlayerToolWeaponMode.CursorInteraction)
                    FireCursor(position, rotation);
            }
            else
                remainingShots = 0;
            aim?.FinishShotAlignment();
        }

        /// <summary>Captures the exact pointer contact after camera following without allocating a projectile.</summary>
        /// <param name="position">Current world muzzle position.</param>
        /// <param name="rotation">Current positive-Z shot orientation.</param>
        private void FireCursor(Vector3 position, Quaternion rotation)
        {
            if (camera == null || !camera.isActiveAndEnabled || !PlayerToolPointer.TryRay(camera.View, out Ray ray))
                return;
            // A screen ray uses the same culling and collision layers as the visible weapon target.
            Physics.SyncTransforms();
            if (!physics.Cast(ray.origin, ray.direction, Mathf.Min(tool.Weapon.Distance, camera.View.farClipPlane - camera.View.nearClipPlane), 0f,
                tool.Weapon.Layers.value & camera.View.cullingMask, tool.Weapon.HitTriggers, out RaycastHit contact))
                return;
            Vector3 direction = contact.point - position;
            PlayerTools.PublishCursorUse(new PlayerToolHit(owner, tool, owner.UseActionId, true, contact, position,
                direction.sqrMagnitude > 0.000001f ? direction.normalized : rotation * Vector3.forward, 0f, true));
        }

        /// <summary>Shows impact feedback only after an interaction accepts this cursor contact.</summary>
        /// <param name="hit">Captured cursor shot confirmed by the interaction package.</param>
        internal void ConfirmCursorHit(PlayerToolHit hit)
        {
            effects?.Impacts.Emit(hit.Origin, hit.Contact.point, hit.Contact.normal, hit.Direction);
        }

        /// <summary>Advances projectiles even while another tool is selected.</summary>
        /// <param name="delta">Scaled physics step.</param>
        internal void Simulate(float delta)
        {
            effects?.Advance(delta);
            projectiles?.Advance(delta);
        }

        #endregion

        #endregion
    }
}
