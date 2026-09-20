using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws Body dimensions and records changes on the owning window's draft.</summary>
    internal static class PlayerBodyDraftView
    {
        #region Labels

        private static readonly GUIContent radiusLabel = new GUIContent("Radius (m)", "Draft capsule radius in metres. Must be finite and greater than zero.");
        private static readonly GUIContent heightLabel = new GUIContent("Height (m)", "Draft total height including rounded ends. Must be at least twice the radius.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Records Undo before replacing the two raw draft dimensions, leaving the Body asset intact.</summary>
        /// <param name="session">Session containing the dimensions shown in these controls.</param>
        /// <param name="undoTarget">Window that serializes the session and owns its draft Undo history.</param>
        /// <returns>True when a dimension was edited during this GUI event.</returns>
        public static bool Draw(PlayerBodyEditSession session, Object undoTarget)
        {
            // Keep entered values intact, including invalid dimensions that need correction.
            EditorGUI.BeginChangeCheck();
            float radius = EditorGUILayout.FloatField(radiusLabel, session.Radius);
            float height = EditorGUILayout.FloatField(heightLabel, session.Height);
            if (!EditorGUI.EndChangeCheck())
                return false;

            // Undo belongs to the window; the preset is written only by Apply.
            Undo.RecordObject(undoTarget, "Edit Body Draft");
            session.SetDraft(radius, height);
            return true;
        }

        #endregion

        #endregion
    }
}
