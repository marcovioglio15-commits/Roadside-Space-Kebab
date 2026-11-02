using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits a detached conversation sheet through a movable block canvas and subordinate entry fields.</summary>
    [CustomEditor(typeof(DialogueTree))]
    internal sealed class DialogueTreeEditor : UnityEditor.Editor
    {
        #region State

        private readonly DialogueGraphCanvas canvas = new DialogueGraphCanvas();
        private Vector2 scroll;
        private string warning = string.Empty;
        private string externalWarning = string.Empty;
        private DialogueTree template;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Checks the initial sheet and follows external draft or preset edits.</summary>
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Changed;
            EditorApplication.projectChanged += Changed;
            Changed();
        }

        /// <summary>Removes editor callbacks when the detached proposal is released.</summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Changed;
            EditorApplication.projectChanged -= Changed;
        }

        /// <summary>Refreshes the bottom warning only after a deliberate edit or external change.</summary>
        private void Changed()
        {
            warning = ((DialogueTree)target).TryValidate(out string issue) ? string.Empty : issue;
            Repaint();
        }

        /// <summary>Opens saved sheets in the dedicated tree window.</summary>
        public override void OnInspectorGUI()
        {
            if (StudioButton.Draw(new GUIContent("Open Dialogue Tree", "Edit this complete sheet in its independent Apply/Discard window.")))
                DialogueTreeWindow.Open((DialogueTree)target);
        }

        #endregion

        #region Layout

        /// <summary>Accepts the owning draft window's conflict or Apply message without duplicating labels.</summary>
        /// <param name="message">Current transaction issue, or empty.</param>
        /// <returns>True when the window needs another repaint.</returns>
        internal bool SetExternalWarning(string message)
        {
            if (externalWarning == message)
                return false;
            externalWarning = message;
            return true;
        }

        /// <summary>Draws the editable canvas, contextual block fields and a compact yellow warning.</summary>
        /// <param name="height">Available height above the Apply/Discard footer.</param>
        /// <param name="orders">Optional destination order context.</param>
        internal void Draw(float height, OrderSettings orders)
        {
            DialogueTree tree = (DialogueTree)target;
            float leftWidth = Mathf.Max(280f, EditorGUIUtility.currentViewWidth - 340f);
            Rect graph;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Fit Tree", "Frame every block. Wheel zooms. Middle drag pans. Right click opens block actions."), EditorStyles.toolbarButton))
                    canvas.Frame(tree, new Vector2(leftWidth, height - 26f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Save As Preset", "Save the entire sheet as a separate preset. Entry and trigger references remain shared."), EditorStyles.toolbarButton))
                    Export(tree);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                graph = GUILayoutUtility.GetRect(leftWidth, height - 26f, GUILayout.Width(leftWidth), GUILayout.Height(height - 26f));
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(310f)))
                using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(height - 26f)))
                {
                    scroll = view.scrollPosition;
                    DrawInspector(tree, orders);
                }
            }
            canvas.Draw(graph, tree, null, Changed, Repaint);
            string issue = string.IsNullOrEmpty(externalWarning) ? warning : externalWarning;
            if (!string.IsNullOrEmpty(issue))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.82f, 0.25f);
                GUI.Label(new Rect(graph.x + 10f, graph.yMax - 42f, graph.width - 20f, 36f), issue, EditorStyles.wordWrappedMiniLabel);
                GUI.color = previous;
            }
        }

        /// <summary>Edits block settings and keeps entry fields visually subordinate.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="orders">Optional order menu context.</param>
        private void DrawInspector(DialogueTree tree, OrderSettings orders)
        {
            serializedObject.Update();
            template = (DialogueTree)EditorGUILayout.ObjectField(new GUIContent("Preset", "Optional whole-sheet preset to import into this draft."), template, typeof(DialogueTree), false);
            using (new EditorGUI.DisabledScope(template == null || template == tree))
                if (StudioButton.Draw(new GUIContent("Import Sheet", "Replace this draft's blocks and links. Apply saves; Undo or Discard restores the previous draft.")))
                {
                    Undo.RecordObject(tree, "Import dialogue sheet");
                    tree.Start = template.Start;
                    tree.StartPosition = template.StartPosition;
                    tree.HasStartPosition = template.HasStartPosition;
                    tree.Nodes = Array.ConvertAll(template.Nodes, block => ObjectWorkspace.Copy(block));
                    EditorUtility.SetDirty(tree);
                    serializedObject.Update();
                    Changed();
                }
            DialogueNodeInspector.Connection(serializedObject.FindProperty("Start"), tree, "Start Block");
            int index = Array.FindIndex(tree.Nodes, block => block.Id == canvas.Selected);
            if (index >= 0)
            {
                EditorGUILayout.Space(8f);
                DialogueNodeInspector.Draw(serializedObject.FindProperty("Nodes").GetArrayElementAtIndex(index), tree, canvas.SelectedRow, orders);
            }
            if (serializedObject.ApplyModifiedProperties())
                Changed();
        }

        /// <summary>Saves a complete independent sheet without changing its current source.</summary>
        /// <param name="tree">Draft to copy into a new preset.</param>
        private static void Export(DialogueTree tree)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Dialogue Tree", tree.name + " Copy", "asset", "Save the complete sheet as a dedicated preset.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueTree copy = Instantiate(tree);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(copy, "Save dialogue tree preset");
            AssetDatabase.SaveAssets();
        }

        #endregion

        #endregion
    }
}
