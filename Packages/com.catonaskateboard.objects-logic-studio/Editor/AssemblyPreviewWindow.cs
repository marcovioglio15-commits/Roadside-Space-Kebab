using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Provides isolated 3D navigation and transform handles for the retained product magnet draft.</summary>
    internal sealed class AssemblyPreviewWindow : EditorWindow
    {
        #region Serialized Fields

        [Header("Preview")]
        [Tooltip("Stable product component identity associated with this preview.")]
        [SerializeField]
        private long componentId;
        [Tooltip("Currently selected magnet index.")]
        [SerializeField]
        private int selected;
        [Tooltip("Move, rotate, scale or hand tool mode.")]
        [SerializeField]
        private int mode;
        [Tooltip("Orbit angles retained when the preview reloads.")]
        [SerializeField]
        private Vector2 orbit = new Vector2(25f, 145f);
        [Tooltip("Orbit focus in product-local coordinates.")]
        [SerializeField]
        private Vector3 pivot;
        [Tooltip("Preview camera distance from its focus point.")]
        [SerializeField]
        private float distance = 4f;

        #endregion

        #region State

        private static readonly GUIContent[] modes =
        {
            new GUIContent("Move", "Move the selected magnet in product-local space."),
            new GUIContent("Rotate", "Rotate the selected ingredient placement."),
            new GUIContent("Scale", "Scale the ingredient relative to its original prefab scale."),
            new GUIContent("Hand", "Pan the view with the left mouse button (Q).")
        };
        private readonly AssemblyPreviewNavigation navigation = new AssemblyPreviewNavigation();
        private readonly AssemblyPreviewGeometry geometry = new AssemblyPreviewGeometry();
        private PreviewRenderUtility preview;
        private SerializedObject data;
        private ObjectWorkspace state;
        private GameObject cachedRoot;
        private bool dirty = true;
        private bool framePending = true;
        private Vector2 scroll;
        private string status = string.Empty;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens the preview for the currently retained product card without changing scenes.</summary>
        /// <param name="workspace">Workspace owning the pending recipe.</param>
        /// <param name="product">Existing product component represented by that draft.</param>
        internal static void Open(ObjectWorkspace workspace, ObjectAssemblyProduct product)
        {
            // This window edits the same persistent draft as the main tool.
            AssemblyPreviewWindow window = GetWindow<AssemblyPreviewWindow>("Assembly Preview");
            window.componentId = ObjectWorkspaceTarget.FileId(product);
            window.state = workspace;
            window.dirty = window.framePending = true;
            window.Show();
        }

        /// <summary>Creates only editor preview resources and reconnects source-change notifications.</summary>
        private void OnEnable()
        {
            // PreviewRenderUtility owns a private preview scene; gameplay and native prefab stages stay untouched.
            state = ObjectWorkspace.instance;
            data = new SerializedObject(state);
            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 35f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            preview.ambientColor = Color.gray;
            minSize = new Vector2(740f, 480f);
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.projectChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.update += UpdateNavigation;
        }

        /// <summary>Disposes preview resources and saves the pending layout when closing or reloading.</summary>
        private void OnDisable()
        {
            // Every private preview scene and render texture has one deterministic owner.
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.projectChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            EditorApplication.update -= UpdateNavigation;
            navigation.Release();
            data?.Dispose();
            data = null;
            preview?.Cleanup();
            preview = null;
            if (state != null)
                state.Persist();
        }

        /// <summary>Invalidates source geometry after asset edits, Undo or native hierarchy changes.</summary>
        private void Refresh()
        {
            // Mesh and material discovery is deferred until the next visible preview pass.
            dirty = true;
            Repaint();
        }

        /// <summary>Stops keyboard flight when the preview loses focus.</summary>
        private void OnLostFocus()
        {
            // Pointer capture and pressed keys belong only to the focused preview.
            navigation.Release();
        }

        /// <summary>Repaints only while captured keyboard flight moves the preview camera.</summary>
        private void UpdateNavigation()
        {
            // The editor update supplies continuous motion between IMGUI keyboard events.
            if (navigation.Tick(orbit, ref pivot))
                Repaint();
        }

        #endregion

        #region Layout

        /// <summary>Draws a dedicated viewport beside the selected slot's precise numeric controls.</summary>
        private void OnGUI()
        {
            // A window retained for a different or closed prefab must never expose another product's settings.
            GameObject root = state.Target.Resolve();
            if (!state.Target.IsOpen || root == null || state.Extended.Kind != ExtendedInteractionKind.AssemblyProduct
                || state.Extended.ComponentId != componentId || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.LabelField("Open this product's Assembly Product card in Object Assemble to resume its preview.", EditorStyles.wordWrappedLabel);
                return;
            }
            AssemblyProductSettings settings = state.Extended.Draft.AssemblyProduct.Settings;
            if (dirty || cachedRoot != root)
            {
                geometry.Refresh(root, settings);
                cachedRoot = root;
                dirty = false;
            }
            if (framePending)
            {
                Frame(settings);
                framePending = false;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                mode = GUILayout.Toolbar(mode, modes, EditorStyles.toolbarButton, GUILayout.Width(240f));
                if (GUILayout.Button(new GUIContent("Frame All", "Fit the product and all guide prefabs in the viewport."), EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Frame(settings);
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent("RMB + WASD/QE · Alt + drag · F", "RMB: look and fly with WASD/QE; Shift: faster; wheel while flying: speed. Alt+LMB: orbit; MMB: pan; Alt+RMB or wheel: zoom. F: frame selected; Q/W/E/R: hand/move/rotate/scale."), EditorStyles.miniLabel);
            }
            Rect viewport = new Rect(0f, 22f, Mathf.Max(200f, position.width - 310f), position.height - 46f);
            DrawViewport(viewport, settings);
            GUILayout.BeginArea(new Rect(viewport.xMax + 6f, 28f, 298f, position.height - 58f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSelection(settings);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.Label(new Rect(8f, position.height - 22f, position.width - 16f, 20f),
                status.Length > 0 ? status : state.HasChanges ? "Pending layout · Apply saves directly to the product prefab" : "Applied layout", EditorStyles.miniLabel);
        }

        /// <summary>Edits the selected slot and commits through the main workspace transaction.</summary>
        /// <param name="settings">Current recipe layout.</param>
        private void DrawSelection(AssemblyProductSettings settings)
        {
            // Slots are created in the product card; this panel focuses on placement and guide selection.
            if (settings.Magnets.Length == 0)
            {
                EditorGUILayout.LabelField("Add magnets in the product card to begin arranging ingredients.", EditorStyles.wordWrappedLabel);
                return;
            }
            selected = Mathf.Clamp(selected, 0, settings.Magnets.Length - 1);
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (GUILayout.Toggle(selected == index, new GUIContent((index + 1) + ". " + settings.Magnets[index].Name,
                    "Select this ingredient placement slot."), EditorStyles.miniButton))
                    selected = index;
            EditorGUILayout.Space(8f);
            data.Update();
            AssemblyControls.DrawMagnet(data.FindProperty("Extended.Draft.AssemblyProduct.Settings.Magnets").GetArrayElementAtIndex(selected));
            if (data.ApplyModifiedProperties())
            {
                state.Persist();
                dirty = true;
                Repaint();
            }
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!state.HasChanges))
                if (GUILayout.Button(new GUIContent("Apply Layout", "Validate and save the retained workspace changes directly to this prefab.")))
                {
                    ObjectWorkspaceSession.Apply(state, out status);
                    Refresh();
                }
        }

        #endregion

        #region Viewport

        /// <summary>Renders shared source geometry and handles inside one preview coordinate space.</summary>
        /// <param name="rect">Viewport rectangle in window coordinates.</param>
        /// <param name="settings">Current recipe layout.</param>
        private void DrawViewport(Rect rect, AssemblyProductSettings settings)
        {
            // A local GUI group keeps input and render coordinates aligned at arbitrary editor DPI.
            GUI.BeginGroup(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            if (navigation.Handle(local, ref orbit, ref pivot, ref distance, ref mode))
                FrameSelection(settings);
            if (Event.current.type == EventType.Used)
                Repaint();
            Quaternion rotation = Quaternion.Euler(orbit.x, orbit.y, 0f);
            preview.camera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
            preview.camera.nearClipPlane = Mathf.Max(0.001f, distance * 0.001f);
            preview.camera.farClipPlane = Mathf.Max(100f, distance * 20f);
            preview.camera.aspect = rect.width / rect.height;
            preview.BeginPreview(local, GUIStyle.none);
            Texture texture;
            try
            {
                if (Event.current.type == EventType.Repaint)
                {
                    geometry.Draw(preview, settings);
                    preview.Render(true, false);
                }
                using (new Handles.DrawingScope(Matrix4x4.identity))
                {
                    Handles.SetCamera(preview.camera);
                    DrawHandles(settings);
                }
            }
            finally
            {
                texture = preview.EndPreview();
            }
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(local, texture, ScaleMode.StretchToFill, false);
            GUI.EndGroup();
        }

        /// <summary>Edits selected magnet transforms with standard native position, rotation and scale handles.</summary>
        /// <param name="settings">Pending slot layout.</param>
        private void DrawHandles(AssemblyProductSettings settings)
        {
            // Empty slots remain visible even without a guide prefab.
            for (int index = 0; index < settings.Magnets.Length; index++)
            {
                AssemblyMagnet marker = settings.Magnets[index];
                if (!AssemblyValidation.ValidMagnet(marker))
                    continue;
                Handles.color = index == selected ? new Color(1f, 0.72f, 0.18f) : new Color(0.3f, 0.8f, 0.95f);
                float size = HandleUtility.GetHandleSize(marker.Position) * 0.06f;
                if (Handles.Button(marker.Position, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                {
                    selected = index;
                    Repaint();
                }
                Handles.Label(marker.Position + Vector3.up * size * 2f, marker.Name);
            }
            if (selected < 0 || selected >= settings.Magnets.Length || Event.current.alt || navigation.Active || mode == 3)
                return;
            AssemblyMagnet magnet = settings.Magnets[selected];
            if (!AssemblyValidation.ValidMagnet(magnet))
                return;
            Vector3 position = magnet.Position;
            Quaternion rotation = Quaternion.Euler(magnet.Rotation);
            Vector3 scale = magnet.Scale;
            EditorGUI.BeginChangeCheck();
            switch (mode)
            {
                case 0:
                    position = Handles.PositionHandle(position, rotation);
                    break;
                case 1:
                    rotation = Handles.RotationHandle(rotation, position);
                    break;
                case 2:
                    scale = Handles.ScaleHandle(scale, position, rotation, HandleUtility.GetHandleSize(position));
                    break;
            }
            if (!EditorGUI.EndChangeCheck())
                return;
            Undo.RecordObject(state, "Place assembly magnet");
            magnet.Position = position;
            magnet.Rotation = rotation.eulerAngles;
            magnet.Scale = scale;
            state.Persist();
            Repaint();
        }

        /// <summary>Frames the selected guide while keeping the current viewing direction.</summary>
        /// <param name="settings">Current recipe and guide layout.</param>
        private void FrameSelection(AssemblyProductSettings settings)
        {
            // Empty slots still have a useful focus; no selection falls back to the whole product.
            if (selected < 0 || selected >= settings.Magnets.Length)
            {
                Frame(settings);
                return;
            }
            Bounds bounds = geometry.Bounds(settings, selected);
            pivot = bounds.center;
            distance = Mathf.Max(0.2f, bounds.extents.magnitude * 3.5f);
            Repaint();
        }

        /// <summary>Fits all visible guides and slot positions in the preview.</summary>
        /// <param name="settings">Current recipe layout.</param>
        private void Frame(AssemblyProductSettings settings)
        {
            // Frame is an explicit viewport operation, independent of serialized recipe validation.
            Bounds bounds = geometry.Bounds(settings);
            pivot = bounds.center;
            distance = Mathf.Max(0.2f, bounds.extents.magnitude * 3.5f);
            Repaint();
        }

        #endregion

        #endregion
    }
}
