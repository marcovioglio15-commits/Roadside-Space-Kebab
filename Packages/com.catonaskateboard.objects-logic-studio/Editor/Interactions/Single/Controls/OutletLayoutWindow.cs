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
        private ObjectLayoutViewport viewport;
        private System.Action<PreviewRenderUtility, AssemblyProductSettings> drawMeshes;
        private System.Action<AssemblyProductSettings> drawHandle;
        private SerializedObject data;
        private GameObject cached;
        private bool dirty = true;
        private bool resolvePending = true;
        private GameObject resolvedRoot;
        private string status = string.Empty;
        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Opens an independent layout transaction from the selected spray or dispenser card.</summary>
        /// <param name="workspace">Main tool containing the pending emitter settings.</param>
        /// <param name="layout">Pose list to edit in the shared viewport.</param>
        internal static void Open(ObjectWorkspace workspace, ObjectPointLayout layout)
        {
            string title = layout switch
            {
                ObjectPointLayout.OrbitCenters => "Orbit Center Layout",
                ObjectPointLayout.Magnets => "Scoop Magnet Layout",
                ObjectPointLayout.Outlets => "Outlet Layout",
                _ => "Nozzle Layout"
            };
            OutletLayoutWindow window = GetWindow<OutletLayoutWindow>(title);
            if (window.session.HasChanges)
            {
                window.status = "Apply or Discard before opening another layout.";
                window.Show();
                return;
            }
            window.session.Read(workspace, layout);
            window.hasUnsavedChanges = false;
            window.resolvePending = true;
            window.mode = 0;
            window.selected = 0;
            window.titleContent = new GUIContent(title);
            window.cached = null;
            window.dirty = true;
            window.Show();
        }
        /// <summary>Allocates editor-only preview resources and attaches change notifications.</summary>
        private void OnEnable()
        {
            minSize = new Vector2(500f, 400f);
            data = new SerializedObject(this);
            viewport = new ObjectLayoutViewport();
            drawMeshes = geometry.Draw;
            drawHandle = DrawHandle;
            wantsMouseMove = true;
            hasUnsavedChanges = session.HasChanges;
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
            viewport?.Dispose();
            viewport = null;
        }
        /// <summary>Refreshes cached geometry only after source or Undo changes.</summary>
        private void Refresh()
        {
            session.Refresh();
            resolvePending = true;
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
            // Resolve stable prefab identity only after source changes, not while orbiting or dragging a handle.
            if (resolvePending)
            {
                resolvedRoot = session.Target.Resolve();
                resolvePending = false;
            }
            GameObject root = resolvedRoot;
            if (root == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            RefreshGeometry(root);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                ObjectLayoutViewport.DrawTools(ref mode, session.Rotatable);
                GUILayout.FlexibleSpace();
                ObjectLayoutViewport.DrawNavigationHint();
                if (GUILayout.Button(new GUIContent("Frame", "Fit the object in the preview."), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    Frame();
            }
            Points();
            RefreshGeometry(root);
            if (session.Draft.Points is { Length: > 0 })
            {
                selected = Mathf.Clamp(selected, 0, session.Draft.Points.Length - 1);
                data.Update();
                SerializedProperty point = data.FindProperty("session.Draft.Points").GetArrayElementAtIndex(selected);
                float positionRange = session.Layout == ObjectPointLayout.OrbitCenters ? 20f : 2f;
                ObjectNumericControls.Vector(point.FindPropertyRelative("Position"), -positionRange, positionRange);
                if (session.Rotatable)
                    ObjectNumericControls.Vector(point.FindPropertyRelative("Rotation"), -360f, 360f);
                if (data.ApplyModifiedProperties())
                    hasUnsavedChanges = session.HasChanges;
                // Reserve the viewport after Unity has measured headers and wrapped vector controls.
                Rect area = GUILayoutUtility.GetRect(1f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawViewport(area);
            }
            else
                EditorGUILayout.LabelField("Add a point to place it in the preview.", EditorStyles.miniLabel);
            saveChangesMessage = "Apply or discard the pending point layout.";
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
            Rect area = viewport.ResolveRect(rect);
            if (navigation.Handle(area, ref orbit, ref pivot, ref distance, ref mode))
                Frame();
            if (mode is 1 or 2)
                mode = session.Rotatable ? 1 : 0;
            // Guide meshes track local moves without rebuilding their render entries.
            for (int index = 0; index < geometrySettings.Magnets.Length; index++)
                geometrySettings.Magnets[index].Position = session.Draft.Points[index].Position;
            viewport.Draw(area, orbit, pivot, distance, geometrySettings, drawMeshes, drawHandle);
            if (Event.current.type == EventType.Used || Event.current.type == EventType.MouseMove && area.Contains(Event.current.mousePosition))
                Repaint();
        }

        /// <summary>Draws point markers and edits the selected pose with native handles.</summary>
        /// <param name="layout">Preview geometry using the same point positions as the draft.</param>
        private void DrawHandle(AssemblyProductSettings layout)
        {
            DispenserOutlet point = session.Draft.Points[selected];
            if (!point.IsValid)
                return;
            // An orbit marker points along local Y; emitter markers point along local Z.
            if (Event.current.type == EventType.Repaint)
                for (int index = 0; index < layout.Magnets.Length; index++)
                    if (session.Draft.Points[index] is { IsValid: true } marker)
                    {
                        Quaternion orientation = Quaternion.Euler(marker.Rotation);
                        if (session.Layout == ObjectPointLayout.OrbitCenters)
                            GravityOrbitGizmos.Draw(marker.Position, orientation, index == selected);
                        else
                            StudioOutletHandles.Marker(marker.Position, orientation, index == selected);
                    }

            Vector3 position = point.Position;
            Quaternion rotation = Quaternion.Euler(point.Rotation);
            if (!StudioOutletHandles.Edit(ref position, ref rotation, mode, navigation.Active))
                return;
            Undo.RecordObject(this, "Place " + session.PointName);
            point.Position = position;
            if (mode == 1)
                point.Rotation = rotation.eulerAngles;
            hasUnsavedChanges = session.HasChanges;
            Repaint();
        }

        /// <summary>Selects, appends or removes independent dispenser points without modifying the source component.</summary>
        private void Points()
        {
            if (!session.Multiple)
                return;
            int count = session.Draft.Points?.Length ?? 0;
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUI.DisabledScope(selected <= 0))
                if (GUILayout.Button(new GUIContent("◀", "Edit the previous point."), GUILayout.Width(28f)))
                    selected--;
            string label = session.PointName;
            EditorGUILayout.LabelField(count > 0 ? label + " " + (selected + 1) + " / " + count : "No points", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(selected >= count - 1))
                if (GUILayout.Button(new GUIContent("▶", "Edit the next point."), GUILayout.Width(28f)))
                    selected++;
            if (GUILayout.Button(new GUIContent("+", "Add a new point."), GUILayout.Width(28f)))
            {
                Undo.RecordObject(this, "Add " + label);
                System.Array.Resize(ref session.Draft.Points, count + 1);
                selected = session.Draft.Points.Length - 1;
                session.Draft.Points[selected] = new DispenserOutlet();
                hasUnsavedChanges = session.HasChanges;
                dirty = true;
            }
            using (new EditorGUI.DisabledScope(count <= 1))
                if (GUILayout.Button(new GUIContent("−", "Remove the selected point."), GUILayout.Width(28f)))
                {
                    Undo.RecordObject(this, "Remove " + label);
                    for (int index = selected; index < session.Draft.Points.Length - 1; index++)
                        session.Draft.Points[index] = session.Draft.Points[index + 1];
                    System.Array.Resize(ref session.Draft.Points, session.Draft.Points.Length - 1);
                    selected = Mathf.Min(selected, session.Draft.Points.Length - 1);
                    hasUnsavedChanges = session.HasChanges;
                    dirty = true;
                }
        }
        /// <summary>Updates cached meshes and local portion guides only when the layout changes.</summary>
        /// <param name="root">Dispenser or nozzle owner being previewed.</param>
        private void RefreshGeometry(GameObject root)
        {
            int count = session.Draft.Points?.Length ?? 0;
            if (!dirty && cached == root && geometrySettings.Magnets.Length == count)
                return;
            geometrySettings.Magnets = new AssemblyMagnet[count];
            for (int index = 0; index < count; index++)
                geometrySettings.Magnets[index] = new AssemblyMagnet { Position = session.Draft.Points[index].Position, PreviewPrefab = session.Portion };
            geometry.Refresh(root, geometrySettings, false);
            if (cached != root)
                Frame();
            cached = root;
            dirty = false;
        }

        /// <summary>Fits the current source mesh and guide bounds without changing the layout.</summary>
        private void Frame()
        {
            Bounds bounds = geometry.Bounds(geometrySettings);
            foreach (DispenserOutlet point in session.Draft.Points)
                if (point is { IsValid: true })
                    bounds.Encapsulate(point.Position);
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
            {
                hasUnsavedChanges = false;
                base.SaveChanges();
            }
            Repaint();
        }
        /// <summary>Abandons nozzle edits without changing unrelated main-window settings.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            status = string.Empty;
            hasUnsavedChanges = false;
            dirty = true;
            base.DiscardChanges();
            Repaint();
        }
        #endregion
        #endregion
    }
}
