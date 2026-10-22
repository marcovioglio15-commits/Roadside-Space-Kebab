using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits one tool through its own recoverable Apply/Discard transaction.</summary>
    internal sealed class PlayerToolWindow : PlayerToolAssetWindow
    {
        #region Fields

        private static readonly string[] pages = { "Tool" };

        #endregion

        #region Properties

        /// <summary>Restricts the asset selector to tool identities.</summary>
        protected override Type PresetType => typeof(PlayerTool);
        /// <summary>Names the tool's single settings page.</summary>
        protected override string[] Tabs => pages;
        /// <summary>Current detached tool used by the muzzle preview while this transaction remains open.</summary>
        internal PlayerTool EditingTool => Draft as PlayerTool;
        /// <summary>Persistent tool used if its separate muzzle preview outlives this window.</summary>
        internal PlayerTool AppliedTool => Source as PlayerTool;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Focuses this tool's window without replacing another tool's pending draft.</summary>
        /// <param name="source">Persistent tool to configure.</param>
        /// <param name="root">Current Tools hierarchy root.</param>
        /// <param name="allowSwitching">Whether switch clips apply to this player.</param>
        internal static void Open(PlayerTool source, Transform root, bool allowSwitching)
        {
            // Reopening a tool retains its existing proposal and hierarchy context.
            ShowAsset<PlayerToolWindow>(source, root, allowSwitching, "Player Tool");
        }

        #endregion

        #region Validation

        /// <summary>Rejects invalid enabled tool behavior before committing this window's draft.</summary>
        /// <param name="messages">Receives an actionable validation warning.</param>
        protected override void Validate(List<string> messages)
        {
            // Switch validation follows the player mode used to open this window.
            if (!((PlayerTool)Draft).TryValidate(Switching, out string warning))
                messages.Add(warning);
        }

        #endregion

        #endregion
    }
}
