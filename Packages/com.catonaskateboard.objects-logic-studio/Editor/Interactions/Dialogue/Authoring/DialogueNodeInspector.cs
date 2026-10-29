#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using System;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows only the selected graph operation's active settings and named connections.</summary>
    internal static class DialogueNodeInspector
    {
        #region Methods
        #region Drawing
        /// <summary>Edits one serialized node without exposing technical connection keys.</summary>
        /// <param name="property">Detached node configuration.</param>
        /// <param name="tree">Proposal used for destination choices.</param>
        /// <param name="orders">Optional consumption authoring context.</param>
        internal static void Draw(SerializedProperty property, DialogueTree tree, OrderSettings orders)
        {
            HoverControls.Field(property, "Name");
            HoverControls.Field(property, "Kind");
            switch ((DialogueNodeKind)property.FindPropertyRelative("Kind").enumValueIndex)
            {
                case DialogueNodeKind.Entry:
                    DialogueEntryControls.Draw(property.FindPropertyRelative("Entry"), orders);
                    Connection(property.FindPropertyRelative("Next"), tree, "Next");
                    break;
                case DialogueNodeKind.Condition:
                    Condition(property.FindPropertyRelative("Condition"), orders);
                    Connection(property.FindPropertyRelative("Next"), tree, "True");
                    Connection(property.FindPropertyRelative("Otherwise"), tree, "False");
                    break;
                case DialogueNodeKind.Wait:
                    HoverControls.Field(property, "Duration");
                    Connection(property.FindPropertyRelative("Next"), tree, "Next");
                    break;
                case DialogueNodeKind.Subtree:
                    DialogueTreeControls.Tree(property.FindPropertyRelative("Subtree"), orders);
                    Connection(property.FindPropertyRelative("Next"), tree, "On Return");
                    break;
                case DialogueNodeKind.Select:
                    HoverControls.Field(property, "Selection");
                    SerializedProperty branches = property.FindPropertyRelative("Branches");
                    for (int index = 0; index < branches.arraySize; index++)
                    {
                        SerializedProperty branch = branches.GetArrayElementAtIndex(index);
                        if (StudioArrayGUI.Header(branches, index, new GUIContent(branch.FindPropertyRelative("Name").stringValue, branches.tooltip)))
                            break;
                        if (!branch.isExpanded)
                            continue;
                        using StudioIndentScope indent = new StudioIndentScope();
                        HoverControls.Field(branch, "Name");
                        Connection(branch.FindPropertyRelative("Target"), tree, "Destination");
                        if (property.FindPropertyRelative("Selection").enumValueIndex == (int)DialogueSelection.WeightedRandom)
                            HoverControls.Field(branch, "Weight");
                        SerializedProperty condition = branch.FindPropertyRelative("Condition");
                        condition.isExpanded = EditorGUILayout.Foldout(condition.isExpanded, new GUIContent("Conditions", condition.tooltip), true);
                        if (condition.isExpanded)
                            Condition(condition, orders);
                    }
                    StudioArrayGUI.Add(branches, "Add Branch", () => new DialogueBranch());
                    Connection(property.FindPropertyRelative("Otherwise"), tree, "Fallback");
                    break;
            }
        }

        /// <summary>Offers node titles while storing only stable internal destination keys.</summary>
        /// <param name="property">Serialized destination key.</param>
        /// <param name="tree">Graph containing valid targets.</param>
        /// <param name="label">Port name.</param>
        internal static void Connection(SerializedProperty property, DialogueTree tree, string label)
        {
            GUIContent[] names = new GUIContent[tree.Nodes.Length + 1];
            names[0] = new GUIContent("None");
            for (int index = 0; index < tree.Nodes.Length; index++)
                names[index + 1] = new GUIContent((index + 1) + ". " + tree.Nodes[index].Name);
            int selected = Array.FindIndex(tree.Nodes, node => node.Id == property.stringValue) + 1;
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            EditorGUI.BeginChangeCheck();
            int choice = EditorGUI.Popup(rect, new GUIContent(label, property.tooltip), selected, names);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = choice > 0 ? tree.Nodes[choice - 1].Id : string.Empty;
        }

        /// <summary>Draws the shared predicate form with context-aware order or flag selectors.</summary>
        /// <param name="property">Serialized predicate.</param>
        /// <param name="orders">Optional destination catalogs.</param>
        private static void Condition(SerializedProperty property, OrderSettings orders)
        {
            HoverControls.Field(property, "RequireAll");
            HoverControls.Field(property, "Invert");
            HoverControls.Field(property, "CheckFlags");
            if (property.FindPropertyRelative("CheckFlags").boolValue)
            {
                ObjectFlagSelector.Draw(property.FindPropertyRelative("Flags"));
                if (property.FindPropertyRelative("Flags").arraySize > 1)
                    HoverControls.Field(property, "AllFlags");
            }
            HoverControls.Field(property, "CheckSpawn");
            if (property.FindPropertyRelative("CheckSpawn").boolValue)
            {
                HoverControls.Field(property, "FirstSpawn");
                HoverControls.Field(property, "LastSpawn");
            }
            DialogueConsumptionControls.Draw(property.FindPropertyRelative("Consumption"), "Consumption", context: orders);
        }
        #endregion
        #endregion
    }
}
#endif
