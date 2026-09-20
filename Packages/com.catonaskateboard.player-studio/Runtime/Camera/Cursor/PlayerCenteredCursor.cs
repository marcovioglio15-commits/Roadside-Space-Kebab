using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Draws a centered graphic for a locked pointer without creating UI objects or textures.</summary>
    internal static class PlayerCenteredCursor
    {
        #region Methods

        /// <summary>Repaints only the active camera's onscreen viewport; capture state is owned by its rig.</summary>
        /// <param name="view">Existing camera that owns the pointer.</param>
        /// <param name="texture">Optional authored graphic; null uses a built-in contrasting crosshair.</param>
        internal static void Draw(Camera view, Texture2D texture)
        {
            // IMGUI renders directly; no Canvas, GameObject, material or texture is instantiated.
            if (Event.current.type != EventType.Repaint || view.targetTexture != null || view.targetDisplay != 0)
                return;
            Vector2 center = view.pixelRect.center;
            center.y = Screen.height - center.y;
            Color previous = GUI.color;
            GUI.color = Color.white;
            if (texture != null)
                GUI.DrawTexture(new Rect(center.x - texture.width * 0.5f, center.y - texture.height * 0.5f,
                    texture.width, texture.height), texture);
            else
            {
                // A dark border keeps the marker readable over both sky and shaded geometry.
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(center.x - 7f, center.y - 2f, 14f, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - 2f, center.y - 7f, 4f, 14f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(center.x - 6f, center.y - 1f, 12f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - 1f, center.y - 6f, 2f, 12f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        #endregion
    }
}
