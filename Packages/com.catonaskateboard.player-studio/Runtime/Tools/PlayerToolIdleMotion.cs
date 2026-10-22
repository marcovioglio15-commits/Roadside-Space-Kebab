using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Coordinates reversible arm idle and parent rotation follow around authored action tracks.</summary>
    internal sealed class PlayerToolIdleMotion
    {
        #region State

        private readonly Transform arm;
        private readonly PlayerToolAnimationRun idle;
        private readonly PlayerToolArmRotation rotation;
        private float elapsed;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Binds idle tracks relative to the explicitly selected arm.</summary>
        /// <param name="settings">Enabled arm configuration.</param>
        /// <param name="root">Tools hierarchy root.</param>
        internal PlayerToolIdleMotion(PlayerToolArmSettings settings, Transform root)
        {
            arm = PlayerHierarchy.Resolve(root, settings.Path);
            idle = new PlayerToolAnimationRun(settings.IdleEnabled ? settings.Idle : null, arm);
            rotation = settings.SmoothRotation ? new PlayerToolArmRotation(arm, settings) : null;
        }

        /// <summary>Rejects a missing arm or an attempt to animate the player root.</summary>
        /// <param name="moved">Unique transform snapshot collection.</param>
        /// <param name="player">Protected player root.</param>
        /// <param name="view">Existing camera excluded from the smoothed arm hierarchy.</param>
        /// <returns>True when the arm and all enabled idle targets exist.</returns>
        internal bool CollectTargets(HashSet<Transform> moved, Transform player, Transform view)
        {
            // Camera descendants would feed the arm's own rotation back into its parent target.
            if (arm == null || arm == player || rotation != null && view != null && view.IsChildOf(arm)
                || !idle.CollectTargets(moved, player))
                return false;
            if (rotation != null)
                moved.Add(arm);
            return true;
        }

        #endregion

        #region Playback

        /// <summary>Removes the previous idle sample before actions capture or update their poses.</summary>
        internal void Restore()
        {
            rotation?.Restore();
            idle.Restore();
        }

        /// <summary>Applies parent rotation follow after idle or action poses, including during recharge.</summary>
        /// <param name="delta">Scaled presentation duration.</param>
        internal void SampleRotation(float delta)
        {
            // Rotation follow remains active while action tracks suspend idle playback.
            rotation?.Sample(delta);
        }

        /// <summary>Loops authored arm tracks without accumulating offsets on the live transform.</summary>
        /// <param name="delta">Scaled seconds spent idle.</param>
        internal void Sample(float delta)
        {
            if (idle.Duration <= 0f)
                return;
            // Each frame captures the clean pose, so moving the player's parent remains natural.
            elapsed = Mathf.Repeat(elapsed + delta, idle.Duration);
            idle.Begin();
            idle.Sample(elapsed);
        }

        #endregion

        #endregion
    }
}
