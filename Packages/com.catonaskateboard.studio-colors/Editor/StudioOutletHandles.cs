using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Shares outlet markers, transform handles and toolbar symbols across emitter previews.</summary>
    public static class StudioOutletHandles
    {
        #region Methods
        #region Drawing

        /// <summary>Draws the same native position and rotation symbols in each outlet preview.</summary>
        /// <param name="mode">Retained tool index; zero moves and one rotates.</param>
        public static void Toolbar(ref int mode)
        {
            if (GUILayout.Toggle(mode == 0, new GUIContent(EditorGUIUtility.IconContent("MoveTool").image, "Move outlet (W)"), EditorStyles.toolbarButton, GUILayout.Width(36f)))
                mode = 0;
            if (GUILayout.Toggle(mode == 1, new GUIContent(EditorGUIUtility.IconContent("RotateTool").image, "Rotate outlet (E)"), EditorStyles.toolbarButton, GUILayout.Width(36f)))
                mode = 1;
        }

        /// <summary>Draws a compact positive-Z arrow using the same colour and screen-relative size.</summary>
        /// <param name="position">Outlet position in the current drawing frame.</param>
        /// <param name="rotation">Outlet orientation in the current drawing frame.</param>
        /// <param name="selected">Whether this marker owns the active transform handles.</param>
        public static void Marker(Vector3 position, Quaternion rotation, bool selected = true)
        {
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.75f, selected ? 1f : 0.4f));
            Handles.ArrowHandleCap(0, position, rotation, HandleUtility.GetHandleSize(position) * 0.35f, EventType.Repaint);
        }

        /// <summary>Edits a pose using the same move and rotate handles while allowing view navigation.</summary>
        /// <param name="position">Receives the edited point in the current drawing frame.</param>
        /// <param name="rotation">Receives the edited orientation in the current drawing frame.</param>
        /// <param name="mode">Zero moves; one rotates; other view tools leave the pose unchanged.</param>
        /// <param name="navigating">Whether the viewport currently owns a camera gesture.</param>
        /// <returns>True only when a transform handle changed the pose.</returns>
        public static bool Edit(ref Vector3 position, ref Quaternion rotation, int mode, bool navigating)
        {
            if (navigating || Event.current.alt || mode is not (0 or 1))
                return false;
            EditorGUI.BeginChangeCheck();
            if (mode == 0)
                position = Handles.PositionHandle(position, rotation);
            else
                rotation = Handles.RotationHandle(rotation, position);
            return EditorGUI.EndChangeCheck();
        }

        #endregion
        #endregion
    }
}
