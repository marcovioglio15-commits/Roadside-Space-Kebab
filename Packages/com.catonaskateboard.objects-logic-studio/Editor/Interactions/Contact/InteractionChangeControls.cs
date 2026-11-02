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
                bool add = change.FindPropertyRelative("Operation").enumValueIndex == (int)InteractionChangeOperation.Add;
                if (add)
                {
                    HoverControls.Field(change, "AddKind", "Interaction Type");
                    HoverControls.Field(change, "Name");
                }
                else
                    Target(change.FindPropertyRelative("Target"), source);
                InteractionRuntimeKind kind = add ? (InteractionRuntimeKind)change.FindPropertyRelative("AddKind").enumValueIndex
                    : (InteractionRuntimeKind)change.FindPropertyRelative("Target.Kind").enumValueIndex;
                if (change.FindPropertyRelative("Operation").enumValueIndex == (int)InteractionChangeOperation.Remove)
                    continue;
                SerializedProperty preset = change.FindPropertyRelative("Preset");
                EditorGUI.BeginChangeCheck();
                ScriptableObject selected = (ScriptableObject)EditorGUILayout.ObjectField(new GUIContent("Preset", preset.tooltip),
                    preset.objectReferenceValue, InteractionRuntimeCatalog.PresetType(kind), false);
                if (EditorGUI.EndChangeCheck())
                    preset.objectReferenceValue = selected;
                if (add)
                    Actions(change, kind, selected);
                using (new EditorGUI.DisabledScope(selected == null))
                    if (StudioButton.Draw(new GUIContent("Select Preset", "Open the source preset in the Inspector. Runtime imports never overwrite it.")))
                        Selection.activeObject = selected;
            }
            StudioArrayGUI.Add(changes, "Add Interaction Change", () => new InteractionChangeSettings());
        }

        /// <summary>Offers only the actions actually used by a newly added preset.</summary>
        /// <param name="change">Addition block receiving local action references.</param>
        /// <param name="kind">New card family.</param>
        /// <param name="preset">Selected preset used to resolve conditional input requirements.</param>
        private static void Actions(SerializedProperty change, InteractionRuntimeKind kind, ScriptableObject preset)
        {
            bool input = kind switch
            {
                InteractionRuntimeKind.Hover or InteractionRuntimeKind.Outline or InteractionRuntimeKind.ObjectDegradation
                    or InteractionRuntimeKind.ElasticDeformation or InteractionRuntimeKind.DirtTrail or InteractionRuntimeKind.Deathbox
                    or InteractionRuntimeKind.PresenceCap or InteractionRuntimeKind.AvailableOrders or InteractionRuntimeKind.Spawn
                    or InteractionRuntimeKind.AssemblyProduct or InteractionRuntimeKind.Unlock => false,
                InteractionRuntimeKind.Dialogue => preset is DialoguePreset dialogue && dialogue.Settings.NeedsStartAction,
                InteractionRuntimeKind.Container => preset is ContainerPreset container && !container.Settings.ScoopFromDispenser,
                InteractionRuntimeKind.Contact => preset is ContactModificationPreset contact && contact.Settings.RequiresInput,
                InteractionRuntimeKind.GravityGenerator => preset is GravityGeneratorPreset gravity && gravity.Settings.Restore != GravityRestoreMode.Timer,
                InteractionRuntimeKind.Ambient => preset is AmbientPreset ambient && ambient.Settings.Mode == AmbientMode.Radio,
                InteractionRuntimeKind.AssemblyStation => preset is AssemblyStationPreset station && station.Settings.Trigger == AssemblyStationTrigger.InputAction,
                _ => true
            };
            if (input)
                StudioInputActionMenu.Draw(change.serializedObject, change.FindPropertyRelative("Action").propertyPath,
                    "ObjectsLogicStudio.Contact.Add", ExtendedInteractionControls.Button);
            if (kind == InteractionRuntimeKind.Dialogue)
                StudioInputActionMenu.Draw(change.serializedObject, change.FindPropertyRelative("AdvanceAction").propertyPath,
                    "ObjectsLogicStudio.Contact.Advance", ExtendedInteractionControls.Button);
        }

        /// <summary>Selects an existing exact card without asking for a type or field name as text.</summary>
        /// <param name="property">Portable interaction descriptor.</param>
        /// <param name="source">Sample object supplying selectable cards.</param>
        private static void Target(SerializedProperty property, GameObject source)
        {
            RuntimeInteractionReference selected = (RuntimeInteractionReference)property.boxedValue;
            string label = selected.Assigned ? selected.Kind + " / " + selected.Name : "Choose Interaction";
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Interaction", property.tooltip));
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(source == null);
            if (!GUI.Button(rect, new GUIContent(label, source == null ? "Choose a hierarchy source first." : property.tooltip), EditorStyles.popup))
                return;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            Dictionary<(InteractionRuntimeKind, string), int> occurrences = new Dictionary<(InteractionRuntimeKind, string), int>();
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            foreach (ObjectInteraction feature in source.GetComponents<ObjectInteraction>())
            {
                if (InteractionRuntimeCatalog.KindOf(feature) is not InteractionRuntimeKind kind)
                    continue;
                occurrences.TryGetValue((kind, feature.InteractionName), out int occurrence);
                occurrences[(kind, feature.InteractionName)] = occurrence + 1;
                RuntimeInteractionReference reference = new RuntimeInteractionReference
                    { Assigned = true, Kind = kind, Name = feature.InteractionName, Occurrence = occurrence };
                menu.AddItem(new GUIContent(kind + "/" + feature.InteractionName + " [" + (occurrence + 1) + "]"),
                    selected.Assigned && selected.Kind == kind && selected.Name == reference.Name && selected.Occurrence == occurrence, () =>
                    {
                        if (!guard())
                            return;
                        using SerializedObject data = new SerializedObject(owner);
                        data.FindProperty(path).boxedValue = reference;
                        data.ApplyModifiedProperties();
                        StudioFieldMenu.Notify(owner);
                    });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No interactions on this object"));
            menu.DropDown(rect);
        }

        #endregion

        #endregion
    }
}
