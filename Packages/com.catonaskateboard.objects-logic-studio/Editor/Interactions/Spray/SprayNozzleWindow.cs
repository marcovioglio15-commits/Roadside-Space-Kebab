using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Positions a nozzle against shared source geometry in an isolated editor viewport.</summary>
    internal sealed class SprayNozzleWindow : EditorWindow
    {
        #region Fields
        [Header("Preview")]
        [Tooltip("Independent nozzle Apply and Discard transaction.")]
        [SerializeField]
        private SprayLayoutSession session = new SprayLayoutSession();
        [Tooltip("Orbit angles retained by the preview.")]
        [SerializeField]
        private Vector2 orbit = new Vector2(25f, 145f);
        [Tooltip("Orbit focus in object-local coordinates.")]
        [SerializeField]
        private Vector3 pivot;
        [Tooltip("Preview camera distance.")]
        [SerializeField]
        private float distance = 2f;
        [Tooltip("Active transform handle.")]
        [SerializeField]
        private int mode;
        private readonly AssemblyPreviewGeometry geometry = new AssemblyPreviewGeometry();
        private readonly AssemblyPreviewNavigation navigation = new AssemblyPreviewNavigation();
        private readonly AssemblyProductSettings geometrySettings = new AssemblyProductSettings();
        private PreviewRenderUtility preview;
        private SerializedObject data;
        private GameObject cached;
        private bool dirty = true;
        private string status = string.Empty;
        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Opens an independent nozzle transaction from the selected Spray Sauce card.</summary>
        /// <param name="workspace">Main tool containing the pending emitter settings.</param>
        internal static void Open(ObjectWorkspace workspace)
        {
            SprayNozzleWindow window = GetWindow<SprayNozzleWindow>("Nozzle Layout");
            if (window.session.HasChanges)
            {
                window.status = "Apply or Discard before opening another nozzle.";
                window.Show();
                return;
            }
            window.session.Read(workspace);
            window.cached = null;
            window.dirty = true;
            window.Show();
        }
        /// <summary>Allocates editor-only preview resources and attaches change notifications.</summary>
        private void OnEnable()
        {
            minSize = new Vector2(500f, 400f);
            data = new SerializedObject(this);
            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 35f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            preview.ambientColor = Color.gray;
            EditorApplication.projectChanged += Refresh;
            EditorApplication.hierarchyChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.update += Navigate;
        }
        /// <summary>Releases the private preview scene and its input capture.</summary>
        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            EditorApplication.update -= Navigate;
            navigation.Release();
            data?.Dispose();
            preview?.Cleanup();
        }
        /// <summary>Refreshes cached geometry only after source or Undo changes.</summary>
        private void Refresh()
        {
            dirty = true;
            Repaint();
        }
        /// <summary>Repaints while keyboard navigation is actively moving the camera.</summary>
        private void Navigate()
        {
            if (navigation.Tick(orbit, ref pivot))
                Repaint();
        }
        /// <summary>Releases navigation when focus moves to another editor.</summary>
        private void OnLostFocus()
        {
            navigation.Release();
        }
        #endregion
        #region Drawing
        /// <summary>Draws transform handles, precise coordinates and a full-width footer.</summary>
        private void OnGUI()
        {
            GameObject root = session.Target.Resolve();
            if (root == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (dirty || cached != root)
            {
                geometry.Refresh(root, geometrySettings, false);
                if (cached != root)
                    Frame();
                cached = root;
                dirty = false;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(mode == 0, new GUIContent(EditorGUIUtility.IconContent("MoveTool").image, "Move nozzle"), EditorStyles.toolbarButton, GUILayout.Width(36f)))
                    mode = 0;
                if (GUILayout.Toggle(mode == 1, new GUIContent(EditorGUIUtility.IconContent("RotateTool").image, "Rotate nozzle"), EditorStyles.toolbarButton, GUILayout.Width(36f)))
                    mode = 1;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Frame", "Fit the object in the preview."), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    Frame();
            }
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("session.Draft.Position"));
            EditorGUILayout.PropertyField(data.FindProperty("session.Draft.Rotation"));
            data.ApplyModifiedProperties();
            DrawViewport(new Rect(0f, 100f, position.width, Mathf.Max(100f, position.height - 156f)));
            hasUnsavedChanges = session.HasChanges;
            saveChangesMessage = "Apply or discard the pending nozzle layout.";
            GUILayout.BeginArea(new Rect(8f, position.height - 52f, position.width - 16f, 48f));
            EditorGUILayout.LabelField(status.Length > 0 ? status : hasUnsavedChanges ? "Pending layout" : "Applied layout", EditorStyles.miniLabel);
            using (new StudioButton.RowScope())
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
            {
                if (StudioButton.Draw(new GUIContent("Apply", "Save the nozzle pose."), expandWidth: true, minimumHeight: 26f))
                    SaveChanges();
                if (StudioButton.Draw(new GUIContent("Discard", "Reload the applied nozzle pose."), expandWidth: true, minimumHeight: 26f))
                    DiscardChanges();
            }
            GUILayout.EndArea();
        }
        /// <summary>Renders geometry without instantiating gameplay prefabs or components.</summary>
        /// <param name="rect">Viewport in window coordinates.</param>
        private void DrawViewport(Rect rect)
        {
            GUI.BeginGroup(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            if (navigation.Handle(local, ref orbit, ref pivot, ref distance, ref mode))
                Frame();
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
                    geometry.Draw(preview, geometrySettings);
                    preview.Render(true, false);
                }
                using (new Handles.DrawingScope(Matrix4x4.identity))
                {
                    Handles.SetCamera(preview.camera);
                    DrawHandle();
                }
            }
            finally
            {
                texture = preview.EndPreview();
            }
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(local, texture, ScaleMode.StretchToFill, false);
            GUI.EndGroup();
            if (Event.current.type == EventType.Used)
                Repaint();
        }
        /// <summary>Updates only the detached nozzle with standard move and rotate handles.</summary>
        private void DrawHandle()
        {
            SprayNozzle nozzle = session.Draft;
            if (!nozzle.IsValid())
                return;
            Quaternion rotation = Quaternion.Euler(nozzle.Rotation);
            float size = HandleUtility.GetHandleSize(nozzle.Position) * 0.35f;
            Handles.color = new Color(0.25f, 0.9f, 0.75f);
            Handles.ArrowHandleCap(0, nozzle.Position, rotation, size, EventType.Repaint);
            if (Event.current.alt || navigation.Active)
                return;
            EditorGUI.BeginChangeCheck();
            Vector3 point = mode == 0 ? Handles.PositionHandle(nozzle.Position, rotation) : nozzle.Position;
            Quaternion turn = mode == 1 ? Handles.RotationHandle(rotation, point) : rotation;
            if (!EditorGUI.EndChangeCheck())
                return;
            Undo.RecordObject(this, "Place Nozzle");
            nozzle.Position = point;
            nozzle.Rotation = turn.eulerAngles;
            Repaint();
        }
        /// <summary>Fits the current source mesh without changing the nozzle pose.</summary>
        private void Frame()
        {
            Bounds bounds = geometry.Bounds(geometrySettings);
            pivot = bounds.center;
            distance = Mathf.Max(0.2f, bounds.extents.magnitude * 3.5f);
            Repaint();
        }
        #endregion
        #region Transaction
        /// <summary>Saves only this window's independent nozzle proposal.</summary>
        public override void SaveChanges()
        {
            if (session.Apply(out status))
                base.SaveChanges();
            Repaint();
        }
        /// <summary>Abandons nozzle edits without changing unrelated main-window settings.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            status = string.Empty;
            base.DiscardChanges();
            Repaint();
        }
        #endregion
        #endregion
    }
}
