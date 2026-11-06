using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits a selected block and its subordinate entry without exposing connection keys.</summary>
    internal static class DialogueNodeInspector
    {
        #region State

        private static readonly ObjectStudioSections policySections = new ObjectStudioSections();

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Draws block settings and the selected entry row.</summary>
        /// <param name="property">Selected block's serialized data.</param>
        /// <param name="tree">Containing sheet.</param>
        /// <param name="rowId">Optional selected entry key.</param>
        /// <param name="orders">Optional order context.</param>
        internal static void Draw(SerializedProperty property, DialogueTree tree, string rowId, OrderSettings orders)
        {
            HoverControls.Field(property, "Name");
            HoverControls.Field(property, "End");
            if (property.FindPropertyRelative("End").boolValue)
                return;
            HoverControls.Field(property, "Routing", "Routing Block");
            if (!property.FindPropertyRelative("Routing").boolValue)
            {
                HoverControls.Field(property, "OverridePolicy", "Override Dialogue Settings");
                if (property.FindPropertyRelative("OverridePolicy").boolValue)
                    ExtendedInteractionControls.DrawDialogue(property.FindPropertyRelative("Policy"), policySections, false);
            }
            HoverControls.Field(property, "Selection");
            SerializedProperty entries = property.FindPropertyRelative("Entries");
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
            using StudioIndentScope indent = new StudioIndentScope();
            // Every row stays inside its block, with explicit add/remove/reorder actions.
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty row = entries.GetArrayElementAtIndex(index);
                if (row.FindPropertyRelative("Id").stringValue == rowId)
                    row.isExpanded = true;
                if (StudioArrayGUI.Header(entries, index, new GUIContent(row.FindPropertyRelative("Name").stringValue, entries.tooltip)))
                    break;
                if (!row.isExpanded)
                    continue;
                using StudioIndentScope rowIndent = new StudioIndentScope();
                HoverControls.Field(row, "Name");
                if (!property.FindPropertyRelative("Routing").boolValue)
                    DialogueEntryControls.Draw(row.FindPropertyRelative("Entry"));
                Trigger(row.FindPropertyRelative("Trigger"), orders);
                if (property.FindPropertyRelative("Selection").enumValueIndex == (int)DialogueSelection.WeightedRandom)
                    HoverControls.Field(row, "Weight");
                Connection(row.FindPropertyRelative("Next"), tree, "After Final Page");
            }
        }

        /// <summary>Chooses a destination by block title rather than by a typed key.</summary>
        /// <param name="property">Stable destination key.</param>
        /// <param name="tree">Sheet supplying block choices.</param>
        /// <param name="label">Contextual caption.</param>
        internal static void Connection(SerializedProperty property, DialogueTree tree, string label)
        {
            string[] names = new string[tree.Nodes.Length + 1];
            names[0] = "Choose Block";
            int selected = 0;
            for (int index = 0; index < tree.Nodes.Length; index++)
            {
                names[index + 1] = tree.Nodes[index].Name;
                if (tree.Nodes[index].Id == property.stringValue)
                    selected = index + 1;
            }
            EditorGUI.BeginChangeCheck();
            selected = StudioFieldGUI.Popup(new GUIContent(label, property.tooltip), selected, names);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = selected == 0 ? string.Empty : tree.Nodes[selected - 1].Id;
        }

        /// <summary>Assigns and independently edits a reusable trigger preset.</summary>
        /// <param name="property">Row's trigger reference.</param>
        /// <param name="orders">Optional destination order context.</param>
        private static void Trigger(SerializedProperty property, OrderSettings orders)
        {
            StudioFieldGUI.PropertyField(property);
            using StudioButton.RowScope buttons = new StudioButton.RowScope();
            using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
            {
                if (StudioButton.Draw(new GUIContent("Open Trigger", "Edit this shared trigger independently of the tree.")))
                    DialogueTriggerWindow.Open((DialogueTriggerPreset)property.objectReferenceValue, orders);
                if (StudioButton.Draw(new GUIContent("Make Copy", "Create and select a separate trigger preset.")))
                {
                    DialogueTriggerPreset copy = ObjectPresetCopy.Save((DialogueTriggerPreset)property.objectReferenceValue);
                    if (copy != null)
                        property.objectReferenceValue = copy;
                }
            }
            if (!StudioButton.Draw(new GUIContent("New Trigger", "Create a reusable trigger for this entry.")))
                return;
            string path = EditorUtility.SaveFilePanelInProject("New Trigger", "Dialogue Trigger", "asset", "Save the trigger preset.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueTriggerPreset trigger = ScriptableObject.CreateInstance<DialogueTriggerPreset>();
            AssetDatabase.CreateAsset(trigger, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(trigger, "Create Dialogue Trigger");
            property.objectReferenceValue = trigger;
            DialogueTriggerWindow.Open(trigger, orders);
        }

        #endregion

        #endregion
    }
}
