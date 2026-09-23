using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Coordinates durable workspace recovery while protecting snapshots with missing sources.</summary>
    internal sealed class PlayerWorkspaceRecovery
    {
        #region State

        private PlayerWorkspaceStore.Snapshot retained;
        private string warning = string.Empty;
        internal bool IsBlocked => warning.Length > 0;

        #endregion

        #region Methods

        /// <summary>Loads and resolves a saved workspace before clean sessions refresh their baselines.</summary>
        /// <param name="window">Preview whose native pose is restored.</param>
        /// <param name="state">Receives recovered data only when all identities can be resolved.</param>
        internal void Load(SceneView window, ref PlayerStudioState state)
        {
            // A failed restore retains the file untouched and blocks editing until an explicit decision.
            try
            {
                retained = PlayerWorkspaceStore.Load();
                if (retained == null)
                    return;
                PlayerStudioState restored = PlayerWorkspaceStore.Restore(retained, out warning);
                if (IsBlocked)
                    return;
                state = restored;
                window.position = retained.Position;
                window.LookAt(retained.Pivot, retained.Rotation, retained.Size, retained.Orthographic, true);
            }
            catch (Exception exception)
            {
                warning = "Workspace recovery failed: " + exception.Message;
            }
        }

        /// <summary>Restores splitter and scroll after the native workspace has been built.</summary>
        /// <param name="workspace">Newly constructed Editor panels.</param>
        internal void RestoreLayout(PlayerStudioWorkspace workspace)
        {
            // Failed recovery never applies a partial layout to an unrelated session.
            if (retained != null && !IsBlocked)
                workspace.Restore(retained);
        }

        /// <summary>Persists the session at close or a graceful Editor shutdown.</summary>
        /// <param name="state">Current proposals and their baselines.</param>
        /// <param name="window">Native preview state.</param>
        /// <param name="workspace">Optional live panels supplying splitter and scroll.</param>
        internal void Save(PlayerStudioState state, SceneView window, PlayerStudioWorkspace workspace)
        {
            // Play copies and unresolved snapshots must never replace the editable workspace.
            if (IsBlocked || EditorApplication.isPlaying)
                return;
            try
            {
                retained = PlayerWorkspaceStore.Capture(state, window);
                workspace?.Capture(retained);
                PlayerWorkspaceStore.Save(retained);
            }
            catch (Exception exception)
            {
                warning = "Workspace could not be saved: " + exception.Message;
                Debug.LogWarning(warning);
            }
        }

        /// <summary>Provides explicit retry or abandonment when saved references cannot be restored.</summary>
        /// <param name="window">Window receiving a successful recovery.</param>
        /// <param name="state">Session replaced after successful recovery or explicit abandonment.</param>
        /// <returns>True when ordinary controls must remain hidden.</returns>
        internal bool Draw(SceneView window, ref PlayerStudioState state)
        {
            // This warning is actionable: load the referenced scenes before retrying.
            if (!IsBlocked)
                return false;
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
            if (GUILayout.Button(new GUIContent("Retry Workspace Recovery", "Resolve the saved references after loading their scenes or restoring missing assets.")))
                Load(window, ref state);
            if (GUILayout.Button(new GUIContent("Discard Saved Workspace", "Abandon the saved proposal without modifying any preset or scene.")))
            {
                warning = string.Empty;
                retained = null;
                state = new PlayerStudioState();
                Save(state, window, null);
            }
            return true;
        }

        #endregion
    }
}
