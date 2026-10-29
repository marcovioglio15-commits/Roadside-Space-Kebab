#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns shared dialogue graphs and spawn-specific overrides in the interaction tool.</summary>
    internal static class DialogueTreeControls
    {
        #region Methods
        #region Drawing
        /// <summary>Draws graph references with independent editing and safe per-element array controls.</summary>
        /// <param name="settings">Detached dialogue configuration.</param>
        internal static void Draw(SerializedProperty settings)
        {
            Tree(settings.FindPropertyRelative("Tree"));
            HoverControls.Field(settings, "UseSpawnTrees");
            if (!settings.FindPropertyRelative("UseSpawnTrees").boolValue)
                return;
            SerializedProperty rules = settings.FindPropertyRelative("SpawnTrees");
            for (int index = 0; index < rules.arraySize; index++)
            {
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Spawn " + (index + 1), rules.tooltip)))
                    break;
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (!rule.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                HoverControls.Field(rule, "Match");
                HoverControls.Field(rule, "Spawn");
                Tree(rule.FindPropertyRelative("Tree"));
            }
            StudioArrayGUI.Add(rules, "Add Spawn Tree", () => new DialogueSpawnTree());
        }

        /// <summary>Shares asset assignment between interaction and subtree node inspectors.</summary>
        /// <param name="property">Graph asset reference.</param>
        /// <param name="orders">Optional order catalogs inherited by a subtree editor.</param>
        internal static void Tree(SerializedProperty property, OrderSettings orders = null)
        {
            if (orders == null && property.serializedObject.targetObject is ObjectWorkspace workspace)
                orders = workspace.Target.Resolve()?.GetComponent<ObjectAvailableOrders>()?.Settings;
            StudioFieldGUI.PropertyField(property);
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
                    if (StudioButton.Draw(new GUIContent("Edit Tree", "Edit this shared graph in its own Apply/Discard session.")))
                        DialogueTreeWindow.Open((DialogueTree)property.objectReferenceValue, orders);
                if (!StudioButton.Draw(new GUIContent("New Tree", "Create and assign a new shared conversation graph.")))
                    return;
                string path = EditorUtility.SaveFilePanelInProject("New Dialogue Tree", "Dialogue Tree", "asset", "Choose a location for the tree.");
                if (string.IsNullOrEmpty(path))
                    return;
                DialogueTree tree = ScriptableObject.CreateInstance<DialogueTree>();
                DialogueNode end = new DialogueNode { Id = System.Guid.NewGuid().ToString("N"), Name = "End", Kind = DialogueNodeKind.End,
                    Position = new Vector2(350f, 100f) };
                DialogueNode entry = new DialogueNode { Id = System.Guid.NewGuid().ToString("N"), Name = "Entry", Next = end.Id,
                    Position = new Vector2(40f, 100f) };
                tree.Start = entry.Id;
                tree.Nodes = new[] { entry, end };
                AssetDatabase.CreateAsset(tree, AssetDatabase.GenerateUniqueAssetPath(path));
                Undo.RegisterCreatedObjectUndo(tree, "Create Dialogue Tree");
                property.objectReferenceValue = tree;
                DialogueTreeWindow.Open(tree, orders);
            }
        }
        #endregion
        #endregion
    }
}
#endif
