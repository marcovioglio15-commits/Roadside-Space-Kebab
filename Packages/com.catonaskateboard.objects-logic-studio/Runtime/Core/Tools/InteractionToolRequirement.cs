using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Limits an interaction to no tool, one tool, a selection, or unrestricted use.</summary>
    public enum InteractionToolMode { All, None, Specific, Selection }

    /// <summary>Checks the observer player's settled tool identity without scene searches or name matching.</summary>
    [Serializable]
    public sealed class InteractionToolRequirement
    {
        #region Fields

        [Header("Player Tool")]
        [Tooltip("All leaves tool selection unrestricted. None requires no active tool. Specific and Selection accept the chosen tool assets.")]
        public InteractionToolMode Mode;
        [Tooltip("Tool that must be active for this interaction.")]
        public PlayerTool Tool;
        [Tooltip("Any one of these tools may activate this interaction.")]
        public PlayerTool[] Tools = Array.Empty<PlayerTool>();

        #endregion

        #region Methods

        #region Availability

        /// <summary>Tests a settled player selection; a switch cannot temporarily satisfy the None mode.</summary>
        /// <param name="player">Observed player's optional Tools component.</param>
        /// <param name="hasPlayer">Whether an active observer has resolved a player.</param>
        /// <returns>True when this tool restriction permits activation.</returns>
        public bool Allows(PlayerTools player, bool hasPlayer)
        {
            // The default preserves existing interactions, including autonomous effects without an observer.
            if (Mode == InteractionToolMode.All)
                return true;
            if (!hasPlayer || player != null && player.isActiveAndEnabled && (!player.IsReady || player.IsSwitching))
                return false;
            PlayerTool active = player != null && player.isActiveAndEnabled ? player.ActiveTool : null;
            switch (Mode)
            {
                case InteractionToolMode.None:
                    return active == null;
                case InteractionToolMode.Specific:
                    return Tool != null && active == Tool;
                case InteractionToolMode.Selection:
                    if (active != null && Tools != null)
                        foreach (PlayerTool tool in Tools)
                            if (tool == active)
                                return true;
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        #region Validation

        /// <summary>Reports incomplete selected-tool restrictions without replacing saved values.</summary>
        /// <param name="warning">Receives a missing or duplicate selected tool.</param>
        /// <returns>True when the selected mode has all required identities.</returns>
        public bool TryValidate(out string warning)
        {
            // Only the chosen mode contributes validation requirements.
            warning = "Assign a tool for Specific, or a non-empty selection of unique tool assets.";
            switch (Mode)
            {
                case InteractionToolMode.All:
                case InteractionToolMode.None:
                    break;
                case InteractionToolMode.Specific:
                    if (Tool == null)
                        return false;
                    break;
                case InteractionToolMode.Selection:
                    if (Tools == null || Tools.Length == 0)
                        return false;
                    for (int index = 0; index < Tools.Length; index++)
                    {
                        if (Tools[index] == null)
                            return false;
                        for (int previous = 0; previous < index; previous++)
                            if (Tools[index] == Tools[previous])
                                return false;
                    }
                    break;
                default:
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
