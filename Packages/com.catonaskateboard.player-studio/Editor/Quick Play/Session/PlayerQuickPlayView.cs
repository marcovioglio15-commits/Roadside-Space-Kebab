using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Displays the actual test camera in Player Studio and scopes runtime input to its focused viewport.</summary>
    internal sealed class PlayerQuickPlayView : IDisposable
    {
        #region State

        private readonly EditorWindow window;
        private PlayerCameraRig rig;
        private PlayerInput input;
        private RenderTexture texture;
        private bool ownsInputScope;
        private bool ownsCursorPermission;
        private bool disposed;
        private bool captured;
        private bool waitingForRelease;
        private PlayerCameraSettings settings;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Attaches repaint and focus handling for one existing workspace.</summary>
        /// <param name="window">Player Studio window that owns the preview.</param>
        internal PlayerQuickPlayView(EditorWindow window)
        {
            // The runtime camera is resolved after the temporary scene has entered Play.
            this.window = window;
            EditorApplication.update += Update;
            EditorApplication.focusChanged += HandleApplicationFocus;
            window.rootVisualElement.RegisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
        }

        /// <summary>Restores input settings and releases the rendering target at Stop, reload or window close.</summary>
        public void Dispose()
        {
            // Event invocation snapshots can still contain this callback after unsubscription.
            if (disposed)
                return;
            disposed = true;
            // Only the temporary settings copy was changed; the project asset is never dirtied.
            EditorApplication.update -= Update;
            EditorApplication.focusChanged -= HandleApplicationFocus;
            window.rootVisualElement.UnregisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
            SetCaptured(false);
            if (rig != null && rig.View != null && rig.View.targetTexture == texture)
                rig.View.targetTexture = null;
            if (ownsInputScope)
                PlayerQuickPlayInputScope.Release();
            ReleaseTexture();
            rig = null;
            input = null;
        }

        /// <summary>Connects once to the owned test scene and repaints its rendered frames.</summary>
        private void Update()
        {
            // Scene transitions are handled by the window; an ordinary Play session is never intercepted.
            if (disposed || !PlayerQuickPlay.IsActive || !EditorApplication.isPlaying)
                return;
            if (rig == null)
            {
                foreach (PlayerCameraRig candidate in UnityEngine.Object.FindObjectsByType<PlayerCameraRig>(FindObjectsInactive.Exclude))
                    if (candidate.gameObject.scene.path == PlayerQuickPlay.ScenePath && candidate.View != null)
                    {
                        rig = candidate;
                        input = candidate.GetComponent<PlayerInput>();
                        rig.Host.MasterPreset.CameraPreset.TryGetSettings(out settings, out _);
                        PlayerQuickPlayInputScope.Acquire();
                        ownsInputScope = true;
                        input?.DeactivateInput();
                        rig.SetInputFocus(false);
                        window.Focus();
                        break;
                    }
            }

            // Leaving the preview cannot keep movement or cursor capture active in another editor panel.
            if (captured && (EditorWindow.focusedWindow != window || EditorApplication.isPaused))
                SetCaptured(false);
            window.Repaint();
        }

        #endregion

        #region Viewport

        /// <summary>Releases input when Unity loses operating-system focus, even if this remains its selected window.</summary>
        /// <param name="focused">Whether Unity has become the foreground application.</param>
        private void HandleApplicationFocus(bool focused)
        {
            // Background input routing must never keep this player moving while another application is used.
            if (!focused)
                SetCaptured(false);
        }

        /// <summary>Releases gameplay controls before interacting with another part of the workspace.</summary>
        /// <param name="eventData">Pointer press routed through the window root.</param>
        private void HandlePointerDown(PointerDownEvent eventData)
        {
            // A toolbar or sidebar click must stop held movement even while the same window stays focused.
            if (!captured)
                return;
            VisualElement viewport = window.rootVisualElement.Q("player-game-viewport");
            if (viewport == null || !viewport.worldBound.Contains(eventData.position))
                SetCaptured(false);
        }

        /// <summary>Draws the runtime camera texture and captures input only after a deliberate viewport click.</summary>
        internal void Draw()
        {
            // A dedicated IMGUI container prevents SceneView shortcuts from consuming gameplay keys.
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Event current = Event.current;
            if (current.type == EventType.Repaint)
            {
                EnsureTexture(rect);
                EditorGUI.DrawRect(rect, Color.black);
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                if (!captured)
                    GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f),
                        new GUIContent("Click to control • Esc to release", "Runs the current draft configuration through Unity Play Mode."));
                else if (settings.ShowCenteredCursor && ownsCursorPermission && UnityEngine.Cursor.lockState == CursorLockMode.Locked)
                    DrawCursor(rect.center);
            }
            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                window.Focus();
                GUIUtility.keyboardControl = GUIUtility.GetControlID(FocusType.Keyboard);
                waitingForRelease = true;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && waitingForRelease)
            {
                // The click used to enter the preview must not also trigger a gameplay action.
                waitingForRelease = false;
                SetCaptured(true);
                current.Use();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                SetCaptured(false);
                current.Use();
            }
        }

        /// <summary>Controls the existing input owner and rig without rebinding actions or creating runtime UI.</summary>
        /// <param name="value">Whether the preview currently owns gameplay input.</param>
        private void SetCaptured(bool value)
        {
            // Focus changes are explicit boundaries, not per-frame input reconfiguration.
            if (captured == value || value && rig == null)
                return;
            if (value && settings.LockCursor && settings.LookEnabled && settings.Mode != PlayerCameraMode.Fixed)
            {
                if (!PlayerQuickPlayCursor.TrySetAllowed(true))
                {
                    Debug.LogWarning("This Unity Editor version does not expose cursor capture for the embedded Play viewport.");
                    return;
                }
                ownsCursorPermission = true;
            }
            captured = value;
            if (value)
                input?.ActivateInput();
            else
                input?.DeactivateInput();
            rig?.SetInputFocus(value);
            if (!value && ownsCursorPermission)
            {
                PlayerQuickPlayCursor.TrySetAllowed(false);
                ownsCursorPermission = false;
            }
            window.Repaint();
        }

        /// <summary>Allocates a render target only when the viewport pixel dimensions change.</summary>
        /// <param name="rect">Available preview area in Editor points.</param>
        private void EnsureTexture(Rect rect)
        {
            // The enabled gameplay camera renders through the project's normal render pipeline.
            if (rig == null || rig.View == null || rect.width <= 1f || rect.height <= 1f)
                return;
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width * EditorGUIUtility.pixelsPerPoint));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height * EditorGUIUtility.pixelsPerPoint));
            if (texture != null && texture.width == width && texture.height == height)
                return;
            rig.View.targetTexture = null;
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Player Studio Quick Play",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            rig.View.targetTexture = texture;
            rig.View.aspect = (float)width / height;
        }

        /// <summary>Draws the configured center pointer over the embedded camera texture.</summary>
        /// <param name="center">Viewport center in Editor points.</param>
        private void DrawCursor(Vector2 center)
        {
            // Camera OnGUI does not render into a RenderTexture, so the Editor draws this small overlay.
            Texture2D cursor = settings.CursorTexture;
            if (cursor != null)
                GUI.DrawTexture(new Rect(center.x - cursor.width * 0.5f, center.y - cursor.height * 0.5f, cursor.width, cursor.height), cursor);
            else
            {
                EditorGUI.DrawRect(new Rect(center.x - 7f, center.y - 2f, 14f, 4f), Color.black);
                EditorGUI.DrawRect(new Rect(center.x - 2f, center.y - 7f, 4f, 14f), Color.black);
                EditorGUI.DrawRect(new Rect(center.x - 6f, center.y - 1f, 12f, 2f), Color.white);
                EditorGUI.DrawRect(new Rect(center.x - 1f, center.y - 6f, 2f, 12f), Color.white);
            }
        }

        /// <summary>Releases GPU and native resources owned by the last viewport size.</summary>
        private void ReleaseTexture()
        {
            // Release is called only on resize and disposal.
            if (texture == null)
                return;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        #endregion

        #endregion
    }
}
