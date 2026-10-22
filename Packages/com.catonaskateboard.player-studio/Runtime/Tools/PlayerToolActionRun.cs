using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Plays one use animation or a reload combo, restoring temporary poses after completion.</summary>
    internal sealed class PlayerToolActionRun
    {
        #region State

        private readonly PlayerToolAnimationRun use;
        private readonly PlayerToolAnimationRun[] reload;
        private int step = -1;
        private float elapsed;
        private bool reloading;

        #endregion

        #region Properties

        /// <summary>Whether use or reload tracks currently own the visual transforms.</summary>
        internal bool IsBusy => step >= 0;
        /// <summary>Whether the active playback is the reload combo.</summary>
        internal bool IsReloading => IsBusy && reloading;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Binds every enabled action track once for this player.</summary>
        /// <param name="tool">Tool supplying use and reload presets.</param>
        /// <param name="root">Hierarchy root selected by Tools.</param>
        internal PlayerToolActionRun(PlayerTool tool, Transform root)
        {
            // Disabled reload combos do not allocate track bindings.
            use = new PlayerToolAnimationRun(tool.UseAnimation, root);
            reload = tool.ReloadEnabled ? new PlayerToolAnimationRun[tool.ReloadCombo.Length] : Array.Empty<PlayerToolAnimationRun>();
            for (int index = 0; index < reload.Length; index++)
                reload[index] = new PlayerToolAnimationRun(tool.ReloadCombo[index], root);
        }

        /// <summary>Collects every transform that must return to its original pose when Tools stops.</summary>
        /// <param name="moved">Unique moved transforms shared with the switch and arm modules.</param>
        /// <param name="player">Protected player root.</param>
        /// <returns>True when all action targets exist and are visual children.</returns>
        internal bool CollectTargets(HashSet<Transform> moved, Transform player)
        {
            if (!use.CollectTargets(moved, player))
                return false;
            // One missing combo child rejects the binding before anything moves.
            foreach (PlayerToolAnimationRun animation in reload)
                if (!animation.CollectTargets(moved, player))
                    return false;
            return true;
        }

        #endregion

        #region Playback

        /// <summary>Starts temporary action tracks after idle and aim offsets have been removed.</summary>
        /// <param name="reloadRequested">True for the ordered reload combo, false for use.</param>
        internal void Begin(bool reloadRequested)
        {
            reloading = reloadRequested;
            step = 0;
            elapsed = 0f;
            Current().Begin();
            Advance(0f);
        }

        /// <summary>Samples actions while carrying leftover time into the next reload step.</summary>
        /// <param name="delta">Scaled seconds available this frame.</param>
        internal void Advance(float delta)
        {
            // Each iteration completes a distinct step; no unbounded zero-duration loop is possible.
            while (IsBusy)
            {
                PlayerToolAnimationRun animation = Current();
                float consumed = Mathf.Min(delta, Mathf.Max(0f, animation.Duration - elapsed));
                elapsed += consumed;
                delta -= consumed;
                animation.Sample(elapsed);
                if (elapsed < animation.Duration)
                    return;
                if (!reloading || step + 1 == reload.Length)
                {
                    Stop();
                    return;
                }
                step++;
                elapsed = 0f;
                Current().Begin();
            }
        }

        /// <summary>Unwinds each completed combo snapshot in reverse order, including shared targets.</summary>
        internal void Stop()
        {
            // Reverse restoration returns overlapping tracks to the pose before the combo began.
            if (reloading)
                for (int index = step; index >= 0; index--)
                    reload[index].Restore();
            else
                use.Restore();
            step = -1;
        }

        /// <summary>Resolves the currently playing animation without allocating a sequence for use.</summary>
        /// <returns>The active use or reload step.</returns>
        private PlayerToolAnimationRun Current()
        {
            return reloading ? reload[step] : use;
        }

        #endregion

        #endregion
    }
}
