using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Draws a circular track and progress arc without runtime textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("")]
    public sealed class AssemblyChargeRing : MaskableGraphic
    {
        #region State

        private const int segments = 96;
        private AssemblyChargeSettings settings;
        private float progress;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Applies one product's appearance at the start of a charge.</summary>
        /// <param name="configuration">Validated circular indicator settings.</param>
        internal void Configure(AssemblyChargeSettings configuration)
        {
            settings = configuration;
            progress = 0f;
            SetVerticesDirty();
        }

        /// <summary>Rebuilds only when the normalized charge amount changes.</summary>
        /// <param name="amount">Elapsed fraction of the required hold duration.</param>
        internal void SetProgress(float amount)
        {
            if (progress == amount)
                return;
            progress = amount;
            SetVerticesDirty();
        }

        /// <summary>Submits the background track and the remaining or accumulated progress to the existing canvas.</summary>
        /// <param name="vertices">Unity's reused UI mesh builder.</param>
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            // No mesh, material or UI object is instantiated by the gameplay presentation path.
            vertices.Clear();
            if (settings == null)
                return;
            DrawArc(vertices, 1f, settings.TrackColor);
            DrawArc(vertices, settings.Drain ? 1f - progress : progress, settings.RingColor);
            if (settings.Icon == null)
                DrawSymbol(vertices);
        }

        /// <summary>Adds a bounded strip of ring quads starting at the top of the authored rectangle.</summary>
        /// <param name="vertices">Destination UI vertex buffer.</param>
        /// <param name="amount">Fraction of the circle to display.</param>
        /// <param name="tint">Track or progress color.</param>
        private void DrawArc(VertexHelper vertices, float amount, Color tint)
        {
            // The final segment ends at the exact fraction rather than stepping by one whole segment.
            Rect bounds = GetPixelAdjustedRect();
            float outer = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            float inner = outer * (1f - settings.Thickness);
            float direction = settings.Clockwise ? -1f : 1f;
            int count = Mathf.CeilToInt(amount * segments);
            Vector2 previous = Vector2.up;
            for (int index = 1; index <= count; index++)
            {
                float angle = Mathf.PI * 0.5f + direction * Mathf.PI * 2f * Mathf.Min((float)index / segments, amount);
                Vector2 next = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int start = vertices.currentVertCount;
                vertices.AddVert(bounds.center + previous * outer, tint, Vector2.zero);
                vertices.AddVert(bounds.center + next * outer, tint, Vector2.zero);
                vertices.AddVert(bounds.center + next * inner, tint, Vector2.zero);
                vertices.AddVert(bounds.center + previous * inner, tint, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
                previous = next;
            }
        }

        /// <summary>Draws a compact completion mark when no custom sprite is selected.</summary>
        /// <param name="vertices">Existing UI vertex buffer shared with the ring.</param>
        private void DrawSymbol(VertexHelper vertices)
        {
            Rect bounds = GetPixelAdjustedRect();
            float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            Vector2 joint = bounds.center + new Vector2(-0.09f, -0.18f) * radius;
            DrawStroke(vertices, bounds.center + new Vector2(-0.3f, 0.02f) * radius, joint, radius * 0.055f);
            DrawStroke(vertices, joint, bounds.center + new Vector2(0.32f, 0.29f) * radius, radius * 0.055f);
        }

        /// <summary>Appends a solid stroke without allocating textures or additional graphics.</summary>
        /// <param name="vertices">Destination mesh builder.</param>
        /// <param name="from">Stroke origin in local panel coordinates.</param>
        /// <param name="to">Stroke destination in local panel coordinates.</param>
        /// <param name="halfWidth">Half the visible line thickness.</param>
        private void DrawStroke(VertexHelper vertices, Vector2 from, Vector2 to, float halfWidth)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
            int start = vertices.currentVertCount;
            vertices.AddVert(from - normal, settings.IconColor, Vector2.zero);
            vertices.AddVert(from + normal, settings.IconColor, Vector2.zero);
            vertices.AddVert(to + normal, settings.IconColor, Vector2.zero);
            vertices.AddVert(to - normal, settings.IconColor, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }

        #endregion

        #endregion
    }
}
