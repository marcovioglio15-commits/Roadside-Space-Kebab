using CatOnASkateboard.StudioColors.Editor;
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
        private AssemblyLayoutSession session = new AssemblyLayoutSession();
        [Tooltip("Currently selected magnet index.")]
        [SerializeField]
        private int selected;
        [Tooltip("Move, rotate, scale or hand tool mode.")]
        [SerializeField]
        private int mode;
        [Tooltip("Show the completed product appearance instead of ingredient placement guides.")]
        [SerializeField]
        private bool completed;
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

        private readonly StudioPreviewNavigation navigation = new StudioPreviewNavigation();
        private readonly AssemblyPreviewGeometry geometry = new AssemblyPreviewGeometry();
        private ObjectLayoutViewport viewport;
        private System.Action<PreviewRenderUtility, AssemblyProductSettings> drawMeshes;
        private System.Action<AssemblyProductSettings> drawHandles;
        private SerializedObject data;
        private GameObject cachedRoot;
        private bool dirty = true;
        private bool resolvePending = true;
        private GameObject resolvedRoot;
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
            AssemblyPreviewWindow window = GetWindow<AssemblyPreviewWindow>("Magnet Layout");
            if (!window.CanOpen())
                return;
            window.session.Read(workspace, product);
            window.ShowLayout("Magnet Layout");
        }

        /// <summary>Opens the shared placement viewport for an Actual Item Fill step.</summary>
        /// <param name="workspace">Container draft supplying the box and magnets.</param>
        /// <param name="threshold">Unique count of the step being edited.</param>
        internal static void OpenFill(ObjectWorkspace workspace, int threshold)
        {
            AssemblyPreviewWindow window = GetWindow<AssemblyPreviewWindow>("Fill Magnet Layout");
            if (!window.CanOpen())
                return;
            try
            {
                window.session.ReadFill(workspace, threshold);
                window.ShowLayout("Fill Magnet Layout");
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogWarning(exception.Message);
            }
        }

        /// <summary>Keeps pending changes attached to their original source before switching previews.</summary>
        /// <returns>True when the window can show another layout.</returns>
        private bool CanOpen()
        {
            if (!session.HasChanges)
                return true;
            status = "Apply or Discard before opening another layout.";
            Show();
            return false;
        }

        /// <summary>Frames a newly selected layout and clears the previous view state.</summary>
        /// <param name="title">Window title for the selected placement type.</param>
        private void ShowLayout(string title)
        {
            titleContent = new GUIContent(title);
            completed = false;
            status = string.Empty;
            dirty = framePending = resolvePending = true;
            hasUnsavedChanges = false;
            Show();
        }

        /// <summary>Creates only editor preview resources and reconnects source-change notifications.</summary>
        private void OnEnable()
        {
            // PreviewRenderUtility owns a private preview scene; gameplay and native prefab stages stay untouched.
            data = new SerializedObject(this);
            viewport = new ObjectLayoutViewport();
            drawMeshes = geometry.Draw;
            drawHandles = DrawHandles;
            wantsMouseMove = true;
            hasUnsavedChanges = session.HasChanges;
            minSize = new Vector2(740f, 480f);
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.projectChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
            StudioFieldMenu.Changed += FieldChanged;
            EditorApplication.update += UpdateNavigation;
        }

        /// <summary>Disposes preview resources and saves the pending layout when closing or reloading.</summary>
        private void OnDisable()
        {
            // Every private preview scene and render texture has one deterministic owner.
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.projectChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            StudioFieldMenu.Changed -= FieldChanged;
            EditorApplication.update -= UpdateNavigation;
            navigation.Release();
            data?.Dispose();
            data = null;
            viewport?.Dispose();
            viewport = null;
        }

        /// <summary>Invalidates source geometry after asset edits, Undo or native hierarchy changes.</summary>
        private void Refresh()
        {
            session.Refresh();
            resolvePending = true;
            hasUnsavedChanges = session.HasChanges;
            // Mesh and material discovery is deferred until the next visible preview pass.
            dirty = true;
            Repaint();
        }

        /// <summary>Protects pending layouts when a field menu writes outside the normal GUI pass.</summary>
        /// <param name="edited">Editor object receiving the delayed menu edit.</param>
        private void FieldChanged(Object edited)
        {
            if (edited == this)
                Refresh();
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
            // Resolve stable prefab identity only after source changes, not while orbiting or dragging a handle.
            if (resolvePending)
            {
                resolvedRoot = session.Target.Resolve();
                resolvePending = false;
            }
            GameObject root = resolvedRoot;
            if (root == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.LabelField("Reopen the layout from its interaction settings.", EditorStyles.wordWrappedLabel);
                return;
            }
            AssemblyProductSettings settings = session.Draft.Settings;
            if (dirty || cachedRoot != root)
            {
                geometry.Refresh(root, settings, completed);
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
                ObjectLayoutViewport.DrawTools(ref mode, scale: true);
                if (GUILayout.Button(new GUIContent("Frame All", "Fit the product and all guide prefabs in the viewport."), EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Frame(settings);
                if (!session.IsItemFill)
                {
                    EditorGUI.BeginChangeCheck();
                    completed = GUILayout.Toggle(completed, new GUIContent("Completed", "Show the final mesh and ingredient visibility after the recipe completes."),
                        EditorStyles.toolbarButton, GUILayout.Width(80f));
                    if (EditorGUI.EndChangeCheck())
                        Refresh();
                }
                GUILayout.FlexibleSpace();
                ObjectLayoutViewport.DrawNavigationHint();
            }
            Rect viewport = new Rect(0f, 22f, Mathf.Max(200f, position.width - 390f), position.height - 78f);
            DrawViewport(viewport, settings);
            GUILayout.BeginArea(new Rect(viewport.xMax + 6f, 28f, 378f, position.height - 90f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new ObjectStudioFieldLayout(195f))
                DrawSelection(settings);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            saveChangesMessage = "Apply or discard the pending Magnet Layout changes.";
            GUILayout.BeginArea(new Rect(8f, position.height - 52f, position.width - 16f, 48f));
            EditorGUILayout.LabelField(status.Length > 0 ? status : hasUnsavedChanges ? "Pending layout" : "Applied layout", EditorStyles.miniLabel);
            using (new StudioButton.RowScope())
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
            {
                if (StudioButton.Draw(new GUIContent("Apply to draft", "Copy these placements into the settings group."), expandWidth: true, minimumHeight: 26f))
                    SaveChanges();
                if (StudioButton.Draw(new GUIContent("Discard", "Reload placements from the settings group."), expandWidth: true, minimumHeight: 26f))
                    DiscardChanges();
            }
            GUILayout.EndArea();
        }

        /// <summary>Edits the selected slot and commits through the main workspace transaction.</summary>
        /// <param name="settings">Current recipe layout.</param>
        private void DrawSelection(AssemblyProductSettings settings)
        {
            // Slots are created in the product card; this panel focuses on placement and guide selection.
            if (settings.Magnets.Length == 0)
            {
                EditorGUILayout.LabelField("Add magnets in the interaction settings to place them here.", EditorStyles.wordWrappedLabel);
                return;
            }
            selected = Mathf.Clamp(selected, 0, settings.Magnets.Length - 1);
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (GUILayout.Toggle(selected == index, new GUIContent((index + 1) + ". " + settings.Magnets[index].Name,
                    "Select this ingredient placement slot."), EditorStyles.miniButton))
                    selected = index;
            EditorGUILayout.Space(8f);
            data.Update();
            SerializedProperty magnet = data.FindProperty("session.Draft.Settings.Magnets").GetArrayElementAtIndex(selected);
            if (session.IsItemFill)
                InventoryFillMagnetControls.Pose(magnet);
            else
                AssemblyControls.DrawMagnet(magnet);
            if (data.ApplyModifiedProperties())
            {
                dirty = true;
                hasUnsavedChanges = session.HasChanges;
                Repaint();
            }
        }

        /// <summary>Commits this window's layout and resolves the native unsaved-change prompt.</summary>
        public override void SaveChanges()
        {
            if (session.Apply(out status))
                base.SaveChanges();
            Refresh();
        }

        /// <summary>Discards this window's proposal without changing pending main-window settings.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            status = string.Empty;
            base.DiscardChanges();
            Refresh();
        }

        #endregion

        #region Viewport

        /// <summary>Renders shared source geometry and handles inside one preview coordinate space.</summary>
        /// <param name="rect">Viewport rectangle in window coordinates.</param>
        /// <param name="settings">Current recipe layout.</param>
        private void DrawViewport(Rect rect, AssemblyProductSettings settings)
        {
            Rect area = viewport.ResolveRect(rect);
            if (navigation.Handle(area, ref orbit, ref pivot, ref distance, ref mode))
                FrameSelection(settings);
            viewport.Draw(area, orbit, pivot, distance, settings, drawMeshes, drawHandles);
            if (Event.current.type == EventType.Used || Event.current.type == EventType.MouseMove && area.Contains(Event.current.mousePosition))
                Repaint();
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
            Undo.RecordObject(this, "Place assembly magnet");
            magnet.Position = position;
            magnet.Rotation = rotation.eulerAngles;
            magnet.Scale = scale;
            hasUnsavedChanges = session.HasChanges;
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
