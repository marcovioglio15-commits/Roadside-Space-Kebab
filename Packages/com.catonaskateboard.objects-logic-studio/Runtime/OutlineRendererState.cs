using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Marks an original renderer for the shared glow pass without cloning its mesh or materials.</summary>
    internal sealed class OutlineRendererState
    {
        #region State

        internal const uint RenderingLayer = 1u << 31;
        private static readonly int colorId = Shader.PropertyToID("_ObjectGlowColor");
        private static readonly int shapeId = Shader.PropertyToID("_ObjectGlowShape");
        private static int session;
        private readonly Renderer source;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly bool originalLayer;
        private bool visible;
        private int activeSession;

        #endregion

        #region Properties

        /// <summary>Number of registered renderers; zero skips all glow render passes.</summary>
        internal static int ActiveCount { get; private set; }

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Captures ownership of the reserved glow rendering bit once for this renderer.</summary>
        /// <param name="renderer">Original source supplied by prefab authoring or ingredient insertion.</param>
        internal OutlineRendererState(Renderer renderer)
        {
            // Other rendering bits always remain untouched.
            source = renderer;
            originalLayer = source != null && (source.renderingLayerMask & RenderingLayer) != 0;
            activeSession = session;
        }

        /// <summary>Resets the shared count when entering Play without managed reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Retained bindings detect the new session before adjusting the count.
            session++;
            ActiveCount = 0;
        }

        /// <summary>Changes glow membership only at availability boundaries.</summary>
        /// <param name="enabled">Whether the owning interaction currently permits glow.</param>
        internal void SetVisible(bool enabled)
        {
            // Removing a destroyed renderer must still release its registry count.
            if (activeSession != session)
            {
                activeSession = session;
                visible = false;
            }
            enabled &= source != null;
            if (visible != enabled)
                ActiveCount += enabled ? 1 : -1;
            visible = enabled;
            if (source != null)
                source.renderingLayerMask = enabled || originalLayer ? source.renderingLayerMask | RenderingLayer
                    : source.renderingLayerMask & ~RenderingLayer;
        }

        #endregion

        #region Settings

        /// <summary>Writes per-renderer glow properties at setup or explicit settings changes.</summary>
        /// <param name="settings">Validated appearance shared by the owning interaction.</param>
        internal void Apply(OutlineSettings settings)
        {
            // Slot overrides inherit the existing renderer block before adding this effect's private shader keys.
            if (source == null)
                return;
            source.GetPropertyBlock(properties);
            Write(settings);
            source.SetPropertyBlock(properties);
            int count = source.sharedMaterials.Length;
            for (int index = 0; index < count; index++)
            {
                source.GetPropertyBlock(properties, index);
                if (properties.isEmpty)
                    source.GetPropertyBlock(properties);
                Write(settings);
                source.SetPropertyBlock(properties, index);
            }
        }

        /// <summary>Updates only private effect properties in the retained material property block.</summary>
        /// <param name="settings">Validated width, light and crease threshold.</param>
        private void Write(OutlineSettings settings)
        {
            // Premultiplication makes color alpha an intensity control without altering source transparency.
            properties.SetColor(colorId, settings.Color * (settings.Intensity * settings.Color.a));
            properties.SetVector(shapeId, new Vector4(settings.Thickness, Mathf.Cos(settings.EdgeAngle * Mathf.Deg2Rad), 0f, 0f));
        }

        #endregion

        #endregion
    }
}
