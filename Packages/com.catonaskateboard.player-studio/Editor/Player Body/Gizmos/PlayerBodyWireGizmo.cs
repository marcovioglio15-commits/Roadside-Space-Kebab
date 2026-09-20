using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares the capsule outline between applied host gizmos and the scene's draft overlay.</summary>
    internal static class PlayerBodyWireGizmo
    {
        #region Methods

        #region Drawing

        /// <summary>Draws a feet-anchored Body while restoring the caller's Handles state.</summary>
        /// <param name="matrix">Applied or proposed body pose; draft scaling can be shown without moving an object.</param>
        /// <param name="settings">Validated capsule dimensions.</param>
        /// <param name="color">Color distinguishing applied and proposed geometry.</param>
        public static void Draw(Matrix4x4 matrix, PlayerBodySettings settings, Color color)
        {
            // The same geometry serves scene gizmos and previews; only the color changes.
            using (new Handles.DrawingScope(color, matrix))
                DrawCapsule(settings);
        }

        /// <summary>
        /// Outlines a feet-anchored capsule using two rings and two perpendicular profiles.
        /// </summary>
        /// <param name="settings">Validated capsule dimensions to draw.</param>
        private static void DrawCapsule(PlayerBodySettings settings)
        {
            // Place the hemisphere centres so the total height includes the rounded ends.
            Vector3 bottom = Vector3.up * settings.Radius;
            Vector3 top = Vector3.up * (settings.Height - settings.Radius);

            // Rings show the transition between the straight sides and rounded caps.
            Handles.DrawWireDisc(bottom, Vector3.up, settings.Radius);
            Handles.DrawWireDisc(top, Vector3.up, settings.Radius);

            // Two profiles describe the full volume without a dense wire mesh.
            for (int axis = 0; axis < 2; axis++)
            {
                // Build one side profile from its horizontal direction and arc normal.
                Vector3 direction = axis == 0 ? Vector3.right : Vector3.forward;
                Vector3 normal = Vector3.Cross(direction, Vector3.up);
                Vector3 offset = direction * settings.Radius;

                // Join the upper and lower arcs with two straight sides.
                Handles.DrawWireArc(top, normal, direction, 180f, settings.Radius);
                Handles.DrawWireArc(bottom, normal, direction, -180f, settings.Radius);
                Handles.DrawLine(bottom + offset, top + offset);
                Handles.DrawLine(bottom - offset, top - offset);
            }
        }

        #endregion

        #endregion
    }
}
