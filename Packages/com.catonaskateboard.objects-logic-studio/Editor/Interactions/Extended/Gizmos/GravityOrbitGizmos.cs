using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws the same orbit center and up axis in placement previews and Scene view.</summary>
    internal static class GravityOrbitGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Marks the center, orbit plane and positive axis in the current handle space.</summary>
        /// <param name="position">Center position.</param>
        /// <param name="rotation">Rotation whose positive Y axis defines the orbit.</param>
        /// <param name="selected">Whether this center owns the active edit handles.</param>
        internal static void Draw(Vector3 position, Quaternion rotation, bool selected)
        {
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.75f, selected ? 0.9f : 0.45f));
            // Screen-relative size keeps nearby centers legible without implying an orbit radius.
            Handles.DrawWireDisc(position, rotation * Vector3.up, HandleUtility.GetHandleSize(position) * 0.14f);
            StudioOutletHandles.Marker(position, rotation * Quaternion.Euler(-90f, 0f, 0f), selected);
        }

        #endregion

        #endregion
    }
}

