using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits shared entry references without embedding copies of their pages in the interaction.</summary>
    internal static class DialogueEntryControls
    {
        #region Methods
        #region Drawing
        /// <summary>Draws explicit add, remove and reorder controls for the current interaction's entries.</summary>
        /// <param name="settings">Default entry selection on a draft or shared interaction preset.</param>
        internal static void DrawEntries(SerializedProperty settings)
        {
            GameObject root = settings.serializedObject.targetObject switch
            {
                ObjectWorkspace workspace => workspace.Target.Resolve(),
                Component component => component.gameObject,
                _ => null
            };
            OrderSettings orders = root != null ? root.GetComponent<ObjectAvailableOrders>()?.Settings : null;
            SerializedProperty entries = settings.FindPropertyRelative("Entries");
            // References are changed individually; resizing cannot silently discard a whole page sequence.
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                DialogueEntry preset = (DialogueEntry)entry.objectReferenceValue;
                if (StudioArrayGUI.Header(entries, index, new GUIContent(preset != null ? preset.Name : "Entry " + (index + 1), entries.tooltip)))
                    break;
                if (!entry.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                Draw(entry, orders, settings.FindPropertyRelative("Trigger").enumValueIndex == (int)DialogueTrigger.Consumption,
                    settings.FindPropertyRelative("Selection").enumValueIndex == (int)DialogueSelection.WeightedRandom);
            }
            StudioArrayGUI.Add(entries, "Add Dialogue Entry", () => null);
        }

        /// <summary>Assigns, opens or creates a shared entry for the current content draft.</summary>
        /// <param name="property">Entry asset reference on an interaction or graph node.</param>
        /// <param name="orders">Optional authoring catalog context.</param>
        /// <param name="consumption">Whether the consuming interaction evaluates entry receipt filters.</param>
        /// <param name="weighted">Whether entry weights participate in selection.</param>
        internal static void Draw(SerializedProperty property, OrderSettings orders = null, bool consumption = true, bool weighted = true)
        {
            StudioFieldGUI.PropertyField(property);
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
                    if (StudioButton.Draw(new GUIContent("Edit Entry", "Edit the shared entry with its own Apply/Discard transaction.")))
                        DialogueEntryWindow.Open((DialogueEntry)property.objectReferenceValue, orders?.Catalog, consumption, weighted, orders);
                if (!StudioButton.Draw(new GUIContent("New Entry", "Create and assign a reusable page sequence.")))
                    return;
                string path = EditorUtility.SaveFilePanelInProject("New Dialogue Entry", "Dialogue Entry", "asset", "Choose where to save the entry.");
                if (string.IsNullOrEmpty(path))
                    return;
                DialogueEntry entry = ScriptableObject.CreateInstance<DialogueEntry>();
                entry.Name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(entry, AssetDatabase.GenerateUniqueAssetPath(path));
                Undo.RegisterCreatedObjectUndo(entry, "Create Dialogue Entry");
                property.objectReferenceValue = entry;
                DialogueEntryWindow.Open(entry, orders?.Catalog, consumption, weighted, orders);
            }
        }
        #endregion
        #endregion
    }
}
