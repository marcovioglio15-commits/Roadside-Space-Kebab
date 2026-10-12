#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws and edits a bounded graph canvas with native Undo, links, pan and zoom.</summary>
    [Serializable]
    internal sealed class DialogueGraphCanvas
    {
        #region Fields
        [Header("View")]
        [Tooltip("Selected node key retained independently of its title.")]
        public string Selected = string.Empty;
        [Tooltip("Canvas pan in screen pixels.")]
        public Vector2 Pan = new Vector2(30f, 30f);
        [Tooltip("Canvas scale used only by the editor viewport.")]
        public float Zoom = 0.8f;
        private string moving;
        private string linking;
        private int port;
        private bool panning;
        private int dragGroup;
        private const float Width = 210f;
        private const float Height = 112f;
        #endregion
        #region Methods
        #region View
        /// <summary>Draws links behind stable-size nodes and processes only viewport-local pointer events.</summary>
        /// <param name="rect">Available editor viewport.</param>
        /// <param name="tree">Detached graph proposal.</param>
        /// <param name="active">Optional active preview node in this tree.</param>
        /// <param name="repaint">Requests another frame after navigation or editing.</param>
        /// <param name="changed">Invalidates simulations after a graph edit.</param>
        internal void Draw(Rect rect, DialogueTree tree, string active, Action repaint, Action changed)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.125f, 0.13f));
            GUI.BeginGroup(rect);
            Rect local = new Rect(Vector2.zero, rect.size);
            Grid(local);
            // Draw connections first so node labels stay readable above crossing wires.
            foreach (DialogueNode node in tree.Nodes)
            {
                if (node.Kind is DialogueNodeKind.Entry or DialogueNodeKind.Wait or DialogueNodeKind.Subtree or DialogueNodeKind.Condition)
                    Link(tree, node, node.Next, -1);
                if (node.Kind is DialogueNodeKind.Condition or DialogueNodeKind.Select)
                    Link(tree, node, node.Otherwise, -2);
                if (node.Kind == DialogueNodeKind.Select)
                    foreach (DialogueBranch branch in node.Branches)
                        Link(tree, node, branch.Target, 0);
            }
            foreach (DialogueNode node in tree.Nodes)
                Node(node, tree.Start == node.Id, node.Id == active);
            if (!string.IsNullOrEmpty(linking))
            {
                DialogueNode source = Array.Find(tree.Nodes, candidate => candidate.Id == linking);
                if (source != null)
                    Wire(Output(source, port), Event.current.mousePosition, Color.yellow);
            }
            Handle(local, tree, repaint, changed);
            GUI.EndGroup();
        }

        /// <summary>Frames all nodes or the current selection without changing any authored coordinates.</summary>
        /// <param name="tree">Graph to frame.</param>
        /// <param name="size">Viewport dimensions.</param>
        /// <param name="selection">Whether to focus only the selected node.</param>
        internal void Frame(DialogueTree tree, Vector2 size, bool selection = false)
        {
            if (tree.Nodes.Length == 0)
                return;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (DialogueNode node in tree.Nodes)
                if (!selection || node.Id == Selected)
                {
                    minimum = Vector2.Min(minimum, node.Position);
                    maximum = Vector2.Max(maximum, node.Position + new Vector2(Width, Height));
                }
            if (!float.IsFinite(minimum.x))
                return;
            Zoom = Mathf.Clamp(Mathf.Min((size.x - 60f) / (maximum.x - minimum.x), (size.y - 60f) / (maximum.y - minimum.y)), 0.12f, 1.25f);
            Pan = size * 0.5f - (minimum + maximum) * 0.5f * Zoom;
        }

        /// <summary>Offers supported node types at a chosen graph position.</summary>
        /// <param name="tree">Proposal receiving the new node.</param>
        /// <param name="point">Unscaled insertion position.</param>
        /// <param name="repaint">Refresh after the menu action.</param>
        internal void AddMenu(DialogueTree tree, Vector2 point, Action repaint)
        {
            GenericMenu menu = new GenericMenu();
            foreach (DialogueNodeKind kind in Enum.GetValues(typeof(DialogueNodeKind)))
                menu.AddItem(new GUIContent(kind.ToString()), false, () =>
                {
                    Selected = DialogueGraphEdits.Add(tree, kind, point).Id;
                    repaint();
                });
            menu.ShowAsContext();
        }
        #endregion
        #region Drawing
        /// <summary>Draws a restrained coordinate grid independent of authored scene geometry.</summary>
        /// <param name="rect">Viewport-local bounds.</param>
        private void Grid(Rect rect)
        {
            float step = Mathf.Max(16f, 40f * Zoom);
            Color color = new Color(1f, 1f, 1f, 0.035f);
            for (float x = Pan.x % step; x < rect.width; x += step)
                EditorGUI.DrawRect(new Rect(x, 0f, 1f, rect.height), color);
            for (float y = Pan.y % step; y < rect.height; y += step)
                EditorGUI.DrawRect(new Rect(0f, y, rect.width, 1f), color);
        }

        /// <summary>Draws a compact operation using clipped labels and distinct entry/exit handles.</summary>
        /// <param name="node">Operation to display.</param>
        /// <param name="start">Whether this is the tree's entry point.</param>
        /// <param name="active">Whether the preview is executing this node.</param>
        private void Node(DialogueNode node, bool start, bool active)
        {
            Rect rect = Bounds(node);
            EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f),
                active ? new Color(0.35f, 0.8f, 0.45f) : node.Id == Selected ? new Color(0.3f, 0.65f, 0.85f) : Color.black);
            EditorGUI.DrawRect(rect, new Color(0.23f, 0.24f, 0.25f));
            if (Zoom >= 0.45f)
            {
                GUI.Label(new Rect(rect.x + 9f, rect.y + 5f, rect.width - 18f, 20f),
                    new GUIContent((start ? "> " : string.Empty) + node.Name, node.Name), EditorStyles.boldLabel);
                string detail = node.Kind switch
                {
                    DialogueNodeKind.Entry => node.Entry != null ? node.Entry.Name : "No Entry",
                    DialogueNodeKind.Select => node.Branches.Length + " branches / " + node.Selection,
                    DialogueNodeKind.Subtree => node.Subtree != null ? node.Subtree.name : "No Subtree",
                    DialogueNodeKind.Wait => node.Duration + " s",
                    _ => node.Kind.ToString()
                };
                GUI.Label(new Rect(rect.x + 9f, rect.y + 25f, rect.width - 18f, 20f), new GUIContent(detail, detail), EditorStyles.miniLabel);
            }
            Port(new Vector2(rect.x, rect.center.y), "Connect an output to this input.");
            if (node.Kind is DialogueNodeKind.Entry or DialogueNodeKind.Wait or DialogueNodeKind.Subtree or DialogueNodeKind.Condition)
                Port(Output(node, -1), node.Kind == DialogueNodeKind.Condition ? "True" : "Next");
            if (node.Kind is DialogueNodeKind.Select or DialogueNodeKind.Condition)
                Port(Output(node, -2), node.Kind == DialogueNodeKind.Condition ? "False" : "Fallback");
            if (node.Kind == DialogueNodeKind.Select)
                Port(Output(node, 0), "Choose a branch to connect.");
        }

        /// <summary>Draws one tooltip-bearing connection handle.</summary>
        /// <param name="point">Port centre.</param>
        /// <param name="tooltip">Meaning of the port.</param>
        private static void Port(Vector2 point, string tooltip)
        {
            Rect rect = new Rect(point.x - 5f, point.y - 5f, 10f, 10f);
            EditorGUI.DrawRect(rect, new Color(0.75f, 0.8f, 0.84f));
            GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        /// <summary>Draws a resolved connection while leaving missing outputs visible only as ports.</summary>
        /// <param name="tree">Graph resolving the target key.</param>
        /// <param name="source">Output owner.</param>
        /// <param name="target">Destination key.</param>
        /// <param name="output">Output slot.</param>
        private void Link(DialogueTree tree, DialogueNode source, string target, int output)
        {
            DialogueNode destination = Array.Find(tree.Nodes, candidate => candidate.Id == target);
            if (destination == null)
                return;
            Rect rect = Bounds(destination);
            Wire(Output(source, output), new Vector2(rect.x, rect.center.y), new Color(0.48f, 0.62f, 0.7f));
        }

        /// <summary>Draws a flow wire without editing either endpoint.</summary>
        /// <param name="start">Output centre.</param>
        /// <param name="end">Input centre.</param>
        /// <param name="color">Connection tint.</param>
        private static void Wire(Vector2 start, Vector2 end, Color color)
        {
            Handles.DrawBezier(start, end, start + Vector2.right * 65f, end - Vector2.right * 65f, color, null, 2f);
        }

        /// <summary>Maps an operation to stable viewport bounds.</summary>
        /// <param name="node">Authored node.</param>
        /// <returns>Screen-space node rectangle.</returns>
        private Rect Bounds(DialogueNode node) => new Rect(Pan + node.Position * Zoom, new Vector2(Width, Height) * Zoom);

        /// <summary>Places compact branch/fallback ports without resizing Select nodes for large catalogs.</summary>
        /// <param name="node">Output owner.</param>
        /// <param name="output">Output slot.</param>
        /// <returns>The port centre in viewport coordinates.</returns>
        private Vector2 Output(DialogueNode node, int output)
        {
            Rect rect = Bounds(node);
            return new Vector2(rect.xMax, rect.y + rect.height * (output == -2 ? 0.82f : 0.55f));
        }
        #endregion
        #region Input
        /// <summary>Handles graph navigation, node drag, output linking and contextual edits.</summary>
        /// <param name="rect">Viewport-local hit region.</param>
        /// <param name="tree">Detached graph proposal.</param>
        /// <param name="repaint">Refresh callback.</param>
        /// <param name="changed">Invalidates simulations after topology or position changes.</param>
        private void Handle(Rect rect, DialogueTree tree, Action repaint, Action changed)
        {
            Event current = Event.current;
            Vector2 mouse = current.mousePosition;
            if (current.type == EventType.MouseUp)
            {
                if (!string.IsNullOrEmpty(moving))
                    Undo.CollapseUndoOperations(dragGroup);
                moving = null;
                panning = false;
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                linking = null;
                current.Use();
            }
            if (!rect.Contains(mouse))
                return;
            if (current.type == EventType.ScrollWheel)
            {
                float previous = Zoom;
                Zoom = Mathf.Clamp(Zoom * Mathf.Exp(-current.delta.y * 0.07f), 0.12f, 1.5f);
                Pan = mouse - (mouse - Pan) * (Zoom / previous);
                current.Use();
            }
            if (current.type == EventType.MouseDown && current.button == 2)
            {
                panning = true;
                current.Use();
            }
            if (current.type == EventType.MouseDrag && panning)
            {
                Pan += current.delta;
                current.Use();
            }
            DialogueNode hit = Array.FindLast(tree.Nodes, candidate => Expanded(Bounds(candidate)).Contains(mouse));
            if (current.type == EventType.ContextClick)
            {
                if (hit == null)
                    AddMenu(tree, (mouse - Pan) / Zoom, changed);
                else
                    Menu(tree, hit, changed);
                current.Use();
            }
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (hit != null)
                {
                    Selected = hit.Id;
                    if (!string.IsNullOrEmpty(linking) && mouse.x <= Bounds(hit).x + 16f)
                    {
                        DialogueGraphEdits.Connect(tree, Array.Find(tree.Nodes, candidate => candidate.Id == linking), port, hit.Id);
                        linking = null;
                        changed();
                    }
                    else if (mouse.x >= Bounds(hit).xMax - 10f && hit.Kind != DialogueNodeKind.End)
                        LinkMenu(tree, hit, mouse, repaint);
                    else
                    {
                        Undo.IncrementCurrentGroup();
                        dragGroup = Undo.GetCurrentGroup();
                        moving = hit.Id;
                    }
                }
                else
                    Selected = string.Empty;
                GUI.FocusControl(null);
                current.Use();
            }
            if (current.type == EventType.MouseDrag && !string.IsNullOrEmpty(moving))
            {
                DialogueNode node = Array.Find(tree.Nodes, candidate => candidate.Id == moving);
                if (node != null)
                {
                    Undo.RecordObject(tree, "Move Dialogue Node");
                    node.Position += current.delta / Zoom;
                }
                EditorUtility.SetDirty(tree);
                changed();
                current.Use();
            }
            if (current.type == EventType.Used || !string.IsNullOrEmpty(linking))
                repaint();
        }

        /// <summary>Includes connection handles in node hit testing.</summary>
        /// <param name="rect">Node bounds.</param>
        /// <returns>Expanded pointer bounds.</returns>
        private static Rect Expanded(Rect rect) => new Rect(rect.x - 7f, rect.y, rect.width + 14f, rect.height);

        /// <summary>Selects an output, offering named branches when their compact port is used.</summary>
        /// <param name="tree">Graph proposal.</param>
        /// <param name="node">Source node.</param>
        /// <param name="mouse">Pointer position.</param>
        /// <param name="repaint">Refresh after selection.</param>
        private void LinkMenu(DialogueTree tree, DialogueNode node, Vector2 mouse, Action repaint)
        {
            linking = node.Id;
            port = node.Kind is DialogueNodeKind.Condition or DialogueNodeKind.Select
                && mouse.y > Bounds(node).y + Bounds(node).height * 0.7f ? -2 : -1;
            if (node.Kind != DialogueNodeKind.Select || port == -2)
                return;
            linking = null;
            GenericMenu menu = new GenericMenu();
            for (int index = 0; index < node.Branches.Length; index++)
            {
                int branch = index;
                menu.AddItem(new GUIContent((index + 1) + ". " + node.Branches[index].Name), false, () =>
                {
                    linking = node.Id;
                    port = branch;
                    repaint();
                });
            }
            if (node.Branches.Length == 0)
                menu.AddDisabledItem(new GUIContent("No Branches"));
            menu.ShowAsContext();
        }

        /// <summary>Offers explicit node operations without mutating entry or subtree assets.</summary>
        /// <param name="tree">Detached graph.</param>
        /// <param name="node">Selected operation.</param>
        /// <param name="repaint">Refresh after mutation.</param>
        private void Menu(DialogueTree tree, DialogueNode node, Action repaint)
        {
            Selected = node.Id;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Set Start"), tree.Start == node.Id, () =>
            {
                Undo.RecordObject(tree, "Set Dialogue Start");
                tree.Start = node.Id;
                EditorUtility.SetDirty(tree);
                repaint();
            });
            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                Selected = DialogueGraphEdits.Duplicate(tree, node).Id;
                repaint();
            });
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                DialogueGraphEdits.Remove(tree, node);
                Selected = string.Empty;
                repaint();
            });
            menu.ShowAsContext();
        }
        #endregion
        #endregion
    }
}
#endif
