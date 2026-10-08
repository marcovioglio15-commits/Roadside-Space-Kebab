using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns reusable entry assets without embedding mutable page arrays inside interactions.</summary>
    internal static class DialogueEntryControls
    {
        #region Methods
        #region Controls

        /// <summary>Edits ordered entry references with explicit add, remove, reorder and asset commands.</summary>
        /// <param name="settings">Dialogue settings retained by a component draft or interaction preset.</param>
        internal static void Draw(SerializedProperty settings)
        {
            SerializedProperty entries = settings.FindPropertyRelative("Entries");
            StudioArrayGUI.Add(entries, "Add Dialogue Entry", () => null);
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                DialogueEntry preset = (DialogueEntry)entry.objectReferenceValue;
                if (StudioArrayGUI.Header(entries, index, new GUIContent(preset != null ? preset.Name : "Entry " + (index + 1), "Reusable dialogue entry preset.")))
                    break;
                if (!entry.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                StudioFieldGUI.PropertyField(entry, new GUIContent("Entry Preset", entries.tooltip));
                preset = (DialogueEntry)entry.objectReferenceValue;
                using (new StudioButton.RowScope())
                {
                    using (new EditorGUI.DisabledScope(preset == null))
                        if (StudioButton.Draw(new GUIContent("Edit Entry", "Edit shared pages and conditions in their own Apply/Discard window.")))
                            Open(preset, settings);
                    if (StudioButton.Draw(new GUIContent("New Entry", "Create and assign a reusable dialogue entry preset.")))
                        Create(entry, settings);
                }
            }
        }

        /// <summary>Opens an entry with the current interaction's active filter and weighting branches.</summary>
        /// <param name="entry">Shared entry asset.</param>
        /// <param name="settings">Consuming interaction settings.</param>
        private static void Open(DialogueEntry entry, SerializedProperty settings)
        {
            GameObject root = settings.serializedObject.targetObject switch
            {
                ObjectWorkspace workspace => workspace.Target.Resolve(),
                Component component => component.gameObject,
                _ => null
            };
            ObjectAvailableOrders orders = root != null ? root.GetComponent<ObjectAvailableOrders>() : null;
            DialogueEntryWindow.Open(entry, orders != null ? orders.Settings.Catalog : null,
                settings.FindPropertyRelative("Trigger").enumValueIndex == (int)DialogueTrigger.Consumption,
                settings.FindPropertyRelative("Selection").enumValueIndex == (int)DialogueSelection.WeightedRandom);
        }

        /// <summary>Creates one empty asset without altering existing entries or applying the main tool's proposal.</summary>
        /// <param name="property">Reference receiving the new preset.</param>
        /// <param name="settings">Interaction providing editing context.</param>
        private static void Create(SerializedProperty property, SerializedProperty settings)
        {
            string path = EditorUtility.SaveFilePanelInProject("New Dialogue Entry", "Dialogue Entry", "asset", "Choose where to save the entry preset.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueEntry asset = ScriptableObject.CreateInstance<DialogueEntry>();
            asset.Name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(asset, "Create Dialogue Entry");
            property.objectReferenceValue = asset;
            Open(asset, settings);
        }

        #endregion
        #endregion
    }
}
