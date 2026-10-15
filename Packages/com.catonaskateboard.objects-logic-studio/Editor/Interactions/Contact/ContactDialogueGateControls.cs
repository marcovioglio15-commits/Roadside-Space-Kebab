using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Selects one local consumption-unlocking dialogue while retaining portable preset bindings.</summary>
    internal static class ContactDialogueGateControls
    {
        #region Methods
        #region Drawing

        /// <summary>Offers local dialogues only; unbound presets retain editable name and occurrence fields.</summary>
        /// <param name="gate">Detached consumption gate settings.</param>
        internal static void Draw(SerializedProperty gate)
        {
            GameObject root = gate.serializedObject.targetObject is ObjectWorkspace workspace
                ? workspace.Target.Resolve() : HierarchyPathMenu.Source(gate, true);
            if (root != null && root.GetComponents<ObjectDialogue>().Length == 0 && !gate.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(gate, "Enabled", "Consume On Dialogue");
            if (!gate.FindPropertyRelative("Enabled").boolValue)
                return;
            if (root == null)
            {
                HoverControls.Field(gate, "DialogueName");
                HoverControls.Field(gate, "Occurrence");
                return;
            }
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, gate);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(gate));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("After Dialogue", "Enable consuming modifications after this local dialogue completes."));
            string name = gate.FindPropertyRelative("DialogueName").stringValue;
            if (!GUI.Button(rect, string.IsNullOrEmpty(name) ? "Select Dialogue" : name, EditorStyles.popup))
                return;
            UnityEngine.Object target = gate.serializedObject.targetObject;
            string path = gate.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(target, path);
            Dictionary<string, int> occurrences = new Dictionary<string, int>();
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            foreach (ObjectDialogue dialogue in root.GetComponents<ObjectDialogue>())
            {
                string selectedName = dialogue.InteractionName;
                occurrences.TryGetValue(selectedName, out int occurrence);
                occurrences[selectedName] = occurrence + 1;
                bool selected = name == selectedName && gate.FindPropertyRelative("Occurrence").intValue == occurrence;
                menu.AddItem(new GUIContent(selectedName), selected, () =>
                {
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(target);
                    SerializedProperty field = data.FindProperty(path);
                    field.FindPropertyRelative("DialogueName").stringValue = selectedName;
                    field.FindPropertyRelative("Occurrence").intValue = occurrence;
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(target);
                });
            }
            if (occurrences.Count == 0)
                menu.AddDisabledItem(new GUIContent("No local Dialogue"));
            menu.DropDown(rect);
        }

        #endregion
        #endregion
    }
}
