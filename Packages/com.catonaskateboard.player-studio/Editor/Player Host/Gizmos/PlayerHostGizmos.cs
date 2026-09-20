using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>
    /// Draws the selected player's capsule without creating meshes or runtime objects.
    /// The Editor-only assembly keeps this drawing code out of a game build.
    /// </summary>
    internal static class PlayerHostGizmos
    {
        #region Drawing Style

        private static readonly Color bodyColor = new Color(0.2f, 0.85f, 1f, 1f);

        #endregion

        #region Methods

        #region Drawing

        /// <summary>
        /// Draws valid body dimensions only for a selected player with debugging enabled.
        /// </summary>
        /// <param name="host">Selected component whose body should be visualized.</param>
        /// <param name="gizmoType">Selection context supplied by Unity's gizmo callback.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawBody(PlayerHost host, GizmoType gizmoType)
        {
            // Player Studio owns its outline; ordinary Scene views retain this selection gizmo.
            if (SceneView.currentDrawingSceneView is PlayerStudioWindow || !host.DrawBodyGizmo
                || !host.TryGetBodySettings(out PlayerBodySettings settings, out _))
                return;

            // Draw in local metres and restore the previous Handles state afterwards.
            PlayerBodyWireGizmo.Draw(Matrix4x4.TRS(host.transform.position, host.transform.rotation, Vector3.one), settings, bodyColor);
        }
        #endregion

        #endregion
    }
}
