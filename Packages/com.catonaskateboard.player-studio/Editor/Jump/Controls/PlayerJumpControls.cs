using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares conditional jump controls between the asset Inspector and the session draft.</summary>
    internal static class PlayerJumpControls
    {
        #region Labels

        private static readonly GUIContent enabledLabel = new GUIContent("Use Jump", "Enable a fixed-height jump with an assigned Button action. Requires Use Gravity.");
        private static readonly GUIContent heightLabel = new GUIContent("Jump Height (m)", "Requested height above takeoff in free space; collisions may stop the ascent earlier.");
        private static readonly GUIContent bufferLabel = new GUIContent("Input Buffer (s)", "How long a press can wait for landing. Zero accepts only the current motor tick.");
        private static readonly GUIContent coyoteLabel = new GUIContent("Coyote Time (s)", "Grace after walking off support. Zero requires contact; a jump spends this opportunity.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Shows useful jump options and retains a visible way to turn off an incompatible jump.</summary>
        /// <param name="gravityEnabled">Whether the associated gravity is active.</param>
        /// <param name="enabled">Jump toggle, updated by the control.</param>
        /// <param name="height">Proposed height in metres.</param>
        /// <param name="bufferTime">Proposed press retention time.</param>
        /// <param name="coyoteTime">Proposed support grace time.</param>
        public static void Draw(bool gravityEnabled, ref bool enabled, ref float height, ref float bufferTime, ref float coyoteTime)
        {
            // Keep an already enabled toggle visible so a gravity mismatch can be corrected explicitly.
            if (!gravityEnabled && !enabled)
                return;
            enabled = EditorGUILayout.Toggle(enabledLabel, enabled);
            if (!enabled || !gravityEnabled)
                return;

            height = EditorGUILayout.FloatField(heightLabel, height);
            bufferTime = EditorGUILayout.FloatField(bufferLabel, bufferTime);
            coyoteTime = EditorGUILayout.FloatField(coyoteLabel, coyoteTime);
        }

        #endregion

        #endregion
    }
}
