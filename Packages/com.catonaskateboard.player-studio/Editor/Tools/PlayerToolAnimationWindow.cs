using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits one keyframe preset independently of its referring tool or player.</summary>
    internal sealed class PlayerToolAnimationWindow : PlayerToolAssetWindow
    {
        #region Fields

        private static readonly string[] pages = { "Animation" };

        #endregion

        #region Properties

        /// <summary>Restricts selection to reusable transform animations.</summary>
        protected override Type PresetType => typeof(PlayerToolAnimationPreset);
        /// <summary>Names the preset's keyframe page.</summary>
        protected override string[] Tabs => pages;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens this animation's own proposal with the correct hierarchy for its tracks.</summary>
        /// <param name="source">Persistent animation asset.</param>
        /// <param name="root">Tools root, or selected arm root for idle clips.</param>
        internal static void Open(PlayerToolAnimationPreset source, Transform root)
        {
            // Idle callers supply the arm; action callers supply the Tools hierarchy root.
            ShowAsset<PlayerToolAnimationWindow>(source, root, false, "Tool Animation");
        }

        #endregion

        #region Validation

        /// <summary>Checks authored timings and poses without changing pending values.</summary>
        /// <param name="messages">Receives the first invalid animation setting.</param>
        protected override void Validate(List<string> messages)
        {
            // Invalid key times remain pending for correction or Discard.
            if (!((PlayerToolAnimationPreset)Draft).TryValidate(out string warning))
                messages.Add(warning);
        }

        #endregion

        #endregion
    }
}
