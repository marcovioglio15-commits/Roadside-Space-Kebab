using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Selects complete tree presets and their exact or threshold spawn variants.</summary>
    internal static class DialogueTreeControls
    {
        #region Methods

        #region Drawing

        /// <summary>Draws default and spawn-specific complete conversation sheets.</summary>
        /// <param name="settings">Serialized local Dialogue settings.</param>
        internal static void Draw(SerializedProperty settings)
        {
            Tree(settings.FindPropertyRelative("Tree"));
            HoverControls.Field(settings, "UseSpawnTrees");
            if (!settings.FindPropertyRelative("UseSpawnTrees").boolValue)
                return;
            SerializedProperty rules = settings.FindPropertyRelative("SpawnTrees");
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Spawn Variant " + (index + 1), rules.tooltip)))
                    break;
                if (!rule.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                HoverControls.Field(rule, "Match");
                HoverControls.Field(rule, "Spawn");
                Tree(rule.FindPropertyRelative("Tree"));
            }
            StudioArrayGUI.Add(rules, "Add Spawn Tree", () => new DialogueSpawnTree());
        }

        /// <summary>Assigns, opens or creates a whole-sheet preset.</summary>
        /// <param name="property">Tree reference receiving the selection.</param>
        /// <param name="orders">Optional order context for tree editing.</param>
        internal static void Tree(SerializedProperty property, OrderSettings orders = null)
        {
            StudioFieldGUI.PropertyField(property);
            using StudioButton.RowScope row = new StudioButton.RowScope();
            using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
                if (StudioButton.Draw(new GUIContent("Open Tree", "Edit every block, entry, trigger and connection in this preset.")))
                    DialogueTreeWindow.Open((DialogueTree)property.objectReferenceValue, orders);
            if (!StudioButton.Draw(new GUIContent("New Tree", "Create a separate complete conversation sheet.")))
                return;
            string path = EditorUtility.SaveFilePanelInProject("New Dialogue Tree", "Dialogue Tree", "asset", "Save the complete conversation sheet.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueTree tree = ScriptableObject.CreateInstance<DialogueTree>();
            DialogueGraphEdits.Initialize(tree);
            AssetDatabase.CreateAsset(tree, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(tree, "Create Dialogue Tree");
            property.objectReferenceValue = tree;
            DialogueTreeWindow.Open(tree, orders);
        }

        #endregion

        #endregion
    }
}
