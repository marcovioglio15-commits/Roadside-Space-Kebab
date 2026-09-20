using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Schedules one preset-to-scene comparison after the workspace has recovered its player and drafts.</summary>
    internal sealed class PlayerSynchronizationPrompt : IDisposable
    {
        #region State

        private readonly UnityEngine.Object owner;
        private readonly Func<PlayerStudioState> state;
        private readonly Action apply;
        private readonly Action<string> report;
        private bool scheduled;

        #endregion

        #region Methods

        /// <summary>Retains callbacks without capturing a stale workspace after Undo or Play recovery.</summary>
        /// <param name="owner">Window recorded when differences become a proposal.</param>
        /// <param name="state">Resolves the currently restored workspace.</param>
        /// <param name="apply">Shared confirmation action used only after the popup is accepted.</param>
        /// <param name="report">Displays validation issues in the ordinary window footer.</param>
        internal PlayerSynchronizationPrompt(UnityEngine.Object owner, Func<PlayerStudioState> state, Action apply, Action<string> report)
        {
            this.owner = owner;
            this.state = state;
            this.apply = apply;
            this.report = report;
        }

        /// <summary>Coalesces opening and selection events into one comparison after the current Editor event.</summary>
        internal void Schedule()
        {
            // No polling is needed; reopening or selecting a player supplies the next comparison boundary.
            if (scheduled)
                return;
            scheduled = true;
            EditorApplication.delayCall += Check;
        }

        /// <summary>Removes a queued comparison when the window closes or reloads.</summary>
        public void Dispose()
        {
            EditorApplication.delayCall -= Check;
            scheduled = false;
        }

        /// <summary>Offers confirmation while preserving a declined proposal for Apply, Discard and Undo.</summary>
        private void Check()
        {
            scheduled = false;
            if (owner == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            bool proposed = PlayerSceneSynchronization.TryStage(state(), owner, out string differences, out string warning);
            report(warning);
            if (!proposed || Application.isBatchMode)
                return;
            if (EditorUtility.DisplayDialog("Synchronize Player", "The scene differs from the saved presets: " + differences
                + ".\n\nReapply the preset values to the player and its prefab now? Keeping the proposal pending leaves the scene unchanged until Apply.",
                "Synchronize", "Keep Pending"))
                apply();
        }

        #endregion
    }
}
