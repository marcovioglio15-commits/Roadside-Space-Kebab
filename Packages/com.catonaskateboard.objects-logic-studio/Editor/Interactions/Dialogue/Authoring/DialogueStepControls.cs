using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Offers whole-tree or entry checkpoint completion through named preset menus.</summary>
    internal static class DialogueStepControls
    {
        #region Methods

        #region Drawing

        /// <summary>Draws a checkpoint selector only for an actual dialogue source.</summary>
        /// <param name="property">Serialized step descriptor.</param>
        /// <param name="source">Selected completion source.</param>
        internal static void Draw(SerializedProperty property, ObjectInteraction source)
        {
            if (property == null || source is not ObjectDialogue dialogue)
                return;
            DialogueStepReference selected = (DialogueStepReference)property.boxedValue;
            DrawChoice(property, dialogue, selected, -1);
        }

        /// <summary>Offers a checkpoint for one source without resizing stored data during repaint.</summary>
        /// <param name="steps">Optional parallel checkpoint array.</param>
        /// <param name="index">Completion source index.</param>
        /// <param name="source">Selected source component.</param>
        internal static void DrawArray(SerializedProperty steps, int index, ObjectInteraction source)
        {
            if (source is not ObjectDialogue dialogue)
                return;
            DialogueStepReference selected = index < steps.arraySize ? (DialogueStepReference)steps.GetArrayElementAtIndex(index).boxedValue : null;
            DrawChoice(steps, dialogue, selected, index);
        }

        /// <summary>Builds the same named menu for direct and array-backed checkpoint selections.</summary>
        /// <param name="property">Descriptor or array receiving the explicit choice.</param>
        /// <param name="dialogue">Dialogue supplying presets.</param>
        /// <param name="selected">Current optional checkpoint.</param>
        /// <param name="index">Array index, or minus one for a direct descriptor.</param>
        private static void DrawChoice(SerializedProperty property, ObjectDialogue dialogue, DialogueStepReference selected, int index)
        {
            string label = Label(dialogue.Settings.Tree, selected);
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent("Dialogue Step", property.tooltip));
            Rect rect = field.GetRect(EditorGUIUtility.singleLineHeight);
            rect = field.ValueRect(rect);
            if (!GUI.Button(rect, new GUIContent(label, "Choose the full tree or an entry checkpoint."), EditorStyles.popup))
                return;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            Add(menu, "Whole Tree", selected, new DialogueStepReference(), owner, path, guard, index);
            AddTree(menu, dialogue.Settings.Tree, selected, owner, path, guard, index);
            if (dialogue.Settings.UseSpawnTrees)
                foreach (DialogueSpawnTree rule in dialogue.Settings.SpawnTrees)
                    if (rule?.Tree != null)
                        AddTree(menu, rule.Tree, selected, owner, path, guard, index);
            menu.DropDown(rect);
        }

        /// <summary>Finds a concise label without exposing missing keys as editable strings.</summary>
        /// <param name="tree">Default sheet supplying names.</param>
        /// <param name="step">Selected checkpoint.</param>
        /// <returns>Whole-tree or named checkpoint label.</returns>
        private static string Label(DialogueTree tree, DialogueStepReference step)
        {
            if (step == null || string.IsNullOrEmpty(step.Block))
                return "Whole Tree";
            if (tree != null)
                foreach (DialogueNode block in tree.Nodes)
                    if (block.Id == step.Block)
                    {
                        if (string.IsNullOrEmpty(step.Entry))
                            return block.Name + " / Any Entry";
                        foreach (DialogueRow row in block.Entries)
                            if (row.Id == step.Entry)
                                return block.Name + " / " + row.Name;
                    }
            return "Spawn Variant Step";
        }

        /// <summary>Adds this sheet's block and entry checkpoints to a menu.</summary>
        /// <param name="menu">Popup receiving choices.</param>
        /// <param name="tree">Sheet supplying keys and labels.</param>
        /// <param name="selected">Current descriptor.</param>
        /// <param name="owner">Detached proposal receiving the edit.</param>
        /// <param name="path">Serialized descriptor route.</param>
        /// <param name="guard">Delayed-edit selection guard.</param>
        /// <param name="index">Array index, or minus one for a direct descriptor.</param>
        private static void AddTree(GenericMenu menu, DialogueTree tree, DialogueStepReference selected, UnityEngine.Object owner, string path, Func<bool> guard, int index)
        {
            if (tree == null)
                return;
            foreach (DialogueNode block in tree.Nodes)
            {
                if (block.End)
                    continue;
                string prefix = tree.name + "/" + block.Name + "/";
                Add(menu, prefix + "Any Entry", selected, new DialogueStepReference { Block = block.Id }, owner, path, guard, index);
                foreach (DialogueRow row in block.Entries)
                    Add(menu, prefix + row.Name, selected, new DialogueStepReference { Block = block.Id, Entry = row.Id }, owner, path, guard, index);
            }
        }

        /// <summary>Commits one deliberate menu choice to the original guarded proposal.</summary>
        /// <param name="menu">Popup receiving the option.</param>
        /// <param name="label">Visible named choice.</param>
        /// <param name="selected">Current value.</param>
        /// <param name="value">New stable selection.</param>
        /// <param name="owner">Serialized proposal.</param>
        /// <param name="path">Descriptor route.</param>
        /// <param name="guard">Pending-edit guard.</param>
        /// <param name="index">Array index, or minus one for a direct descriptor.</param>
        private static void Add(GenericMenu menu, string label, DialogueStepReference selected, DialogueStepReference value, UnityEngine.Object owner, string path, Func<bool> guard, int index)
        {
            menu.AddItem(new GUIContent(label), selected != null && selected.Block == value.Block && selected.Entry == value.Entry, () =>
            {
                if (!guard())
                    return;
                using SerializedObject data = new SerializedObject(owner);
                SerializedProperty destination = data.FindProperty(path);
                if (index >= 0)
                {
                    while (destination.arraySize <= index)
                    {
                        destination.arraySize++;
                        destination.GetArrayElementAtIndex(destination.arraySize - 1).boxedValue = new DialogueStepReference();
                    }
                    destination = destination.GetArrayElementAtIndex(index);
                }
                destination.boxedValue = value;
                data.ApplyModifiedProperties();
                StudioFieldMenu.Notify(owner);
            });
        }

        #endregion

        #endregion
    }
}
