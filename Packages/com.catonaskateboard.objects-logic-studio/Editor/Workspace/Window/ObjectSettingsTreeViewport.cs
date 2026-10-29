using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps tree pan and zoom independent of window coordinates and serialized settings.</summary>
    internal sealed class ObjectSettingsTreeViewport
    {
        #region State

        internal const float MinimumZoom = 0.01f;
        internal const float MaximumZoom = 2f;
        internal Vector2 Offset;
        internal float Zoom { get; private set; } = 1f;

        #endregion

        #region Methods

        #region Coordinates

        /// <summary>Converts a pointer position to the corresponding tree position.</summary>
        /// <param name="point">Point inside the canvas viewport.</param>
        /// <returns>Unscaled tree coordinates.</returns>
        internal Vector2 ToTree(Vector2 point)
        {
            return (point - Offset) / Zoom;
        }

        /// <summary>Projects a tree block into the clipped drawing viewport.</summary>
        /// <param name="rect">Unscaled tree rectangle.</param>
        /// <returns>Scaled viewport rectangle.</returns>
        internal Rect ToScreen(Rect rect)
        {
            return new Rect(rect.position * Zoom + Offset, rect.size * Zoom);
        }

        /// <summary>Changes zoom around the pointer so the option under it stays in place.</summary>
        /// <param name="point">Zoom anchor inside the viewport.</param>
        /// <param name="zoom">Requested scale within the canvas zoom range.</param>
        internal void ZoomAt(Vector2 point, float zoom)
        {
            Vector2 anchor = ToTree(point);
            Zoom = Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);
            Offset = point - anchor * Zoom;
        }

        /// <summary>Moves the view by a mouse drag without touching any setting.</summary>
        /// <param name="delta">Pointer movement in viewport pixels.</param>
        internal void Pan(Vector2 delta)
        {
            Offset += delta;
        }

        /// <summary>Starts at a readable scale with the interaction block near the left edge.</summary>
        /// <param name="root">Root interaction block.</param>
        /// <param name="size">Available viewport size.</param>
        internal void Focus(Rect root, Vector2 size)
        {
            Zoom = 1f;
            Offset = new Vector2(24f - root.xMin, size.y * 0.5f - root.center.y);
        }

        /// <summary>Frames the visible hierarchy while preserving enough margin for the outer blocks.</summary>
        /// <param name="treeSize">Bounds of all visible tree blocks.</param>
        /// <param name="size">Available viewport size.</param>
        internal void Fit(Vector2 treeSize, Vector2 size)
        {
            Zoom = Mathf.Clamp(Mathf.Min((size.x - 32f) / Mathf.Max(1f, treeSize.x), (size.y - 32f) / Mathf.Max(1f, treeSize.y)), MinimumZoom, MaximumZoom);
            Offset = (size - treeSize * Zoom) * 0.5f;
        }

        #endregion

        #endregion
    }
}
