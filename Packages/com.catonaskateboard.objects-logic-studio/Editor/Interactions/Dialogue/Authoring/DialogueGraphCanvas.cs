using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws indented entry cards and directed links using the main tool's pan and zoom model.</summary>
    internal sealed class DialogueGraphCanvas
    {
        #region State

        private readonly ObjectSettingsTreeViewport viewport = new ObjectSettingsTreeViewport();
        private readonly DialogueGraphLayout layout = new DialogueGraphLayout();
        private string linking = string.Empty;
        private bool draggingLink;
        private bool framed;
        private DialogueNode dragged;
        private bool draggingStart;
        private Vector2 dragOrigin;
        private Vector2 pointerOrigin;
        private bool moved;
        private Rect inputBounds;
        private GUIStyle titleStyle;
        private GUIStyle captionStyle;
        private float styleZoom = -1f;
        private bool darkSkin;
        internal string Selected = string.Empty;
        internal string SelectedRow = string.Empty;

        #endregion

        #region Methods

        #region Navigation

        /// <summary>Fits this view without changing the other main or preview canvas.</summary>
        /// <param name="tree">Current sheet.</param>
        /// <param name="size">Available viewport size.</param>
        internal void Frame(DialogueTree tree, Vector2 size)
        {
            layout.Build(tree);
            viewport.Fit(layout.Bounds.size + Vector2.one * 32f, size);
            viewport.Offset -= (layout.Bounds.min - Vector2.one * 16f) * viewport.Zoom;
            framed = true;
        }

        /// <summary>Draws the same ordered tree in editable and preview views.</summary>
        /// <param name="area">Clipped viewport in window coordinates.</param>
        /// <param name="tree">Current detached sheet.</param>
        /// <param name="active">Optional block highlighted by playback.</param>
        /// <param name="changed">Callback invalidating selection and preview data.</param>
        /// <param name="repaint">Window repaint request.</param>
        /// <param name="readOnly">Whether connection and entry edits are disabled.</param>
        internal void Draw(Rect area, DialogueTree tree, string active, Action changed, Action repaint, bool readOnly = false)
        {
            layout.Build(tree);
            if (!framed && Event.current.type == EventType.Repaint)
                Frame(tree, area.size);
            PrepareStyles();
            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.78f, 0.78f, 0.78f));
            GUI.BeginGroup(area);
            inputBounds = new Rect(Vector2.zero, area.size);
            HandleNavigation(area, repaint);
            if (!readOnly && GUI.enabled)
            {
                HandleConnection(tree, changed, repaint);
                if (dragged != null || draggingStart || inputBounds.Contains(Event.current.mousePosition))
                    HandleBlocks(tree, changed, repaint);
            }
            DrawConnections(tree);
            // Block titles remain visually stronger than their inset child entries.
            foreach (DialogueNode block in tree.Nodes)
                if (block != null && layout.Blocks.TryGetValue(block.Id, out Rect rect))
                    DrawBlock(tree, block, rect, active, changed, readOnly);
            DrawStart(tree);
            GUI.EndGroup();
        }

        /// <summary>Zooms under the pointer and pans without moving or disordering blocks.</summary>
        /// <param name="area">Canvas viewport.</param>
        /// <param name="repaint">Window repaint callback.</param>
        private void HandleNavigation(Rect area, Action repaint)
        {
            Event current = Event.current;
            Rect local = new Rect(Vector2.zero, area.size);
            if (!local.Contains(current.mousePosition))
                return;
            if (current.type == EventType.ScrollWheel)
            {
                viewport.ZoomAt(current.mousePosition, viewport.Zoom * Mathf.Exp(-current.delta.y * 0.08f));
                current.Use();
                repaint();
            }
            else if (current.type == EventType.MouseDrag && (current.button == 2 || current.alt))
            {
                viewport.Pan(current.delta);
                current.Use();
                repaint();
            }
        }

        #endregion

        #region Rendering

        /// <summary>Reuses text styles until zoom or editor skin changes.</summary>
        private void PrepareStyles()
        {
            if (titleStyle == null || darkSkin != EditorGUIUtility.isProSkin)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel);
                captionStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
                darkSkin = EditorGUIUtility.isProSkin;
                styleZoom = -1f;
            }
            if (Mathf.Approximately(styleZoom, viewport.Zoom))
                return;
            styleZoom = viewport.Zoom;
            titleStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(15f * styleZoom));
            captionStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(11f * styleZoom));
        }

        /// <summary>Moves block headers and opens contextual creation or deletion at the pointer.</summary>
        /// <param name="tree">Detached sheet receiving layout edits.</param>
        /// <param name="changed">Draft change callback.</param>
        /// <param name="repaint">Window repaint request.</param>
        private void HandleBlocks(DialogueTree tree, Action changed, Action repaint)
        {
            Event current = Event.current;
            // Claim right presses before child buttons can capture them. Middle drag remains dedicated to pan.
            if (current.type == EventType.ContextClick || current.type == EventType.MouseDown && current.button == 1)
            {
                Vector2 point = viewport.ToTree(current.mousePosition);
                DialogueNode hovered = Array.Find(tree.Nodes, block => layout.Blocks.TryGetValue(block.Id, out Rect rect) && rect.Contains(point));
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Block"), false, () =>
                {
                    if (tree == null)
                        return;
                    Selected = DialogueGraphEdits.Add(tree, point).Id;
                    SelectedRow = string.Empty;
                    changed();
                });
                if (hovered != null)
                    menu.AddItem(new GUIContent("Delete Block"), false, () =>
                    {
                        if (tree == null)
                            return;
                        DialogueGraphEdits.Remove(tree, hovered.Id, string.Empty);
                        Selected = SelectedRow = string.Empty;
                        changed();
                    });
                else
                    menu.AddDisabledItem(new GUIContent("Delete Block"));
                menu.ShowAsContext();
                current.Use();
                return;
            }
            if (dragged != null || draggingStart)
            {
                if (current.type == EventType.MouseDrag && current.button == 0)
                {
                    if (!moved)
                        Undo.RecordObject(tree, "Move dialogue block");
                    moved = true;
                    Vector2 position = dragOrigin + (current.mousePosition - pointerOrigin) / viewport.Zoom;
                    if (draggingStart)
                    {
                        tree.StartPosition = position;
                        tree.HasStartPosition = true;
                    }
                    else
                    {
                        dragged.Position = position;
                        dragged.HasPosition = true;
                    }
                    EditorUtility.SetDirty(tree);
                    current.Use();
                    repaint();
                }
                else if (current.type == EventType.MouseUp && current.button == 0)
                {
                    dragged = null;
                    draggingStart = false;
                    GUIUtility.hotControl = 0;
                    if (moved)
                        changed();
                    current.Use();
                }
                return;
            }
            if (current.type != EventType.MouseDown || current.button != 0 || current.alt || !string.IsNullOrEmpty(linking))
                return;
            if (viewport.ToScreen(layout.StartRect).Contains(current.mousePosition))
            {
                BeginDrag(null, layout.StartRect.position, repaint);
                return;
            }
            foreach (DialogueNode block in tree.Nodes)
                if (layout.Blocks.TryGetValue(block.Id, out Rect rect)
                    && viewport.ToScreen(new Rect(rect.x, rect.y, rect.width, block.End ? rect.height : 44f)).Contains(current.mousePosition))
                {
                    BeginDrag(block, rect.position, repaint);
                    break;
                }
        }

        /// <summary>Captures the same drag ownership for Start, End and conversation block headers.</summary>
        /// <param name="block">Conversation or End block. Null selects the Start marker.</param>
        /// <param name="position">Current unscaled canvas position.</param>
        /// <param name="repaint">Window repaint request.</param>
        private void BeginDrag(DialogueNode block, Vector2 position, Action repaint)
        {
            // Share drag capture while retaining a separate saved position for the Start marker.
            dragged = block;
            draggingStart = block == null;
            dragOrigin = position;
            pointerOrigin = Event.current.mousePosition;
            moved = false;
            Selected = block?.Id ?? string.Empty;
            SelectedRow = string.Empty;
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            Event.current.Use();
            repaint();
        }

        /// <summary>Completes a dragged arrow on a block header and supports cancelling an unfinished link.</summary>
        /// <param name="tree">Current sheet.</param>
        /// <param name="changed">Connection edit callback.</param>
        /// <param name="repaint">Canvas repaint request.</param>
        private void HandleConnection(DialogueTree tree, Action changed, Action repaint)
        {
            if (string.IsNullOrEmpty(linking))
                return;
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                linking = string.Empty;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0)
            {
                draggingLink = true;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0 && draggingLink)
            {
                bool connected = false;
                foreach (DialogueNode block in tree.Nodes)
                    if (layout.Blocks.TryGetValue(block.Id, out Rect rect)
                        && viewport.ToScreen(new Rect(rect.x, rect.y, rect.width, 44f)).Contains(current.mousePosition))
                    {
                        DialogueRow row = FindRow(tree, linking);
                        if (row != null)
                        {
                            DialogueGraphEdits.Connect(tree, row, block.Id);
                            changed();
                            connected = true;
                        }
                        break;
                    }
                if (!connected && inputBounds.Contains(current.mousePosition))
                {
                    DialogueRow row = FindRow(tree, linking);
                    Vector2 point = viewport.ToTree(current.mousePosition);
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Create Block"), false, () =>
                    {
                        if (row == null)
                            return;
                        DialogueNode block = DialogueGraphEdits.Add(tree, point);
                        DialogueGraphEdits.Connect(tree, row, block.Id);
                        Selected = block.Id;
                        SelectedRow = string.Empty;
                        changed();
                    });
                    menu.AddItem(new GUIContent("Cancel"), false, () => { });
                    menu.ShowAsContext();
                }
                linking = string.Empty;
                draggingLink = false;
                current.Use();
            }
            repaint();
        }

        /// <summary>Draws a clear block header and smaller indented entry cards.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="block">Block to draw.</param>
        /// <param name="rect">Unscaled block rectangle.</param>
        /// <param name="active">Playback highlight key.</param>
        /// <param name="changed">Edit callback.</param>
        /// <param name="readOnly">Whether mutation is disabled.</param>
        private void DrawBlock(DialogueTree tree, DialogueNode block, Rect rect, string active, Action changed, bool readOnly)
        {
            Rect screen = viewport.ToScreen(rect);
            GUI.Box(screen, GUIContent.none, EditorStyles.helpBox);
            if (block.Id == active || block.Id == Selected)
                Handles.DrawSolidRectangleWithOutline(screen, Color.clear, block.Id == active ? new Color(0.45f, 0.72f, 0.48f) : new Color(0.35f, 0.63f, 0.86f));
            Rect header = viewport.ToScreen(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 30f));
            if (GUI.Button(header, new GUIContent(block.Name, "Select or drag this block. During connection, choose it as the destination."), titleStyle))
            {
                if (!readOnly && !string.IsNullOrEmpty(linking))
                {
                    DialogueRow row = FindRow(tree, linking);
                    if (row != null)
                        DialogueGraphEdits.Connect(tree, row, block.Id);
                    linking = string.Empty;
                    changed();
                }
                Selected = block.Id;
                SelectedRow = string.Empty;
            }
            if (block.End)
                return;
            for (int index = 0; index < block.Entries.Length; index++)
            {
                DialogueRow row = block.Entries[index];
                Rect rowRect = DialogueGraphLayout.Row(rect, index);
                Rect child = viewport.ToScreen(rowRect);
                Color previous = GUI.backgroundColor;
                if (SelectedRow == row.Id)
                    GUI.backgroundColor = new Color(0.45f, 0.68f, 0.9f);
                if (GUI.Button(child, GUIContent.none, EditorStyles.helpBox))
                {
                    Selected = block.Id;
                    SelectedRow = row.Id;
                }
                GUI.backgroundColor = previous;
                GUI.Label(viewport.ToScreen(new Rect(rowRect.x + 9f, rowRect.y + 5f, rowRect.width - 20f, 22f)), row.Name, captionStyle);
                GUI.Label(viewport.ToScreen(new Rect(rowRect.x + 9f, rowRect.y + 28f, rowRect.width - 20f, 22f)), TriggerLabel(row), captionStyle);
                // A thin parent connector makes each row's ownership clear without extra labels.
                Vector3 parent = viewport.ToScreen(new Rect(rect.x + 14f, rect.y + 36f, 0f, 0f)).position;
                Vector3 corner = viewport.ToScreen(new Rect(rect.x + 14f, rowRect.center.y, 0f, 0f)).position;
                Handles.color = new Color(0.5f, 0.5f, 0.5f);
                Handles.DrawAAPolyLine(parent, corner, new Vector3(child.x, child.center.y));
                Rect port = viewport.ToScreen(new Rect(rect.xMax - 8f, rowRect.center.y - 8f, 16f, 16f));
                GUI.Box(port, new GUIContent("●", "Drag to a block header, or click this output and then the destination header."), EditorStyles.miniButton);
                if (!readOnly && Event.current.type == EventType.MouseDown && Event.current.button == 0 && port.Contains(Event.current.mousePosition))
                {
                    linking = row.Id;
                    draggingLink = false;
                    Event.current.Use();
                }
            }
            if (!readOnly && GUI.Button(viewport.ToScreen(new Rect(rect.x + 24f, rect.yMax - 30f, rect.width - 36f, 24f)), new GUIContent("+ Add Entry", "Add another entry directly inside this block."), EditorStyles.miniButton))
            {
                Selected = block.Id;
                SelectedRow = DialogueGraphEdits.AddEntry(tree, block).Id;
                changed();
            }
        }

        /// <summary>Draws all links behind the block cards, routing returns outside their columns.</summary>
        /// <param name="tree">Current sheet.</param>
        private void DrawConnections(DialogueTree tree)
        {
            foreach (DialogueNode block in tree.Nodes)
                if (block != null && !block.End && layout.Blocks.TryGetValue(block.Id, out Rect origin))
                    for (int index = 0; index < block.Entries.Length; index++)
                    {
                        DialogueRow row = block.Entries[index];
                        if (row.Id == linking)
                        {
                            Vector3 port = viewport.ToScreen(new Rect(origin.xMax, DialogueGraphLayout.Row(origin, index).center.y, 0f, 0f)).position;
                            Handles.DrawBezier(port, Event.current.mousePosition, port + Vector3.right * 60f,
                                (Vector3)Event.current.mousePosition - Vector3.right * 60f, new Color(0.4f, 0.7f, 0.95f), null, 2f);
                        }
                        if (row.Next == block.Id || !layout.Blocks.TryGetValue(row.Next, out Rect destination))
                            continue;
                        Vector3 from = viewport.ToScreen(new Rect(origin.xMax, DialogueGraphLayout.Row(origin, index).center.y, 0f, 0f)).position;
                        Vector3 to = viewport.ToScreen(new Rect(destination.x, destination.y + 22f, 0f, 0f)).position;
                        float bend = Mathf.Max(30f, Mathf.Abs(to.x - from.x) * 0.45f);
                        Color color = row.Id == SelectedRow ? new Color(0.4f, 0.7f, 0.95f) : new Color(0.5f, 0.5f, 0.5f);
                        if (destination.x <= origin.x)
                            to = viewport.ToScreen(new Rect(destination.xMax, destination.y + 20f, 0f, 0f)).position;
                        Vector3 endTangent = destination.x <= origin.x ? to + Vector3.right * bend : to - Vector3.right * bend;
                        Handles.DrawBezier(from, to, from + Vector3.right * bend, endTangent, color, null, 2f);
                        Vector3 direction = (to - endTangent).normalized;
                        Vector3 side = new Vector3(-direction.y, direction.x) * 4f;
                        Handles.color = color;
                        Handles.DrawAAConvexPolygon(to, to - direction * 9f + side, to - direction * 9f - side);
                    }
        }

        /// <summary>Draws the sheet entry point independently of conversation block contents.</summary>
        /// <param name="tree">Current sheet.</param>
        private void DrawStart(DialogueTree tree)
        {
            Rect start = viewport.ToScreen(layout.StartRect);
            GUI.Box(start, new GUIContent("Start", "Conversation entry point. Drag to move it."), EditorStyles.helpBox);
            if (!layout.Blocks.TryGetValue(tree.Start, out Rect block))
                return;
            Vector3 to = viewport.ToScreen(new Rect(block.x, block.y + 22f, 0f, 0f)).position;
            Handles.color = new Color(0.5f, 0.5f, 0.5f);
            Handles.DrawAAPolyLine(new Vector3(start.xMax, start.center.y), to);
        }

        /// <summary>Finds a row only during a deliberate connection edit.</summary>
        /// <param name="tree">Current sheet.</param>
        /// <param name="id">Stable row key.</param>
        /// <returns>The source row, or null after deletion.</returns>
        private static DialogueRow FindRow(DialogueTree tree, string id)
        {
            foreach (DialogueNode block in tree.Nodes)
                foreach (DialogueRow row in block.Entries)
                    if (row.Id == id)
                        return row;
            return null;
        }

        /// <summary>Builds a short condition label for an entry card.</summary>
        /// <param name="row">Entry whose trigger is displayed.</param>
        /// <returns>A compact label or an explicit missing-trigger prompt.</returns>
        internal static string TriggerLabel(DialogueRow row)
        {
            return row.Trigger == null ? "Choose Trigger" : row.Trigger.Type == DialogueTrigger.Consumption
                ? "Consumption / " + ObjectNames.NicifyVariableName(row.Trigger.Consumption.Result.ToString())
                : ObjectNames.NicifyVariableName(row.Trigger.Type.ToString());
        }

        #endregion

        #endregion
    }
}
