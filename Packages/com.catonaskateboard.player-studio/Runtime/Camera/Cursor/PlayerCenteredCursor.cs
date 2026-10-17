using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Draws the locked-pointer marker as an existing graphic on the scene Canvas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Player Studio/Centered Cursor")]
    public sealed class PlayerCenteredCursor : MaskableGraphic
    {
        #region State

        private PlayerCameraRig rig;
        private Texture2D texture;
        private float sizeScale;
        private bool visible;

        #endregion

        #region Properties

        /// <summary>Texture used by the shared UI material, with white geometry for the default crosshair.</summary>
        public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Connects an existing player rig without creating UI or changing its cursor-capture policy.</summary>
        /// <param name="owner">Rig supplying capture state, or null when the observer leaves.</param>
        public void Bind(PlayerCameraRig owner)
        {
            rig = owner;
            visible = false;
            SetVerticesDirty();
        }

        /// <summary>Updates visibility and texture only when they change, after the camera finishes moving.</summary>
        private void LateUpdate()
        {
            bool next = rig != null && rig.CenteredCursorVisible;
            if (visible != next)
            {
                visible = next;
                SetVerticesDirty();
            }
            if (!visible)
                return;
            if (texture != rig.CenteredCursorTexture || sizeScale != rig.CenteredCursorScale)
            {
                texture = rig.CenteredCursorTexture;
                sizeScale = rig.CenteredCursorScale;
                SetMaterialDirty();
                SetVerticesDirty();
            }
            // Preserve original-pixel sizing even when the scene Canvas uses reference-resolution scaling.
            rectTransform.anchorMin = rectTransform.anchorMax = rig.View.rect.center;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one / canvas.scaleFactor;
        }

        #endregion

        #region Geometry

        /// <summary>Draws the selected texture or a contrasting crosshair through the existing CanvasRenderer.</summary>
        /// <param name="vertices">Reused UI mesh builder.</param>
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!visible)
                return;
            if (texture != null)
                DrawRect(vertices, new Vector2(texture.width, texture.height), Color.white);
            else
            {
                DrawRect(vertices, new Vector2(14f, 4f), Color.black);
                DrawRect(vertices, new Vector2(4f, 14f), Color.black);
                DrawRect(vertices, new Vector2(12f, 2f), Color.white);
                DrawRect(vertices, new Vector2(2f, 12f), Color.white);
            }
        }

        /// <summary>Appends one centered, textured quad using the rig's original-pixel scale.</summary>
        /// <param name="vertices">Destination mesh builder.</param>
        /// <param name="size">Unscaled dimensions in pixels.</param>
        /// <param name="tint">Crosshair border, center or texture color.</param>
        private void DrawRect(VertexHelper vertices, Vector2 size, Color tint)
        {
            Vector2 half = size * (sizeScale * 0.5f);
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector2(-half.x, -half.y), tint, Vector2.zero);
            vertices.AddVert(new Vector2(-half.x, half.y), tint, Vector2.up);
            vertices.AddVert(half, tint, Vector2.one);
            vertices.AddVert(new Vector2(half.x, -half.y), tint, Vector2.right);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }

        #endregion

        #endregion
    }
}
