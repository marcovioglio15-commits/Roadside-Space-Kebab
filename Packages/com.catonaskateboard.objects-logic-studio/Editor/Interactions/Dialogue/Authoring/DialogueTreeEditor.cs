#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using System;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Combines graph navigation, selected-node editing and a side-effect-free runner preview.</summary>
    [CustomEditor(typeof(DialogueTree))]
    internal sealed class DialogueTreeEditor : UnityEditor.Editor
    {
        #region Fields
        [Header("Graph View")]
        [Tooltip("Transient canvas navigation and selected node.")]
        [SerializeField]
        private DialogueGraphCanvas canvas = new DialogueGraphCanvas();
        [Tooltip("Identity flags supplied only to the graph simulation.")]
        [SerializeField]
        private ObjectFlag[] previewFlags = Array.Empty<ObjectFlag>();
        private Vector2 scroll;
        private Vector2 previewScroll;
        private bool frame = true;
        private bool previewVisible;
        private bool receiptMatches = true;
        private int spawn = 1;
        private string search = string.Empty;
        private string warning = string.Empty;
        private DialogueRunner preview;
        private OrderCatalog sampleCatalog;
        private DialogueTree template;
        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Repaints after Undo and drops simulations referencing externally changed dependencies.</summary>
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Changed;
            EditorApplication.projectChanged += Changed;
        }

        /// <summary>Releases editor callbacks when its detached proposal is replaced.</summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Changed;
            EditorApplication.projectChanged -= Changed;
        }

        /// <summary>Invalidates only the transient simulation, never the authored graph.</summary>
        private void Changed()
        {
            preview = null;
            warning = string.Empty;
            Repaint();
        }

        /// <summary>Routes persistent assets into a dedicated transaction instead of editing them inline.</summary>
        public override void OnInspectorGUI()
        {
            if (StudioButton.Draw(new GUIContent("Edit Dialogue Tree", "Open the graph and its independent Apply/Discard session.")))
                DialogueTreeWindow.Open((DialogueTree)target);
        }
        #endregion
        #region Layout
        /// <summary>Draws a fixed canvas beside a scrollable inspector and optional simulation strip.</summary>
        /// <param name="height">Available viewport height, excluding the transaction footer.</param>
        /// <param name="orders">Optional destination catalogs used only by authoring controls.</param>
        internal void Draw(float height, OrderSettings orders)
        {
            DialogueTree tree = (DialogueTree)target;
            Rect area = GUILayoutUtility.GetRect(100f, height, GUILayout.ExpandWidth(true));
            Rect viewport = new Rect(area.x, area.y + 28f, Mathf.Max(300f, area.width - 335f), area.height - 28f - (previewVisible ? 185f : 0f));
            if (frame)
            {
                canvas.Frame(tree, viewport.size);
                frame = false;
            }
            GUI.BeginGroup(area);
            GUILayout.BeginArea(new Rect(0f, 0f, area.width, 25f), EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Node", "Add an operation at the viewport centre."), EditorStyles.toolbarDropDown, GUILayout.Width(85f)))
                    canvas.AddMenu(tree, (viewport.size * 0.5f - canvas.Pan) / canvas.Zoom, Changed);
                if (GUILayout.Button(new GUIContent("Frame All", "Fit every node in the viewport."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    canvas.Frame(tree, viewport.size);
                if (GUILayout.Button(new GUIContent("Frame Node", "Focus the selected node."), EditorStyles.toolbarButton, GUILayout.Width(85f)))
                    canvas.Frame(tree, viewport.size, true);
                EditorGUI.BeginChangeCheck();
                using (new ObjectStudioFieldLayout(50f))
                    search = EditorGUILayout.TextField(new GUIContent("Search", "Find a node by its title."), search, GUILayout.Width(210f));
                if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(search))
                {
                    DialogueNode found = Array.Find(tree.Nodes, node => node.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (found != null)
                    {
                        canvas.Selected = found.Id;
                        canvas.Frame(tree, viewport.size, true);
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Validate", "Check this graph and all subtree dependencies."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    warning = tree.TryValidate(out string issue, orders) ? "Graph valid" : issue;
                previewVisible = GUILayout.Toggle(previewVisible, new GUIContent("Preview", "Simulate this draft without changing scene objects."), EditorStyles.toolbarButton, GUILayout.Width(65f));
                if (GUILayout.Button(new GUIContent("Export", "Save an independent tree preset from this proposal."), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    Export(tree);
            }
            GUILayout.EndArea();
            GUI.EndGroup();
            canvas.Draw(viewport, tree, preview?.CurrentTree == tree ? preview.CurrentNode : null, Repaint, Changed);
            GUILayout.BeginArea(new Rect(viewport.xMax + 8f, area.y + 28f, 320f, viewport.height));
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            using (new ObjectStudioFieldLayout(130f))
            {
                scroll = view.scrollPosition;
                serializedObject.Update();
                template = (DialogueTree)EditorGUILayout.ObjectField(new GUIContent("Template", "Optional tree preset to copy into this draft. Shared entries and subtrees remain referenced."), template, typeof(DialogueTree), false);
                using (new EditorGUI.DisabledScope(template == null || template == tree))
                    if (StudioButton.Draw(new GUIContent("Import", "Replace this draft with the template. Apply commits the change; Undo or Discard restores the previous graph.")))
                    {
                        Undo.RecordObject(tree, "Import Dialogue Tree");
                        tree.Start = template.Start;
                        tree.Nodes = Array.ConvertAll(template.Nodes, node => ObjectWorkspace.Copy(node));
                        EditorUtility.SetDirty(tree);
                        serializedObject.Update();
                        canvas.Selected = string.Empty;
                        frame = true;
                        Changed();
                    }
                DialogueNodeInspector.Connection(serializedObject.FindProperty("Start"), tree, "Start");
                if (orders == null)
                    sampleCatalog = (OrderCatalog)EditorGUILayout.ObjectField(new GUIContent("Order Catalog", "Optional catalog used only to select condition and entry order names."), sampleCatalog, typeof(OrderCatalog), false);
                int index = Array.FindIndex(tree.Nodes, node => node.Id == canvas.Selected);
                if (index >= 0)
                {
                    EditorGUILayout.Space(8f);
                    DialogueNodeInspector.Draw(serializedObject.FindProperty("Nodes").GetArrayElementAtIndex(index), tree,
                        orders ?? (sampleCatalog != null ? new OrderSettings { Catalog = sampleCatalog } : null));
                }
                if (serializedObject.ApplyModifiedProperties())
                    Changed();
                if (!string.IsNullOrEmpty(warning))
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            }
            GUILayout.EndArea();
            if (previewVisible)
                Preview(new Rect(area.x, viewport.yMax + 5f, area.width, 178f), tree);
        }
        #endregion
        #region Simulation
        /// <summary>Runs the actual graph engine with explicit simulated identity and receipt values.</summary>
        /// <param name="rect">Reserved simulation strip.</param>
        /// <param name="tree">Detached graph proposal.</param>
        private void Preview(Rect rect, DialogueTree tree)
        {
            GUILayout.BeginArea(rect);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new ObjectStudioFieldLayout(60f))
                    spawn = EditorGUILayout.IntField(new GUIContent("Spawn", "Simulated one-based occurrence."), spawn, GUILayout.Width(105f));
                receiptMatches = GUILayout.Toggle(receiptMatches, new GUIContent("Receipts Match", "Result supplied to enabled consumption predicates in this simulation."), EditorStyles.toolbarButton, GUILayout.Width(110f));
                if (GUILayout.Button(new GUIContent("Start", "Start with a fixed preview seed and the selected context."), EditorStyles.toolbarButton, GUILayout.Width(55f)))
                {
                    if (spawn > 0 && tree.TryValidate(out warning))
                    {
                        preview = new DialogueRunner(tree);
                        preview.Start(DialogueContext.Simulate(tree, spawn, previewFlags, receiptMatches));
                    }
                    else if (spawn <= 0)
                        warning = "Preview Spawn must be positive.";
                }
                using (new EditorGUI.DisabledScope(preview == null))
                {
                    if (GUILayout.Button(new GUIContent("Advance", "Advance one page through the same runner used in Play."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        preview.Advance();
                    if (GUILayout.Button(new GUIContent("+1 s", "Advance one second of a pending Wait."), EditorStyles.toolbarButton, GUILayout.Width(50f)))
                        preview.Tick(1f);
                    if (GUILayout.Button(new GUIContent("Stop", "Release the current simulation."), EditorStyles.toolbarButton, GUILayout.Width(45f)))
                        preview = null;
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(preview != null ? preview.State.ToString() : "Idle", EditorStyles.miniLabel);
            }
            using SerializedObject view = new SerializedObject(this);
            ObjectFlagSelector.Draw(view.FindProperty("previewFlags"), new GUIContent("Owner Flags", "Identity flags supplied to the simulation only."));
            view.ApplyModifiedProperties();
            using (EditorGUILayout.ScrollViewScope content = new EditorGUILayout.ScrollViewScope(previewScroll))
            {
                previewScroll = content.scrollPosition;
                if (preview?.Line != null)
                {
                    EditorGUILayout.LabelField(preview.Line.Speaker, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(preview.Line.Text, EditorStyles.wordWrappedLabel);
                }
                else if (preview?.State == DialogueRunState.Failed)
                    EditorGUILayout.LabelField(preview.Error, EditorStyles.wordWrappedLabel);
            }
            GUILayout.EndArea();
        }

        /// <summary>Creates a portable independent graph while retaining its shared content dependencies.</summary>
        /// <param name="tree">Proposal to export.</param>
        private void Export(DialogueTree tree)
        {
            if (!tree.TryValidate(out warning))
                return;
            string path = EditorUtility.SaveFilePanelInProject("Export Dialogue Tree", tree.name, "asset", "Choose the new tree preset location.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueTree copy = Instantiate(tree);
            copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(copy, "Export Dialogue Tree");
        }
        #endregion
        #endregion
    }
}
#endif
