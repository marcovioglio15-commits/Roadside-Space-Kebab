using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits contact card additions, removals and imports exclusively through dedicated presets.</summary>
    internal static class InteractionChangeControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows only the target, preset and bindings required by the chosen operation.</summary>
        /// <param name="changes">Participant's ordered interaction changes.</param>
        /// <param name="source">Existing sample hierarchy used by interaction selectors.</param>
        internal static void Draw(SerializedProperty changes, GameObject source)
        {
            changes.isExpanded = StudioArrayGUI.Foldout(changes, new GUIContent("Interactions", changes.tooltip));
            if (!changes.isExpanded)
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < changes.arraySize; index++)
            {
                SerializedProperty change = changes.GetArrayElementAtIndex(index);
                InteractionChangeSettings value = (InteractionChangeSettings)change.boxedValue;
                string label = ObjectNames.NicifyVariableName(value.Operation.ToString()) + " / " + ObjectNames.NicifyVariableName(value.Kind.ToString());
                if (StudioArrayGUI.Header(changes, index, new GUIContent(label, changes.tooltip)))
                    break;
                if (!change.isExpanded)
                    continue;
                using StudioIndentScope row = new StudioIndentScope();
                HoverControls.Field(change, "Operation");
                InteractionChangeOperation operation = (InteractionChangeOperation)change.FindPropertyRelative("Operation").enumValueIndex;
                bool add = operation == InteractionChangeOperation.Add;
                if (add)
                {
                    HoverControls.Field(change, "AddKind", "Interaction Type");
                    HoverControls.Field(change, "Name");
                }
                else
                    Target(change.FindPropertyRelative("Target"), source, operation);
                InteractionRuntimeKind kind = add ? (InteractionRuntimeKind)change.FindPropertyRelative("AddKind").enumValueIndex
                    : (InteractionRuntimeKind)change.FindPropertyRelative("Target.Kind").enumValueIndex;
                if (operation is not (InteractionChangeOperation.Add or InteractionChangeOperation.ApplyPreset))
                    continue;
                InteractionGroupControls.Draw(change, kind);
                if (add)
                    Actions(change, kind);
            }
            StudioArrayGUI.Add(changes, "Add Interaction Change", () => new InteractionChangeSettings());
        }

        /// <summary>Offers only the actions actually used by a newly added preset.</summary>
        /// <param name="change">Addition block receiving local action references.</param>
        /// <param name="kind">New card family.</param>
        private static void Actions(SerializedProperty change, InteractionRuntimeKind kind)
        {
            // Prepared group settings determine which local input bindings are relevant.
            if (change.FindPropertyRelative("RequiresAction").boolValue)
                StudioInputActionMenu.Draw(change.serializedObject, change.FindPropertyRelative("Action").propertyPath,
                    "ObjectsLogicStudio.Contact.Add", ExtendedInteractionControls.Button);
            if (kind == InteractionRuntimeKind.Dialogue)
                StudioInputActionMenu.Draw(change.serializedObject, change.FindPropertyRelative("AdvanceAction").propertyPath,
                    "ObjectsLogicStudio.Contact.Advance", ExtendedInteractionControls.Button);
        }

        /// <summary>Selects an existing exact card without asking for a type or field name as text.</summary>
        /// <param name="property">Portable interaction descriptor.</param>
        /// <param name="source">Sample object supplying selectable cards.</param>
        /// <param name="operation">Determines whether type selection changes one or all matching cards.</param>
        private static void Target(SerializedProperty property, GameObject source, InteractionChangeOperation operation)
        {
            bool allowTypes = operation != InteractionChangeOperation.ApplyPreset;
            bool remove = operation == InteractionChangeOperation.Remove;
            RuntimeInteractionReference selected = (RuntimeInteractionReference)property.boxedValue;
            string label = !selected.Assigned ? "Choose Interaction" : selected.AllOfType ? selected.Kind + (remove ? " (all)" : " (by type)") : selected.Kind + " / " + selected.Name;
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent("Interaction", property.tooltip));
            Rect rect = field.GetRect(EditorGUIUtility.singleLineHeight);
            rect = field.ValueRect(rect);
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(source == null && !allowTypes);
            if (!GUI.Button(rect, new GUIContent(label, allowTypes ? "Select a type or an exact card. Missing targets are ignored." : property.tooltip), EditorStyles.popup))
                return;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            Dictionary<(InteractionRuntimeKind, string), int> occurrences = new Dictionary<(InteractionRuntimeKind, string), int>();
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            if (allowTypes)
                foreach (InteractionRuntimeKind kind in Enum.GetValues(typeof(InteractionRuntimeKind)))
                    AddTarget(menu, (remove ? "All of Type/" : "By Type/") + ObjectNames.NicifyVariableName(kind.ToString()), selected,
                        new RuntimeInteractionReference { Assigned = true, AllOfType = true, Kind = kind }, owner, path, guard);
            foreach (ObjectInteraction feature in source != null ? source.GetComponents<ObjectInteraction>() : Array.Empty<ObjectInteraction>())
            {
                if (InteractionRuntimeCatalog.KindOf(feature) is not InteractionRuntimeKind kind)
                    continue;
                occurrences.TryGetValue((kind, feature.InteractionName), out int occurrence);
                occurrences[(kind, feature.InteractionName)] = occurrence + 1;
                RuntimeInteractionReference reference = new RuntimeInteractionReference
                    { Assigned = true, Kind = kind, Name = feature.InteractionName, Occurrence = occurrence };
                AddTarget(menu, kind + "/" + feature.InteractionName + " [" + (occurrence + 1) + "]", selected, reference, owner, path, guard);
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No interactions on this object"));
            menu.DropDown(rect);
        }

        /// <summary>Shares guarded menu writes for exact cards and portable type removals.</summary>
        /// <param name="menu">Interaction selection menu.</param>
        /// <param name="label">Visible menu path.</param>
        /// <param name="selected">Current selection.</param>
        /// <param name="reference">Selection committed by this item.</param>
        /// <param name="owner">Detached preset or workspace holding the field.</param>
        /// <param name="path">Serialized selection path.</param>
        /// <param name="guard">Checks that the original field is still editable.</param>
        private static void AddTarget(GenericMenu menu, string label, RuntimeInteractionReference selected,
            RuntimeInteractionReference reference, UnityEngine.Object owner, string path, Func<bool> guard)
        {
            menu.AddItem(new GUIContent(label), selected.Assigned && selected.Kind == reference.Kind && selected.AllOfType == reference.AllOfType
                && (reference.AllOfType || selected.Name == reference.Name && selected.Occurrence == reference.Occurrence), () =>
            {
                // Menus may outlive their original inspector or draft.
                if (!guard())
                    return;
                using SerializedObject data = new SerializedObject(owner);
                data.FindProperty(path).boxedValue = reference;
                data.ApplyModifiedProperties();
                StudioFieldMenu.Notify(owner);
            });
        }

        #endregion

        #endregion
    }
}
