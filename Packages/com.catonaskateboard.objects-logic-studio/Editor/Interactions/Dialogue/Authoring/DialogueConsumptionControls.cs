using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Offers local order names or consumed flags according to the edited object's components.</summary>
    internal static class DialogueConsumptionControls
    {
        #region Methods
        #region Controls

        /// <summary>Draws one independently enabled consumption filter.</summary>
        /// <param name="filter">Serialized filter on an entry or interaction.</param>
        /// <param name="label">Scope shown beside the enabling toggle.</param>
        internal static void Draw(SerializedProperty filter, string label)
        {
            HoverControls.Field(filter, "Enabled", label);
            if (!filter.FindPropertyRelative("Enabled").boolValue)
                return;
            UnityEngine.Object owner = filter.serializedObject.targetObject;
            GameObject root = owner switch
            {
                ObjectWorkspace workspace => workspace.Target.Resolve(),
                Component component => component.gameObject,
                _ => null
            };
            ObjectAvailableOrders orders = root != null ? root.GetComponent<ObjectAvailableOrders>() : null;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            if (orders != null)
            {
                Names(filter.FindPropertyRelative("Orders"), orders.Settings);
                HoverControls.Field(filter, "Result");
                if (filter.FindPropertyRelative("Orders").arraySize > 1)
                    HoverControls.Field(filter, "RequireAll");
            }
            else if (root == null && filter.FindPropertyRelative("Orders").arraySize > 0)
            {
                // Unbound presets retain order names until a local object supplies the catalog.
                StudioFieldGUI.PropertyField(filter.FindPropertyRelative("Orders"), true);
                HoverControls.Field(filter, "Result");
                HoverControls.Field(filter, "RequireAll");
            }
            else
            {
                SerializedProperty flags = filter.FindPropertyRelative("RequiredFlags");
                if (flags.arraySize > 1)
                    HoverControls.Field(filter, "RequireAll");
                FlagRequirementControls.Draw(flags, "+ Add Consumed Flag", false);
            }
        }

        /// <summary>Draws a multiple-choice menu retaining missing names until explicitly removed.</summary>
        /// <param name="property">Selected order names.</param>
        /// <param name="settings">Local available order catalog.</param>
        private static void Names(SerializedProperty property, OrderSettings settings)
        {
            List<string> selected = new List<string>();
            for (int index = 0; index < property.arraySize; index++)
                selected.Add(property.GetArrayElementAtIndex(index).stringValue);
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Orders", property.tooltip));
            if (!GUI.Button(rect, selected.Count == 0 ? "Select Orders" : string.Join(", ", selected), EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu();
            HashSet<string> names = new HashSet<string>(selected);
            foreach (OrderEntry entry in settings.Entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Name))
                    names.Add(entry.Name);
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            foreach (string name in names)
                menu.AddItem(new GUIContent(name), selected.Contains(name), () =>
                {
                    // Delayed callbacks reopen the original draft and preserve its selection guard.
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(owner);
                    SerializedProperty array = data.FindProperty(path);
                    List<string> values = new List<string>();
                    for (int index = 0; index < array.arraySize; index++)
                        values.Add(array.GetArrayElementAtIndex(index).stringValue);
                    if (!values.Remove(name))
                        values.Add(name);
                    array.arraySize = values.Count;
                    for (int index = 0; index < values.Count; index++)
                        array.GetArrayElementAtIndex(index).stringValue = values[index];
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(owner);
                });
            menu.DropDown(rect);
        }

        #endregion
        #endregion
    }
}
