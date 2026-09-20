using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Prepares configuration commands without applying assets or hierarchy changes ahead of confirmation.</summary>
    internal static class PlayerStudioCommands
    {
        #region Methods

        /// <summary>Loads default slot references into the current master's draft and requests complete player synchronization.</summary>
        /// <param name="state">Current workspace, including any selected scene player.</param>
        /// <param name="owner">Window recorded for Undo.</param>
        /// <param name="warning">Receives an unavailable default or pending-work warning.</param>
        /// <returns>True when defaults are staged, or opened without a scene context.</returns>
        internal static bool TryLoadDefaults(PlayerStudioState state, Object owner, out string warning)
        {
            // Menu commands must not bypass the source and Play guards used by fields.
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode || state.HasChanges)
            {
                warning = "Apply or discard the current session in Edit mode before loading defaults.";
                return false;
            }
            PlayerMasterPreset defaults = PlayerDefaultAssets.Ensure();
            if (defaults == null)
            {
                warning = "Default assets are still being prepared. Retry after package import completes.";
                return false;
            }
            if (state.PreviewHost == null)
                return state.Selection.TrySelect(PlayerStudioSourceMode.Master, defaults, null, state.Body, out warning);

            // Keep this player's master identity: Apply saves these slots instead of switching the visible source.
            if (state.Selection.Master != state.PreviewHost.MasterPreset
                && !state.Selection.TrySelect(PlayerStudioSourceMode.Master, state.PreviewHost.MasterPreset, null, state.Body, out warning))
                return false;
            Undo.RecordObject(owner, "Load Player Defaults");
            state.Selection.MasterSession.SetDraft(defaults.BodyPreset, defaults.InputPreset, defaults.LocomotionPreset,
                defaults.VisualPreset, defaults.CameraPreset);
            state.VisualScene.Refresh(state.PreviewHost);
            state.VisualScene.SetDraft(true, state.VisualScene.Existing);
            state.VisualScene.RequestSynchronization();
            state.CameraScene.Refresh(state.PreviewHost);
            state.CameraScene.RequestSynchronization();
            return true;
        }

        #endregion
    }
}
