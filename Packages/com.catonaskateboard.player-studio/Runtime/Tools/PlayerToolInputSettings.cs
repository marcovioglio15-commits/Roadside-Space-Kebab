using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Chooses one cycling command or a dedicated selection command per tool.</summary>
    public enum PlayerToolInputMode { SharedCycle, Independent }

    /// <summary>Maps a tool identity to one imported Button action.</summary>
    [Serializable]
    public sealed class PlayerToolInputBinding
    {
        #region Fields

        [Header("Tool Selection")]
        [Tooltip("Tool selected when this action performs.")]
        public PlayerTool Tool;
        [Tooltip("Button action resolved by ID in this player's own PlayerInput asset.")]
        public InputActionReference Action;

        #endregion
    }

    /// <summary>Separates tool selection, activation and optional reload commands in the player's private action asset.</summary>
    [Serializable]
    public sealed class PlayerToolInputSettings
    {
        #region Fields

        [Header("Tool Selection")]
        [Tooltip("Shared Cycle selects the next tool. Independent selects a specific tool using its own action.")]
        [InspectorName("Selection Mode")]
        public PlayerToolInputMode Mode;
        [Tooltip("Optional Button action that cycles through the configured tools.")]
        [InspectorName("Cycle Tool")]
        public InputActionReference UseTool;
        [Tooltip("Independent tool selection actions. Both tool identities and actions must be unique.")]
        public PlayerToolInputBinding[] Bindings = Array.Empty<PlayerToolInputBinding>();
        [Header("Tool Actions")]
        [Tooltip("Optional Button action that uses the active tool or fires its weapon. Independent from selection commands.")]
        [InspectorName("Use Tool")]
        public InputActionReference Activate;
        [Tooltip("Optional Button action that plays the active tool's enabled reload combo.")]
        public InputActionReference Reload;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks button roles and duplicate selection commands without enabling shared actions.</summary>
        /// <param name="warning">Receives the first incompatible mapping.</param>
        /// <param name="switching">False when the player's single-tool mode disables selection commands.</param>
        /// <returns>True when the selected input mode is valid.</returns>
        public bool TryValidate(out string warning, bool switching = true)
        {
            // Unassigned shared input allows scripted tool selection.
            warning = "Assign distinct Button actions to use, reload and tool selection roles.";
            if (Activate != null && !IsButton(Activate) || Reload != null && !IsButton(Reload))
                return false;
            HashSet<Guid> actions = new HashSet<Guid>();
            if (Activate != null)
                actions.Add(Activate.action.id);
            if (Reload != null && !actions.Add(Reload.action.id))
                return false;
            if (!switching)
            {
                warning = string.Empty;
                return true;
            }
            if (Mode is not (PlayerToolInputMode.SharedCycle or PlayerToolInputMode.Independent))
                return false;
            if (Mode == PlayerToolInputMode.SharedCycle)
            {
                if (UseTool != null && (!IsButton(UseTool) || !actions.Add(UseTool.action.id)))
                    return false;
            }
            else
            {
                if (Bindings == null)
                    return false;
                HashSet<PlayerTool> tools = new HashSet<PlayerTool>();
                foreach (PlayerToolInputBinding binding in Bindings)
                    if (binding == null || binding.Tool == null || !tools.Add(binding.Tool) || !IsButton(binding.Action)
                        || !actions.Add(binding.Action.action.id))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks the imported action role independently of its current enabled state.</summary>
        /// <param name="reference">Selected imported action.</param>
        /// <returns>True for a valid Button action.</returns>
        private static bool IsButton(InputActionReference reference)
        {
            // The runtime resolves another instance through PlayerInput.
            return reference != null && reference.action != null && reference.action.type == InputActionType.Button;
        }

        #endregion

        #endregion
    }
}
