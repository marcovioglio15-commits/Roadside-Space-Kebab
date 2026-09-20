using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Evaluates draft warnings and footer availability only when workspace data changes.</summary>
    internal static class PlayerStudioStatus
    {
        #region Methods

        #region Presentation

        /// <summary>Updates the shared footer without serializing or validating during viewport repaints.</summary>
        /// <param name="state">Current module and scene drafts.</param>
        /// <param name="workspace">Constructed controls, or null before UI initialization.</param>
        /// <param name="blocked">Whether workspace recovery has an unresolved conflict.</param>
        /// <param name="operationWarning">Warning from the latest deliberate action.</param>
        /// <param name="previewWarning">Optional warning from cached preview geometry.</param>
        internal static void Update(PlayerStudioState state, PlayerStudioWorkspace workspace, bool blocked,
            string operationWarning, string previewWarning)
        {
            // Recovery and Play suspend writes, while panel visibility leaves draft ownership unchanged.
            if (workspace == null)
                return;
            bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            bool isAssigning = state.Selection.MasterSession.HasChanges;
            workspace.UpdateQuickPlay(state.Selection.Master != null && !blocked);
            bool isValid = TryGetPreviewSettings(state, out _, out string warning);
            if (isValid && (state.Transform.HasChanges && !state.Transform.TryValidateNumbers(out warning)
                || state.Locomotion.HasChanges && !state.Locomotion.TryValidate(out warning)
                || state.Input.HasChanges && !state.Input.TryValidate(out warning)
                || state.Camera.HasChanges && !state.Camera.TryValidate(out warning)
                || state.Visual.HasChanges && !state.Visual.Draft.TryGetSettings(out _, out warning)))
                isValid = false;
            string targetWarning = "The master is unavailable. Discard before choosing another source.";
            bool hasTarget = isAssigning
                ? state.Selection.Master != null && state.Selection.MasterSession.Source == state.Selection.Master
                : state.Selection.TryValidateTarget(state.Body, out targetWarning);

            // Keep a single actionable warning; Apply performs the complete cross-module preflight.
            if (isPlaying)
                warning = "Preset editing is paused during Play. The draft is retained.";
            else if (operationWarning.Length > 0)
                warning = operationWarning;
            else if (!hasTarget)
                warning = state.Body.Source != null || state.Selection.HasChanges(state.Body) ? targetWarning : string.Empty;
            if (warning.Length == 0)
                warning = previewWarning;
            workspace.UpdateActions(!blocked && !isPlaying && state.HasChanges && isValid && hasTarget,
                !blocked && !isPlaying && (state.Body.Source != null || state.Selection.Master != null || state.HasChanges),
                warning);
        }

        #endregion

        #region Preview

        /// <summary>Resolves the proposed body for framing and gizmos without writing to its asset.</summary>
        /// <param name="state">Workspace with an independent slot and dimensions draft.</param>
        /// <param name="settings">Receives the proposed collision dimensions.</param>
        /// <param name="warning">Receives a missing or invalid Body warning.</param>
        /// <returns>True when the proposed Body can be represented.</returns>
        internal static bool TryGetPreviewSettings(PlayerStudioState state, out PlayerBodySettings settings, out string warning)
        {
            // A replaced slot uses its selected asset; otherwise the retained dimension draft takes precedence.
            return state.Selection.MasterSession.HasChanges && state.Selection.MasterSession.BodyPreset != state.Body.Source
                ? state.Selection.MasterSession.TryGetSettings(out settings, out warning)
                : state.Body.TryGetSettings(out settings, out warning);
        }

        #endregion

        #endregion
    }
}
