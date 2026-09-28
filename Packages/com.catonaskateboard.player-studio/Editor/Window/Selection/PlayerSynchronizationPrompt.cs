using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Offers synchronization directions after the workspace has recovered its player and drafts.</summary>
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

        /// <summary>Stages saved preset values through the explicit Player menu action.</summary>
        internal void StagePresets()
        {
            PlayerSceneSynchronization.TryStage(state(), owner, out string warning);
            report(warning);
        }



        /// <summary>Offers both synchronization directions without changing anything when cancelled.</summary>
        private void Check()
        {
            scheduled = false;
            if (owner == null || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            bool differs = PlayerSceneSynchronization.TryCompare(state(), out string differences, out string warning);
            report(warning);
            if (!differs)
                return;
            if (EditorUtility.DisplayDialog("Synchronize Player", "The scene differs from the saved presets: " + differences
                + ". Apply Saved Presets updates the configured components and the player prefab.", "Apply Saved Presets", "Cancel"))
            {
                if (PlayerSceneSynchronization.TryStage(state(), owner, out warning))
                    apply();
                else
                    report(warning);
            }
        }

        #endregion
    }
}
