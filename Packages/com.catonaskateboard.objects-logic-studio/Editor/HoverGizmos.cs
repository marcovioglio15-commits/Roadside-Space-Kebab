using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws selected-object detection geometry without creating runtime debug objects.</summary>
    internal static class HoverGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Shows the anchor, range and final world offset only for a selected hover component.</summary>
        /// <param name="hover">Selected interaction whose settings are being inspected.</param>
        /// <param name="type">Selection flags supplied by Unity.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ObjectHover hover, GizmoType type)
        {
            // Invalid numbers must not reach Handles or change the authored values.
            if (!hover.DrawGizmos || hover.Settings == null || !hover.Settings.TryValidate(out _))
                return;
            Vector3 anchor = hover.WorldAnchor;
            float handleSize = HandleUtility.GetHandleSize(anchor) * 0.06f;
            Color color = hover.IsHovered ? new Color(0.3f, 1f, 0.55f) : new Color(0.15f, 0.75f, 1f);
            using (new Handles.DrawingScope(color))
            {
                // Three thin rings describe range without hiding the prefab mesh.
                Handles.DrawWireDisc(anchor, Vector3.up, hover.Settings.PlayerDistance);
                Handles.DrawWireDisc(anchor, Vector3.right, hover.Settings.PlayerDistance);
                Handles.DrawWireDisc(anchor, Vector3.forward, hover.Settings.PlayerDistance);
                Handles.SphereHandleCap(0, anchor, Quaternion.identity, handleSize, EventType.Repaint);
                Handles.DrawDottedLine(anchor, anchor + hover.Settings.WorldOffset, 4f);
                Handles.Label(anchor + Vector3.up * handleSize, new GUIContent(hover.InteractionName + " · "
                    + hover.Settings.PlayerDistance.ToString("0.##") + " m", "Player-to-anchor range; screen offsets are applied after projection."));
            }
        }

        #endregion

        #endregion
    }
}
