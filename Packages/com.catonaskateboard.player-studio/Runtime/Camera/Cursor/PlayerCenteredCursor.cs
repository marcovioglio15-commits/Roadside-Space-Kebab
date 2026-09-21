using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Draws a centered graphic for a locked pointer without creating UI objects or textures.</summary>
    internal static class PlayerCenteredCursor
    {
        #region Methods

        #region Rendering

        /// <summary>Repaints only the active camera's onscreen viewport; capture state is owned by its rig.</summary>
        /// <param name="view">Existing camera that owns the pointer.</param>
        /// <param name="texture">Optional authored graphic; null uses a built-in contrasting crosshair.</param>
        /// <param name="scale">Validated uniform multiplier for the cursor's original pixel dimensions.</param>
        internal static void Draw(Camera view, Texture2D texture, float scale)
        {
            // IMGUI renders directly; no Canvas, GameObject, material or texture is instantiated.
            if (Event.current.type != EventType.Repaint || view.targetTexture != null || view.targetDisplay != 0)
                return;
            Vector2 center = view.pixelRect.center;
            center.y = Screen.height - center.y;
            Color previous = GUI.color;
            GUI.color = Color.white;
            if (texture != null)
                DrawRect(center, new Vector2(texture.width, texture.height), scale, texture);
            else
            {
                // A dark border keeps the marker readable over both sky and shaded geometry.
                GUI.color = Color.black;
                DrawRect(center, new Vector2(14f, 4f), scale, Texture2D.whiteTexture);
                DrawRect(center, new Vector2(4f, 14f), scale, Texture2D.whiteTexture);
                GUI.color = Color.white;
                DrawRect(center, new Vector2(12f, 2f), scale, Texture2D.whiteTexture);
                DrawRect(center, new Vector2(2f, 12f), scale, Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        /// <summary>Draws a centered texture rectangle using the same scale for its size and offsets.</summary>
        /// <param name="center">Camera viewport center in GUI pixels.</param>
        /// <param name="size">Original rectangle dimensions.</param>
        /// <param name="scale">Validated positive size multiplier.</param>
        /// <param name="texture">Existing texture to draw.</param>
        private static void DrawRect(Vector2 center, Vector2 size, float scale, Texture2D texture)
        {
            // Scaling the border and inner bars together preserves the procedural crosshair shape.
            GUI.DrawTexture(new Rect(center - size * (scale * 0.5f), size * scale), texture);
        }

        #endregion

        #endregion
    }
}
