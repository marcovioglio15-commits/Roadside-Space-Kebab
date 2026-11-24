using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Renders preview geometry and maps native handle picking to the same viewport at any editor DPI.</summary>
    public sealed class StudioLayoutViewport : IDisposable
    {
        #region State

        private static readonly GUIContent[] tools =
        {
            new GUIContent("Move", "Move the selected point (W)."),
            new GUIContent("Rotate", "Rotate the selected point (E)."),
            new GUIContent("Scale", "Scale the selected placement (R)."),
            new GUIContent("Hand", "Pan the view with the left mouse button (Q).")
        };
        private static readonly GUIContent navigationHint = new GUIContent("RMB + WASD/QE · Alt + drag · F",
            "Right drag looks and flies. Alt + left drag orbits. Middle drag pans. F frames the selection or layout.");

        private readonly PreviewRenderUtility preview = new PreviewRenderUtility();
        private Rect lastRect;
        private int handleControl;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Sets a common camera and lighting treatment for Studio placement tools.</summary>
        public StudioLayoutViewport()
        {
            preview.cameraFieldOfView = 35f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            preview.ambientColor = Color.gray;
        }

        /// <summary>Destroys the private preview scene and render target when the window closes.</summary>
        public void Dispose()
        {
            preview.Cleanup();
        }

        #endregion

        #region Drawing

        /// <summary>Draws a consistent toolbar while omitting tools the current layout does not use.</summary>
        /// <param name="mode">Retained move, rotate, scale or hand index.</param>
        /// <param name="rotate">Whether the selected point has an orientation.</param>
        /// <param name="scale">Whether the selected point has an editable scale.</param>
        public static void DrawTools(ref int mode, bool rotate = true, bool scale = false)
        {
            // Hidden handle modes cannot remain active after a shortcut from another layout.
            if (mode == 1 && !rotate || mode == 2 && !scale)
                mode = 0;
            // Preserve keyboard shortcuts even when some tools are hidden.
            for (int index = 0; index < tools.Length; index++)
                if ((index != 1 || rotate) && (index != 2 || scale)
                    && GUILayout.Toggle(mode == index, tools[index], EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    mode = index;
        }

        /// <summary>Displays navigation shortcuts in each placement preview.</summary>
        public static void DrawNavigationHint()
        {
            GUILayout.Label(navigationHint, EditorStyles.miniLabel);
        }

        /// <summary>Uses the last drawn rectangle when GUILayout is only measuring controls.</summary>
        /// <param name="measured">Viewport rectangle returned by the caller's layout.</param>
        /// <returns>Stable viewport in window GUI points.</returns>
        public Rect ResolveRect(Rect measured)
        {
            // GUILayout returns a dummy rectangle during Layout, when handles register their pick distances.
            if (Event.current.type == EventType.Repaint)
                lastRect = measured;
            return lastRect;
        }

        /// <summary>Uses a shared camera pose for rendering and input without rendering on input events.</summary>
        /// <typeparam name="T">Layout context shared by mesh and handle drawing.</typeparam>
        /// <param name="rect">Viewport in window GUI points.</param>
        /// <param name="orbit">Current pitch and yaw in degrees.</param>
        /// <param name="pivot">Focus in preview space.</param>
        /// <param name="distance">Camera distance from the focus.</param>
        /// <param name="context">Current layout settings.</param>
        /// <param name="meshes">Callback that submits cached preview meshes.</param>
        /// <param name="handles">Callback that draws or updates the current handles.</param>
        public void Draw<T>(Rect rect, Vector2 orbit, Vector3 pivot, float distance, T context,
            Action<PreviewRenderUtility, T> meshes, Action<T> handles)
        {
            if (rect.width < 2f || rect.height < 2f)
                return;

            // Keep field of view and pixel scale consistent between display and handle picking.
            Camera camera = preview.camera;
            Quaternion rotation = Quaternion.Euler(orbit.x, orbit.y, 0f);
            camera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = Mathf.Max(0.001f, distance * 0.001f);
            camera.farClipPlane = Mathf.Max(100f, distance * 20f);
            camera.aspect = rect.width / rect.height;
            Camera previous = Camera.current;
            Matrix4x4 matrix = GUI.matrix;
            GUI.BeginGroup(rect);
            try
            {
                Rect local = new Rect(0f, 0f, rect.width, rect.height);
                if (Event.current.type == EventType.Repaint)
                    Render(local, context, meshes, handles);
                else
                {
                    // Allocate the same handle IDs on every event, even when the pointer is over another panel.
                    EventType type = Event.current.type;
                    bool ignored = Event.current.isMouse && !local.Contains(Event.current.mousePosition)
                        && (handleControl == 0 || GUIUtility.hotControl != handleControl);
                    int previousControl = GUIUtility.hotControl;
                    if (ignored)
                        Event.current.type = EventType.Ignore;
                    try
                    {
                        DrawHandles(camera, context, handles);
                    }
                    finally
                    {
                        if (ignored)
                            Event.current.type = type;
                        if (GUIUtility.hotControl == 0)
                            handleControl = 0;
                        else if (!ignored && previousControl == 0)
                            handleControl = GUIUtility.hotControl;
                    }
                }
            }
            finally
            {
                Camera.SetupCurrent(previous);
                GUI.matrix = matrix;
                GUI.EndGroup();
            }
        }

        /// <summary>Renders the geometry and handle overlay into a single preview texture.</summary>
        /// <typeparam name="T">Layout context for the render callbacks.</typeparam>
        /// <param name="rect">Viewport in window GUI points.</param>
        /// <param name="context">Current layout settings.</param>
        /// <param name="meshes">Callback that submits the visible meshes.</param>
        /// <param name="handles">Callback that draws the handle overlay.</param>
        private void Render<T>(Rect rect, T context, Action<PreviewRenderUtility, T> meshes, Action<T> handles)
        {
            // Rendering and input share the same clipped, local viewport coordinates.
            preview.BeginPreview(rect, GUIStyle.none);
            Texture texture;
            try
            {
                meshes(preview, context);
                preview.Render(true, false);
                RenderTexture.active = preview.camera.targetTexture;
                DrawHandles(preview.camera, context, handles);
            }
            finally
            {
                texture = preview.EndPreview();
            }
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        /// <summary>Scopes native transform handles to this camera without changing their transforms.</summary>
        /// <typeparam name="T">Layout context used by the handle callback.</typeparam>
        /// <param name="camera">Configured preview camera.</param>
        /// <param name="context">Current layout settings.</param>
        /// <param name="handles">Callback that draws or updates the current handles.</param>
        private static void DrawHandles<T>(Camera camera, T context, Action<T> handles)
        {
            using Handles.DrawingScope scope = new Handles.DrawingScope(Matrix4x4.identity);
            Handles.SetCamera(camera);
            handles(context);
        }

        #endregion

        #endregion
    }
}

