using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares the conditional gravity fields between the preset Inspector and session view.</summary>
    internal static class PlayerGravityControls
    {
        #region Labels

        private static readonly GUIContent enabledLabel = new GUIContent("Use Gravity", "Request world-down motion and refresh contact below, even without directional input.");
        private static readonly GUIContent accelerationLabel = new GUIContent("Gravity (m/s²)", "Positive downward acceleration while falling.");
        private static readonly GUIContent terminalLabel = new GUIContent("Terminal Speed (m/s)", "Maximum downward speed; must be positive.");
        private static readonly GUIContent groundLabel = new GUIContent("Ground Speed (m/s)", "Downward request after contact. Keeps checking the floor through Move; does not teleport onto slopes.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Draws active options only, leaving disabled numbers intact for later use.</summary>
        /// <param name="enabled">Gravity toggle, updated from the current GUI event.</param>
        /// <param name="acceleration">Downward acceleration, edited only when enabled.</param>
        /// <param name="terminalSpeed">Fall speed limit, edited only when enabled.</param>
        /// <param name="groundSpeed">Contact refresh speed, edited only when enabled.</param>
        public static void Draw(ref bool enabled, ref float acceleration, ref float terminalSpeed, ref float groundSpeed)
        {
            // One toggle introduces the section without repeating its title above a single foldout.
            enabled = EditorGUILayout.Toggle(enabledLabel, enabled);
            if (!enabled)
                return;

            acceleration = EditorGUILayout.FloatField(accelerationLabel, acceleration);
            terminalSpeed = EditorGUILayout.FloatField(terminalLabel, terminalSpeed);
            groundSpeed = EditorGUILayout.FloatField(groundLabel, groundSpeed);
        }

        #endregion

        #endregion
    }
}
