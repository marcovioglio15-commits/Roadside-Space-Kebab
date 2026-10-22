using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Places a detached muzzle pose against cached geometry, committing only its position and rotation.</summary>
    internal sealed class PlayerToolMuzzleWindow : EditorWindow
    {
        #region Fields

        [Header("Source")]
        [Tooltip("Tool asset or tool window draft receiving only the confirmed muzzle pose.")]
        [SerializeField]
        private PlayerTool tool;
        [Tooltip("Persistent tool retained when a muzzle preview outlives its parent tool window.")]
        [SerializeField]
        private PlayerTool sourceTool;
        [Tooltip("Optional tool transaction whose current draft receives the confirmed muzzle pose.")]
        [SerializeField]
        private PlayerToolWindow owner;
        [Tooltip("Sample hierarchy matching the player's Tools Hierarchy Root.")]
        [SerializeField]
        private GameObject hierarchySource;
        [Tooltip("Muzzle binding retained to detect source changes before Apply.")]
        [SerializeField]
        private string baselinePath;
        [Tooltip("Applied muzzle snapshot used to detect changes from another Inspector.")]
        [SerializeField]
        private PlayerToolPose baseline;
        [Tooltip("Independent muzzle proposal edited by numeric sliders and transform handles.")]
        [SerializeField]
        private PlayerToolPose pose;

        [Header("View")]
        [Tooltip("Retained orbit angles for the private preview camera.")]
        [SerializeField]
        private Vector2 orbit = new Vector2(20f, 145f);
        [Tooltip("Retained preview framing centre in root coordinates.")]
        [SerializeField]
        private Vector3 focus;
        [Tooltip("Retained preview camera distance.")]
        [SerializeField]
        private float distance = 2f;
        [Tooltip("Active position, rotation or navigation tool in the preview.")]
        [SerializeField]
        private int mode;

        private readonly PlayerToolPreviewGeometry geometry = new PlayerToolPreviewGeometry();
        private readonly StudioPreviewNavigation navigation = new StudioPreviewNavigation();
        private PreviewRenderUtility preview;
        private SerializedObject data;
        private bool dirty = true;
        private string status = string.Empty;

        #endregion

        #region Properties

        /// <summary>Whether this preview contains a position or rotation that has not been applied.</summary>
        private bool HasChanges => !pose.Position.Equals(baseline.Position) || !pose.Rotation.Equals(baseline.Rotation);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens an independent muzzle transaction without replacing any unfinished preview.</summary>
        /// <param name="source">Saved tool or detached tool draft whose muzzle is being edited.</param>
        /// <param name="hierarchy">Sample matching Tools Hierarchy Root.</param>
        internal static void Open(PlayerTool source, GameObject hierarchy)
        {
            PlayerToolMuzzleWindow window = GetWindow<PlayerToolMuzzleWindow>("Muzzle Preview");
            if (window.HasChanges)
            {
                window.status = "Apply or Discard the pending muzzle before opening another tool.";
                window.Show();
                return;
            }
            window.tool = source;
            window.sourceTool = EditorUtility.IsPersistent(source) ? source : null;
            window.owner = null;
            // Keep the preview attached when Apply or Discard replaces its parent's temporary copy.
            foreach (PlayerToolWindow candidate in Resources.FindObjectsOfTypeAll<PlayerToolWindow>())
                if (candidate.EditingTool == source)
                {
                    window.owner = candidate;
                    window.sourceTool = candidate.AppliedTool;
                    break;
                }
            window.hierarchySource = hierarchy;
            window.Read();
            window.geometry.Refresh(hierarchy);
            window.dirty = false;
            window.Frame();
            window.Show();
        }

        /// <summary>Allocates private editor rendering resources and attaches source change notifications.</summary>
        private void OnEnable()
        {
            minSize = new Vector2(480f, 420f);
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
            dirty = true;
        }

        /// <summary>Releases private geometry and preview resources when the window closes or scripts reload.</summary>
        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            EditorApplication.update -= Navigate;
            navigation.Release();
            geometry.Dispose();
            data?.Dispose();
            preview?.Cleanup();
        }

        /// <summary>Invalidates cached geometry after hierarchy, import or Undo changes.</summary>
        private void Refresh()
        {
            if (!HasChanges)
                Read();
            dirty = true;
            hasUnsavedChanges = HasChanges;
            Repaint();
        }

        #endregion

        #region Drawing

        /// <summary>Draws compact pose sliders, standard transform handles and an independent Apply/Discard footer.</summary>
        private void OnGUI()
        {
            ResolveTool();
            if (tool == null || hierarchySource == null || tool.Weapon == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.LabelField("Choose a tool and its hierarchy in Edit mode.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            if (dirty)
            {
                geometry.Refresh(hierarchySource);
                dirty = false;
            }
            // Keep source conflicts visible at Apply; a repaint never reloads a pending proposal.
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                StudioOutletHandles.Toolbar(ref mode);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Frame", "Fit the source geometry. Alt-drag or right-drag: orbit; middle-drag: pan; wheel: zoom."), EditorStyles.toolbarButton))
                    Frame();
            }
            data.Update();
            PlayerToolFields.Vector(data.FindProperty("pose.Position"), -5f, 5f);
            PlayerToolFields.Vector(data.FindProperty("pose.Rotation"), -180f, 180f);
            data.ApplyModifiedProperties();
            Transform anchor = PlayerHierarchy.Resolve(hierarchySource.transform, baselinePath);
            if (anchor != null && pose.IsValid())
                DrawViewport(GUILayoutUtility.GetRect(1f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)), anchor);
            else
                EditorGUILayout.LabelField("The muzzle parent is missing or its coordinates are invalid.", EditorStyles.wordWrappedMiniLabel);
            hasUnsavedChanges = HasChanges;
            saveChangesMessage = "Apply or discard the pending muzzle pose.";
            EditorGUILayout.LabelField(status.Length > 0 ? status : HasChanges ? "Pending muzzle" : "Applied muzzle", EditorStyles.miniLabel);
            using StudioButton.RowScope row = new StudioButton.RowScope();
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(!HasChanges);
            if (StudioButton.Draw(new GUIContent("Apply", owner != null
                ? "Update the tool window's muzzle proposal. Apply the tool to save it."
                : "Save only this muzzle's position and rotation."), expandWidth: true))
                SaveChanges();
            if (StudioButton.Draw(new GUIContent("Discard", "Reload the tool's current muzzle position and rotation."), expandWidth: true))
                DiscardChanges();
        }

        /// <summary>Renders cached meshes and handles in root-relative coordinates.</summary>
        /// <param name="rect">Available viewport in window coordinates.</param>
        /// <param name="anchor">Existing transform carrying the muzzle offset.</param>
        private void DrawViewport(Rect rect, Transform anchor)
        {
            if (rect.width < 2f || rect.height < 2f)
                return;
            GUI.BeginGroup(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            if (navigation.Handle(local, ref orbit, ref focus, ref distance, ref mode))
                Frame();
            Quaternion rotation = Quaternion.Euler(orbit.x, orbit.y, 0f);
            preview.camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            preview.camera.nearClipPlane = Mathf.Max(0.001f, distance * 0.001f);
            preview.camera.farClipPlane = Mathf.Max(100f, distance * 20f);
            preview.camera.aspect = local.width / local.height;
            preview.BeginPreview(local, GUIStyle.none);
            Texture texture;
            try
            {
                if (Event.current.type == EventType.Repaint)
                {
                    geometry.Draw(preview);
                    preview.Render(true, false);
                }
                using (new Handles.DrawingScope(Matrix4x4.identity))
                {
                    Handles.SetCamera(preview.camera);
                    DrawHandle(anchor);
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

        /// <summary>Converts handle changes back into the selected muzzle parent's coordinates.</summary>
        /// <param name="anchor">Existing muzzle parent in the source hierarchy.</param>
        private void DrawHandle(Transform anchor)
        {
            Matrix4x4 matrix = hierarchySource.transform.worldToLocalMatrix * anchor.localToWorldMatrix;
            Quaternion basis = Quaternion.Inverse(hierarchySource.transform.rotation) * anchor.rotation;
            Vector3 position = matrix.MultiplyPoint3x4(pose.Position);
            Quaternion rotation = basis * Quaternion.Euler(pose.Rotation);
            StudioOutletHandles.Marker(position, rotation);
            if (!StudioOutletHandles.Edit(ref position, ref rotation, mode, navigation.Active))
                return;
            Undo.RecordObject(this, "Place Tool Muzzle");
            pose.Position = matrix.inverse.MultiplyPoint3x4(position);
            pose.Rotation = (Quaternion.Inverse(basis) * rotation).eulerAngles;
            Repaint();
        }

        /// <summary>Repaints only while captured keyboard navigation is moving the preview camera.</summary>
        private void Navigate()
        {
            if (navigation.Tick(orbit, ref focus))
                Repaint();
        }

        /// <summary>Releases camera navigation when another editor takes focus.</summary>
        private void OnLostFocus()
        {
            navigation.Release();
        }

        /// <summary>Frames source geometry without changing the proposed muzzle.</summary>
        private void Frame()
        {
            focus = geometry.Bounds.center;
            distance = Mathf.Max(0.2f, geometry.Bounds.extents.magnitude * 3.5f);
            Repaint();
        }

        #endregion

        #region Transaction

        /// <summary>Follows a replaced parent draft or falls back to the saved tool after its window closes.</summary>
        private void ResolveTool()
        {
            // The preview keeps its own pending pose and conflict baseline while changing only the destination copy.
            if (owner != null && owner.AppliedTool == sourceTool)
                tool = owner.EditingTool;
            else if (sourceTool != null)
            {
                owner = null;
                tool = sourceTool;
            }
        }

        /// <summary>Loads only the live tool muzzle, leaving other weapon settings untouched.</summary>
        private void Read()
        {
            ResolveTool();
            if (tool == null || tool.Weapon == null)
                return;
            baselinePath = tool.Weapon.MuzzlePath;
            baseline = pose = new PlayerToolPose { Position = tool.Weapon.MuzzlePosition, Rotation = tool.Weapon.MuzzleRotation, Scale = Vector3.one };
            status = string.Empty;
        }

        /// <summary>Commits a valid muzzle only when its source path and applied pose have not changed elsewhere.</summary>
        public override void SaveChanges()
        {
            ResolveTool();
            status = "The muzzle source changed. Discard to reload its current pose.";
            if (tool == null || tool.Weapon == null || tool.Weapon.MuzzlePath != baselinePath
                || !tool.Weapon.MuzzlePosition.Equals(baseline.Position) || !tool.Weapon.MuzzleRotation.Equals(baseline.Rotation))
                return;
            status = "Use finite muzzle position and rotation values.";
            if (!pose.IsValid())
                return;
            Undo.RecordObject(tool, "Apply Tool Muzzle");
            tool.Weapon.MuzzlePosition = pose.Position;
            tool.Weapon.MuzzleRotation = pose.Rotation;
            EditorUtility.SetDirty(tool);
            // A tool window's preview updates only its parent proposal until that window applies it.
            if (EditorUtility.IsPersistent(tool))
                AssetDatabase.SaveAssetIfDirty(tool);
            StudioFieldMenu.Notify(tool);
            Read();
            base.SaveChanges();
            Repaint();
        }

        /// <summary>Discards only the detached muzzle proposal.</summary>
        public override void DiscardChanges()
        {
            Read();
            base.DiscardChanges();
            Repaint();
        }

        #endregion

        #endregion
    }
}
