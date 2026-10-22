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
        private PlayerToolProjectiles projectiles;
        private float nextUse;
        private bool pendingShot;
        private bool autoReloadPending;
        private bool preparingShot;
        private bool alignmentReady;

        #endregion

        #region Properties

        /// <summary>Whether a temporary use or reload sequence currently owns the tool.</summary>
        internal bool IsBusy => action.IsBusy || pendingShot || autoReloadPending;
        /// <summary>Whether the reload combo is playing.</summary>
        internal bool IsReloading => action.IsReloading;
        /// <summary>Whether another use is available under cooldown and pool limits.</summary>
        internal bool CanUse => !IsBusy && Time.time >= nextUse && (projectiles == null || projectiles.CanFire);

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
            this.camera = camera;
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
            return action.CollectTargets(moved, player) && (aim == null || aim.CollectTargets(moved, player));
        }

        /// <summary>Prewarms visuals only after the complete player configuration has passed binding.</summary>
        /// <param name="player">Player whose scene owns the pools.</param>
        internal void Prepare(Transform player)
        {
            if (tool.IsWeapon)
                projectiles = new PlayerToolProjectiles(tool, player, physics, camera, player.GetComponent<PlayerTools>());
        }

        /// <summary>Releases temporary poses and owned projectile pools at module shutdown.</summary>
        public void Dispose()
        {
            // Cancel queued work before releasing the visual state.
            pendingShot = autoReloadPending = preparingShot = alignmentReady = false;
            aim?.Restore();
            action.Stop();
            projectiles?.Dispose();
        }

        #endregion

        #region Commands and Playback

        /// <summary>Starts use keyframes and schedules the actual muzzle shot after camera following.</summary>
        internal void Use()
        {
            // Keep the tool reserved until its optional automatic combo has completed.
            nextUse = Time.time + tool.UseInterval;
            pendingShot = tool.IsWeapon;
            preparingShot = tool.IsWeapon && tool.Weapon.UsesShotAlignment;
            alignmentReady = false;
            autoReloadPending = tool.ReloadEnabled && tool.AutoReload;
            action.Begin(false);
        }

        /// <summary>Starts the enabled reload combo without requiring an ammunition implementation.</summary>
        /// <returns>True when the combo started.</returns>
        internal bool Reload()
        {
            if (IsBusy || !tool.ReloadEnabled)
                return false;
            action.Begin(true);
            return true;
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
            // Hold the authored first use pose until the arm has reached its prepared shooting position.
            if (preparingShot)
            {
                if (alignmentReady)
                {
                    preparingShot = false;
                    nextUse = Time.time + tool.UseInterval;
                }
                return;
            }
            action.Advance(delta);
            // Even an instant use must emit its shot before reload moves the muzzle.
            if (!autoReloadPending || action.IsBusy || pendingShot)
                return;
            autoReloadPending = false;
            action.Begin(true);
        }

        /// <summary>Samples aiming and emits a pending shot from this frame's final muzzle pose.</summary>
        /// <param name="delta">Scaled frame duration.</param>
        internal void Present(float delta)
        {
            alignmentReady = aim == null || aim.Sample(delta, pendingShot && tool.Weapon.UsesShotAlignment ? projectiles : null);
            if (pendingShot && tool.Weapon.UsesShotAlignment && !alignmentReady)
                preparingShot = true;
            if (preparingShot)
                return;
            if (!pendingShot)
                return;
            pendingShot = false;
            if (aim != null && aim.TryGetShot(out Vector3 position, out Quaternion rotation))
                projectiles.Fire(position, rotation);
            aim?.FinishShotAlignment();
        }

        /// <summary>Advances projectiles even while another tool is selected.</summary>
        /// <param name="delta">Scaled physics step.</param>
        internal void Simulate(float delta)
        {
            projectiles?.Advance(delta);
        }

        #endregion

        #endregion
    }
}
