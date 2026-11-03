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
        /// <param name="catalog">Optional order catalog used by an independent entry window.</param>
        /// <param name="context">All spawn-specific catalogs available to an independent entry window.</param>
        internal static void Draw(SerializedProperty filter, string label, OrderCatalog catalog = null, OrderSettings context = null)
        {
            HoverControls.Field(filter, "Enabled", label, string.IsNullOrWhiteSpace(filter.tooltip) ? null : filter.tooltip);
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
            if (orders != null)
                context = orders.Settings;
            using StudioIndentScope indent = new StudioIndentScope();
            SerializedProperty result = filter.FindPropertyRelative("Result");
            result.enumValueIndex = StudioFieldGUI.Popup(StudioFieldMenu.Value(result,
                new GUIContent("Result", ResultTooltip((DialogueOrderResult)result.enumValueIndex))), result.enumValueIndex, result.enumDisplayNames);
            // Latest-delivery modes cover every open slot and have no order-name or All/Any selection.
            if (result.enumValueIndex is (int)DialogueOrderResult.NotPendingOrder or (int)DialogueOrderResult.PendingOrder or (int)DialogueOrderResult.AwaitingOrders)
                return;
            if (result.enumValueIndex == (int)DialogueOrderResult.Completed)
            {
                // A standalone preset can serve either destination; empty flags must remain editable.
                if (context == null && catalog == null)
                    Flags(filter);
                return;
            }
            if (context != null || catalog != null)
            {
                Names(filter.FindPropertyRelative("Orders"), context != null ? context.OrderNames() : catalog.OrderNames());
                if (filter.FindPropertyRelative("Orders").arraySize > 1)
                    HoverControls.Field(filter, "RequireAll");
            }
            else if (root == null && filter.FindPropertyRelative("Orders").arraySize > 0)
            {
                // Unbound presets retain order names until a local object supplies the catalog.
                StudioFieldGUI.PropertyField(filter.FindPropertyRelative("Orders"), true);
                HoverControls.Field(filter, "RequireAll");
            }
            else
                Flags(filter);
        }

        /// <summary>Keeps flag requirements accessible before a preset is assigned to an object.</summary>
        /// <param name="filter">Consumption condition retaining its independent flag list.</param>
        private static void Flags(SerializedProperty filter)
        {
            // Available Orders supplies its own progress; these rows are the flag-based alternative.
            SerializedProperty flags = filter.FindPropertyRelative("RequiredFlags");
            if (flags.arraySize > 1)
                HoverControls.Field(filter, "RequireAll");
            FlagRequirementControls.Draw(flags, "+ Add Consumed Flag", false);
        }

        /// <summary>Explains the selected result without truncating unrelated enum descriptions.</summary>
        /// <param name="result">Current consumption result.</param>
        /// <returns>The exact order and flag behavior of this result.</returns>
        private static string ResultTooltip(DialogueOrderResult result)
        {
            return result switch
            {
                DialogueOrderResult.Completed => "With Available Orders: every unit drawn for this spawn must be completed. Without Available Orders: the consumed flag quantities below must be reached.",
                DialogueOrderResult.UnexpectedDelivery => "With Available Orders: a delivery left surplus units and at least one selected order was drawn for this spawn. Without Available Orders: checks the consumed flag quantities.",
                DialogueOrderResult.PendingOrder => "The latest delivery matched an unfinished order slot before fulfilment. Requires Available Orders; selected order names and flags are ignored.",
                DialogueOrderResult.NotPendingOrder => "The latest delivery matched no unfinished order slot before fulfilment. Requires Available Orders; selected order names and flags are ignored.",
                DialogueOrderResult.AwaitingOrders => "A valid item touched the object while Modify By Contact was waiting for its configured dialogue. The item is not consumed by this event.",
                _ => string.Empty
            };
        }

        /// <summary>Draws a multiple-choice menu retaining missing names until explicitly removed.</summary>
        /// <param name="property">Selected order names.</param>
        /// <param name="entries">Local or explicitly selected base and recipe-variant names.</param>
        private static void Names(SerializedProperty property, IEnumerable<string> entries)
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
            foreach (string entry in entries)
                if (!string.IsNullOrWhiteSpace(entry))
                    names.Add(entry);
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
