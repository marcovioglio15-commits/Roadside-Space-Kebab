using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Provides Scene View mouse gestures and keyboard flight inside the isolated assembly viewport.</summary>
    internal sealed class AssemblyPreviewNavigation
    {
        #region State

        private int control;
        private int button = -1;
        private bool orbiting;
        private bool dolly;
        private bool fast;
        private bool forward;
        private bool backward;
        private bool left;
        private bool right;
        private bool up;
        private bool down;
        private float speed = 2f;
        private double lastUpdate;

        #endregion

        #region Properties

        /// <summary>Whether a captured view gesture should suspend magnet handles.</summary>
        internal bool Active => button >= 0;

        #endregion

        #region Methods

        #region Input

        /// <summary>Consumes navigation gestures while leaving ordinary magnet clicks and text fields available.</summary>
        /// <param name="rect">Viewport in local GUI coordinates.</param>
        /// <param name="orbit">Retained pitch and yaw.</param>
        /// <param name="pivot">Retained view focus.</param>
        /// <param name="distance">Retained camera distance.</param>
        /// <param name="mode">Move, rotate, scale or hand tool index.</param>
        /// <returns>True when F requests framing the selected magnet.</returns>
        internal bool Handle(Rect rect, ref Vector2 orbit, ref Vector3 pivot, ref float distance, ref int mode)
        {
            // Hot-control capture keeps drags coherent when the pointer temporarily leaves the viewport.
            Event current = Event.current;
            int identifier = GUIUtility.GetControlID(FocusType.Passive);
            bool inside = rect.Contains(current.mousePosition);
            if (!inside && !Active || current.isKey && EditorGUIUtility.editingTextField && !Active)
                return false;
            fast = current.shift;
            switch (current.type)
            {
                case EventType.MouseDown when GUIUtility.hotControl == 0
                    && (current.button is 1 or 2 || current.button == 0 && (current.alt || mode == 3)):
                    control = identifier;
                    GUIUtility.hotControl = control;
                    GUIUtility.keyboardControl = 0;
                    EditorGUIUtility.editingTextField = false;
                    button = current.button;
                    orbiting = current.alt && button == 0;
                    dolly = current.alt && button == 1;
                    lastUpdate = EditorApplication.timeSinceStartup;
                    current.Use();
                    break;
                case EventType.MouseUp when Active && current.button == button:
                    Release();
                    current.Use();
                    break;
                case EventType.MouseDrag when Active:
                    Drag(current.delta, ref orbit, ref pivot, ref distance);
                    current.Use();
                    break;
                case EventType.ScrollWheel:
                    if (button == 1 && !dolly)
                        speed = Mathf.Clamp(speed * Mathf.Exp(-current.delta.y * 0.12f), 0.01f, 1000f);
                    else
                        distance = Mathf.Clamp(distance * Mathf.Exp(current.delta.y * 0.06f), 0.02f, 10000f);
                    current.Use();
                    break;
                case EventType.KeyDown:
                case EventType.KeyUp:
                    if (current.keyCode == KeyCode.Escape && Active)
                    {
                        Release();
                        current.Use();
                    }
                    else if (button == 1 && !dolly && SetKey(current.keyCode, current.type == EventType.KeyDown))
                        current.Use();
                    else if (!Active && current.type == EventType.KeyDown && !current.alt && !current.control && !current.command)
                        return Shortcut(current, ref mode);
                    break;
            }
            return false;
        }

        /// <summary>Moves continuously while the right mouse button and a flight key are held.</summary>
        /// <param name="orbit">Current camera orientation.</param>
        /// <param name="pivot">View focus moved together with the flying camera.</param>
        /// <returns>True when a new camera position needs repainting.</returns>
        internal bool Tick(Vector2 orbit, ref Vector3 pivot)
        {
            // Inactive previews do no continuous repainting; delta is bounded after editor stalls.
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min(0.05f, (float)(now - lastUpdate));
            lastUpdate = now;
            if (button != 1 || dolly)
                return false;
            Vector3 direction = new Vector3((right ? 1f : 0f) - (left ? 1f : 0f),
                (up ? 1f : 0f) - (down ? 1f : 0f), (forward ? 1f : 0f) - (backward ? 1f : 0f));
            if (direction.sqrMagnitude == 0f)
                return false;
            pivot += Quaternion.Euler(orbit.x, orbit.y, 0f) * direction.normalized * (speed * (fast ? 4f : 1f) * delta);
            return true;
        }

        /// <summary>Releases pointer and keyboard state when a gesture, focus or window lifetime ends.</summary>
        internal void Release()
        {
            // Focus loss must not leave the next viewport repaint flying or suppressing handles.
            if (control != 0 && GUIUtility.hotControl == control)
                GUIUtility.hotControl = 0;
            control = 0;
            button = -1;
            forward = backward = left = right = up = down = false;
        }

        #endregion

        #region Gestures

        /// <summary>Applies orbit, free look, pan or dolly with the usual Scene View mouse combinations.</summary>
        /// <param name="delta">Captured pointer movement.</param>
        /// <param name="orbit">Camera pitch and yaw.</param>
        /// <param name="pivot">Camera focus.</param>
        /// <param name="distance">Distance to the focus.</param>
        private void Drag(Vector2 delta, ref Vector2 orbit, ref Vector3 pivot, ref float distance)
        {
            // Free look keeps the camera position fixed; orbit instead keeps the focus fixed.
            Quaternion rotation = Quaternion.Euler(orbit.x, orbit.y, 0f);
            if (dolly)
                distance = Mathf.Clamp(distance * Mathf.Exp((delta.x + delta.y) * 0.01f), 0.02f, 10000f);
            else if (orbiting || button == 1)
            {
                Vector3 position = pivot - rotation * Vector3.forward * distance;
                orbit.x = Mathf.Clamp(orbit.x + delta.y * 0.3f, -89.9f, 89.9f);
                orbit.y += delta.x * 0.3f;
                if (!orbiting)
                    pivot = position + Quaternion.Euler(orbit.x, orbit.y, 0f) * Vector3.forward * distance;
            }
            else
                pivot += rotation * new Vector3(-delta.x, delta.y, 0f) * distance * 0.0015f;
        }

        /// <summary>Retains only flight keys handled by this captured viewport.</summary>
        /// <param name="key">Keyboard key received by the focused window.</param>
        /// <param name="pressed">True on key down, false on release.</param>
        /// <returns>True when the key belongs to viewport flight.</returns>
        private bool SetKey(KeyCode key, bool pressed)
        {
            // Opposing keys cancel through the direction calculation instead of overwriting each other.
            switch (key)
            {
                case KeyCode.W: case KeyCode.UpArrow:
                    forward = pressed;
                    break;
                case KeyCode.S: case KeyCode.DownArrow:
                    backward = pressed;
                    break;
                case KeyCode.A: case KeyCode.LeftArrow:
                    left = pressed;
                    break;
                case KeyCode.D: case KeyCode.RightArrow:
                    right = pressed;
                    break;
                case KeyCode.E:
                    up = pressed;
                    break;
                case KeyCode.Q:
                    down = pressed;
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>Handles native tool letters and explicit selection framing outside flight mode.</summary>
        /// <param name="current">Keyboard event received over the viewport.</param>
        /// <param name="mode">Current magnet handle mode.</param>
        /// <returns>True when the selected magnet should be framed.</returns>
        private static bool Shortcut(Event current, ref int mode)
        {
            // Text inputs retain normal editing because the caller excludes active editor text fields.
            switch (current.keyCode)
            {
                case KeyCode.F:
                    current.Use();
                    return true;
                case KeyCode.W:
                    mode = 0;
                    break;
                case KeyCode.E:
                    mode = 1;
                    break;
                case KeyCode.R:
                    mode = 2;
                    break;
                case KeyCode.Q:
                    mode = 3;
                    break;
                default:
                    return false;
            }
            current.Use();
            return false;
        }

        #endregion

        #endregion
    }
}
