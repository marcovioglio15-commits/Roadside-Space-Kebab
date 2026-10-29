using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws a detailed settings tree with mouse navigation and direct links to its editors.</summary>
    internal sealed class ObjectSettingsTree
    {
        #region State

        private readonly ObjectSettingsTreeLayout layout = new ObjectSettingsTreeLayout();
        private readonly ObjectSettingsTreeViewport camera = new ObjectSettingsTreeViewport();
        private GUIStyle labelStyle;
        private GUIStyle arrowStyle;
        private long selectedId;
        private bool focus = true;
        private Vector2 viewportSize;

        #endregion

        #region Methods

        #region Canvas

        /// <summary>Refreshes option blocks after interaction selection or settings changes.</summary>
        /// <param name="state">Selected interaction draft.</param>
        /// <param name="groups">Its thematic settings groups.</param>
        internal void Rebuild(ObjectWorkspace state, List<ObjectGroupInfo> groups)
        {
            if (selectedId != state.Navigation.SelectedId)
            {
                selectedId = state.Navigation.SelectedId;
                focus = true;
            }
            layout.Build(state, groups);
        }

        /// <summary>Draws the clipped canvas and handles pan, wheel zoom and block navigation.</summary>
        /// <param name="state">Workspace receiving navigation commands.</param>
        /// <param name="height">Available canvas height.</param>
        internal void Draw(ObjectWorkspace state, float height)
        {
            if (layout.Nodes.Count == 0)
                return;
            Toolbar();
            Rect viewport = GUILayoutUtility.GetRect(180f, Mathf.Max(180f, height - 28f), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                viewportSize = viewport.size;
                if (focus)
                {
                    camera.Focus(layout.Nodes[0].Rect, viewportSize);
                    focus = false;
                }
            }
            EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.14f, 0.15f, 0.16f) : new Color(0.82f, 0.83f, 0.85f));
            GUI.BeginGroup(viewport);
            try
            {
                Input(viewport.size);
                Connections(viewport.size);
                Blocks(state, viewport.size);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        /// <summary>Offers compact view controls without adding another settings panel.</summary>
        private void Toolbar()
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("Fit", "Fit the tree in this view."), EditorStyles.toolbarButton, GUILayout.Width(30f)))
                camera.Fit(layout.Size, viewportSize);
            if (GUILayout.Button(new GUIContent("100%", "Return to the interaction block at normal size."), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                focus = true;
            if (GUILayout.Button(new GUIContent("Expand all", "Show nested options and linked presets."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                layout.Expand(true);
            if (GUILayout.Button(new GUIContent("Reset", "Show sections and their first options."), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                layout.Expand(false);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            float zoom = GUILayout.HorizontalSlider(camera.Zoom, ObjectSettingsTreeViewport.MinimumZoom, ObjectSettingsTreeViewport.MaximumZoom, GUILayout.Width(64f));
            if (EditorGUI.EndChangeCheck())
                camera.ZoomAt(viewportSize * 0.5f, zoom);
            GUILayout.Label(new GUIContent(Mathf.RoundToInt(camera.Zoom * 100f) + "%", "Scroll to zoom. Drag the background or use the middle mouse button to move."), EditorStyles.miniLabel, GUILayout.Width(38f));
        }

        /// <summary>Captures background drags while leaving option clicks to their own buttons.</summary>
        /// <param name="size">Clipped canvas size.</param>
        private void Input(Vector2 size)
        {
            Event input = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            Rect viewport = new Rect(Vector2.zero, size);
            switch (input.GetTypeForControl(control))
            {
                case EventType.ScrollWheel when viewport.Contains(input.mousePosition):
                    camera.ZoomAt(input.mousePosition, camera.Zoom * Mathf.Pow(1.12f, -input.delta.y));
                    input.Use();
                    EditorWindow.focusedWindow?.Repaint();
                    break;
                case EventType.MouseDown when viewport.Contains(input.mousePosition) && (input.button == 2 || input.button == 0 && !Hit(input.mousePosition)):
                    GUIUtility.hotControl = control;
                    input.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    camera.Pan(input.delta);
                    input.Use();
                    EditorWindow.focusedWindow?.Repaint();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    input.Use();
                    break;
            }
        }

        /// <summary>Checks blocks in tree coordinates before starting a background drag.</summary>
        /// <param name="point">Mouse position inside the viewport.</param>
        /// <returns>True when a block owns the pointer.</returns>
        private bool Hit(Vector2 point)
        {
            Vector2 position = camera.ToTree(point);
            foreach (ObjectSettingsTreeLayout.Node node in layout.Nodes)
                if (node.Rect.Contains(position))
                    return true;
            return false;
        }

        #endregion

        #region Blocks

        /// <summary>Draws visible blocks only and preserves the clicked branch position when expanding.</summary>
        /// <param name="state">Current workspace.</param>
        /// <param name="size">Canvas size for clipping.</param>
        private void Blocks(ObjectWorkspace state, Vector2 size)
        {
            labelStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            arrowStyle ??= new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            labelStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(12f * camera.Zoom));
            arrowStyle.fontSize = labelStyle.fontSize;
            Rect visible = new Rect(Vector2.zero, size);
            ObjectSettingsTreeLayout.Node toggle = null;
            foreach (ObjectSettingsTreeLayout.Node node in layout.Nodes)
            {
                Rect block = camera.ToScreen(node.Rect);
                if (!block.Overlaps(visible))
                    continue;
                EditorGUI.DrawRect(block, node.Kind switch
                {
                    ObjectSettingsTreeLayout.NodeKind.Root => new Color(0.24f, 0.40f, 0.52f),
                    ObjectSettingsTreeLayout.NodeKind.Theme => new Color(0.24f, 0.32f, 0.39f),
                    ObjectSettingsTreeLayout.NodeKind.Section => new Color(0.24f, 0.28f, 0.32f),
                    _ => new Color(0.22f, 0.23f, 0.25f)
                });
                if (node.Expandable && GUI.Button(camera.ToScreen(new Rect(node.Rect.x + 2f, node.Rect.y + 4f, 20f, 30f)), new GUIContent(layout.Expanded(node) ? "▾" : "▸", "Show or hide the options inside."), arrowStyle))
                    toggle = node;
                using (new EditorGUI.DisabledScope(node.Group == null))
                    if (GUI.Button(camera.ToScreen(new Rect(node.Rect.x + 24f, node.Rect.y + 3f, node.Rect.width - 28f, node.Rect.height - 6f)), new GUIContent(node.Label, node.Tooltip), labelStyle))
                        ObjectSettingsTreeNavigation.Open(state, node);
            }
            if (toggle == null)
                return;
            Vector2 anchor = camera.ToScreen(toggle.Rect).center;
            layout.Toggle(toggle);
            ObjectSettingsTreeLayout.Node moved = layout.Nodes.Find(node => node.Key == toggle.Key);
            if (moved != null)
                camera.Pan(anchor - camera.ToScreen(moved.Rect).center);
            EditorWindow.focusedWindow?.Repaint();
        }

        /// <summary>Connects visible branch spans with scaled curves in viewport coordinates.</summary>
        /// <param name="size">Canvas size for clipping.</param>
        private void Connections(Vector2 size)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Handles.BeginGUI();
            foreach (ObjectSettingsTreeLayout.Node node in layout.Nodes)
            {
                if (node.Parent < 0)
                    continue;
                Rect parent = camera.ToScreen(layout.Nodes[node.Parent].Rect);
                Rect child = camera.ToScreen(node.Rect);
                if (parent.xMax > size.x || child.xMin < 0f || Mathf.Max(parent.center.y, child.center.y) < 0f || Mathf.Min(parent.center.y, child.center.y) > size.y)
                    continue;
                Vector3 start = new Vector3(parent.xMax, parent.center.y);
                Vector3 end = new Vector3(child.xMin, child.center.y);
                Handles.DrawBezier(start, end, start + Vector3.right * (40f * camera.Zoom), end - Vector3.right * (40f * camera.Zoom),
                    new Color(0.45f, 0.57f, 0.66f, 0.8f), null, Mathf.Max(1f, 2f * camera.Zoom));
            }
            Handles.EndGUI();
        }

        #endregion

        #endregion
    }
}
