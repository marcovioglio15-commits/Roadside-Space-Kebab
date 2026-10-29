using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Positions spray nozzles and dispenser outlets against shared geometry in one isolated viewport.</summary>
    internal sealed class OutletLayoutWindow : EditorWindow
    {
        #region Fields
        [Header("Preview")]
        [Tooltip("Independent output layout Apply and Discard transaction.")]
        [SerializeField]
        private OutletLayoutSession session = new OutletLayoutSession();
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
        [Tooltip("Output point currently edited by the preview handles.")]
        [SerializeField]
        private int selected;
        private readonly AssemblyPreviewGeometry geometry = new AssemblyPreviewGeometry();
        private readonly StudioPreviewNavigation navigation = new StudioPreviewNavigation();
        private readonly AssemblyProductSettings geometrySettings = new AssemblyProductSettings();
        private PreviewRenderUtility preview;
        private SerializedObject data;
        private GameObject cached;
        private bool dirty = true;
        private string status = string.Empty;
        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Opens an independent layout transaction from the selected spray or dispenser card.</summary>
        /// <param name="workspace">Main tool containing the pending emitter settings.</param>
        /// <param name="dispenser">Whether to edit multiple dispenser points instead of a single spray nozzle.</param>
        internal static void Open(ObjectWorkspace workspace, bool dispenser)
        {
            OutletLayoutWindow window = GetWindow<OutletLayoutWindow>(dispenser ? "Outlet Layout" : "Nozzle Layout");
            if (window.session.HasChanges)
            {
                window.status = "Apply or Discard before opening another layout.";
                window.Show();
                return;
            }
            window.session.Read(workspace, dispenser);
            window.selected = 0;
            window.titleContent = new GUIContent(dispenser ? "Outlet Layout" : "Nozzle Layout");
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
            StudioFieldMenu.Changed += FieldChanged;
            EditorApplication.update += Navigate;
        }
        /// <summary>Releases the private preview scene and its input capture.</summary>
        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            StudioFieldMenu.Changed -= FieldChanged;
            EditorApplication.update -= Navigate;
            navigation.Release();
            data?.Dispose();
            preview?.Cleanup();
        }
        /// <summary>Refreshes cached geometry only after source or Undo changes.</summary>
        private void Refresh()
        {
            session.Refresh();
            hasUnsavedChanges = session.HasChanges;
            dirty = true;
            Repaint();
        }
        /// <summary>Updates close protection immediately after a delayed field-menu operation.</summary>
        /// <param name="edited">Editor object whose serialized layout was changed.</param>
        private void FieldChanged(Object edited)
        {
            if (edited == this)
                Refresh();
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
                StudioOutletHandles.Toolbar(ref mode);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Frame", "Fit the object in the preview."), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    Frame();
            }
            Points();
            if (session.Draft.Points is { Length: > 0 })
            {
                selected = Mathf.Clamp(selected, 0, session.Draft.Points.Length - 1);
                data.Update();
                SerializedProperty point = data.FindProperty("session.Draft.Points").GetArrayElementAtIndex(selected);
                StudioFieldGUI.PropertyField(point.FindPropertyRelative("Position"));
                StudioFieldGUI.PropertyField(point.FindPropertyRelative("Rotation"));
                data.ApplyModifiedProperties();
                // Reserve the viewport after Unity has measured headers and wrapped vector controls.
                Rect viewport = GUILayoutUtility.GetRect(1f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawViewport(viewport);
            }
            else
                EditorGUILayout.LabelField("Add an outlet to place it in the preview.", EditorStyles.miniLabel);
            hasUnsavedChanges = session.HasChanges;
            saveChangesMessage = "Apply or discard the pending outlet layout.";
            using EditorGUILayout.VerticalScope footer = new EditorGUILayout.VerticalScope(GUILayout.Height(52f));
            EditorGUILayout.LabelField(status.Length > 0 ? status : hasUnsavedChanges ? "Pending layout" : "Applied layout", EditorStyles.miniLabel);
            using (new StudioButton.RowScope())
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
            {
                if (StudioButton.Draw(new GUIContent("Apply to draft", "Copy these poses into the settings group."), expandWidth: true, minimumHeight: 26f))
                    SaveChanges();
                if (StudioButton.Draw(new GUIContent("Discard", "Reload poses from the settings group."), expandWidth: true, minimumHeight: 26f))
                    DiscardChanges();
            }
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
            DispenserOutlet nozzle = session.Draft.Points[selected];
            if (!nozzle.IsValid)
                return;
            Quaternion rotation = Quaternion.Euler(nozzle.Rotation);
            for (int index = 0; index < session.Draft.Points.Length; index++)
                if (session.Draft.Points[index] is { IsValid: true } outlet)
                    StudioOutletHandles.Marker(outlet.Position, Quaternion.Euler(outlet.Rotation), index == selected);
            Vector3 point = nozzle.Position;
            if (!StudioOutletHandles.Edit(ref point, ref rotation, mode, navigation.Active))
                return;
            Undo.RecordObject(this, "Place Nozzle");
            nozzle.Position = point;
            nozzle.Rotation = rotation.eulerAngles;
            Repaint();
        }
        /// <summary>Selects, appends or removes independent dispenser points without modifying the source component.</summary>
        private void Points()
        {
            if (!session.Dispenser)
                return;
            int count = session.Draft.Points?.Length ?? 0;
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUI.DisabledScope(selected <= 0))
                if (GUILayout.Button(new GUIContent("◀", "Edit the previous outlet."), GUILayout.Width(28f)))
                    selected--;
            EditorGUILayout.LabelField(count > 0 ? "Outlet " + (selected + 1) + " / " + count : "No outlets", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(selected >= count - 1))
                if (GUILayout.Button(new GUIContent("▶", "Edit the next outlet."), GUILayout.Width(28f)))
                    selected++;
            if (GUILayout.Button(new GUIContent("+", "Add a new output point."), GUILayout.Width(28f)))
            {
                Undo.RecordObject(this, "Add Outlet");
                System.Array.Resize(ref session.Draft.Points, count + 1);
                selected = session.Draft.Points.Length - 1;
                session.Draft.Points[selected] = new DispenserOutlet();
            }
            using (new EditorGUI.DisabledScope(count <= 1))
                if (GUILayout.Button(new GUIContent("−", "Remove the selected output point."), GUILayout.Width(28f)))
                {
                    Undo.RecordObject(this, "Remove Outlet");
                    for (int index = selected; index < session.Draft.Points.Length - 1; index++)
                        session.Draft.Points[index] = session.Draft.Points[index + 1];
                    System.Array.Resize(ref session.Draft.Points, session.Draft.Points.Length - 1);
                    selected = Mathf.Min(selected, session.Draft.Points.Length - 1);
                }
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
