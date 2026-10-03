using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioInput.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Offers existing prefab interactions by name while storing stable component identities.</summary>
    internal static class UnlockInteractionControls
    {
        #region State

        private static readonly InteractionChoiceCatalog choices = new InteractionChoiceCatalog();

        #endregion

        #region Methods

        /// <summary>Displays the existing any/all policy as an explicit logical operator.</summary>
        /// <param name="property">Saved RequireAll flag.</param>
        internal static void ConditionLogic(SerializedProperty property)
        {
            // Existing rule semantics remain unchanged when the caption becomes AND/OR.
            property.boolValue = EditorGUILayout.Popup(new GUIContent("Logic", property.tooltip), property.boolValue ? 0 : 1,
                new[] { "AND - all conditions", "OR - any condition" }) == 0;
        }


        #region Catalog

        /// <summary>Rebuilds the named component catalog only after hierarchy or workspace refresh.</summary>
        /// <param name="target">Current prefab branch, or null when its stage is closed.</param>
        internal static void Refresh(GameObject target)
        {
            // Unlock rules can reference any existing interaction in the open prefab.
            choices.Refresh(target != null ? target.transform.root.gameObject : null, false);
        }

        #endregion

        #region Drawing

        /// <summary>Edits target and independent conditions with project-aware pickers.</summary>
        /// <param name="draft">Serialized stable-identity unlock proposal.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void Draw(SerializedProperty draft, ObjectStudioSections sections)
        {
            // This category never creates Grab, Dialogue, Contact or other source interactions.
            SerializedProperty settings = draft.FindPropertyRelative("Settings");
            HoverControls.Field(settings, "Operation");
            InteractionAvailabilityChange operation = (InteractionAvailabilityChange)settings.FindPropertyRelative("Operation").enumValueIndex;
            if (operation == InteractionAvailabilityChange.Unlock)
                HoverControls.Field(settings, "InitiallyLocked");
            HoverControls.Field(settings, "Repeat");
            choices.Draw(draft.FindPropertyRelative("TargetId"), operation switch
            {
                InteractionAvailabilityChange.Unlock => "Interaction To Unlock",
                InteractionAvailabilityChange.Lock => "Interaction To Lock",
                _ => "Outgoing Interaction"
            });
            long target = draft.FindPropertyRelative("TargetId").longValue;
            long replacement = draft.FindPropertyRelative("ReplacementId").longValue;
            if (operation == InteractionAvailabilityChange.Replace)
                choices.Draw(draft.FindPropertyRelative("ReplacementId"), "Incoming Interaction", target, target != 0 ? target : -1);
            if (!sections.Draw("Conditions", "Apply this availability change after existing interaction events or player commands."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            SerializedProperty conditions = settings.FindPropertyRelative("Conditions");
            SerializedProperty sources = draft.FindPropertyRelative("SourceIds");
            if (conditions.arraySize > 1)
                ConditionLogic(settings.FindPropertyRelative("RequireAll"));
            // Every condition owns its source or dedicated Button independently.
            for (int index = 0; index < conditions.arraySize; index++)
            {
                SerializedProperty condition = conditions.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Condition " + (index + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button(new GUIContent("Remove", "Remove only this condition."), GUILayout.Width(60f)))
                        {
                            conditions.DeleteArrayElementAtIndex(index);
                            sources.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    HoverControls.Field(condition, "Trigger");
                    HoverControls.Field(condition, "Count");
                    switch ((UnlockTrigger)condition.FindPropertyRelative("Trigger").enumValueIndex)
                    {
                        case UnlockTrigger.Interaction:
                            choices.Draw(sources.GetArrayElementAtIndex(index), "Source Interaction", operation switch
                            {
                                InteractionAvailabilityChange.Unlock when settings.FindPropertyRelative("InitiallyLocked").boolValue => target,
                                InteractionAvailabilityChange.Replace => replacement,
                                _ => 0
                            });
                            HoverControls.Field(condition, "Moment");
                            ObjectInteraction source = choices.Resolve(sources.GetArrayElementAtIndex(index).longValue);
                            if (source is ObjectContactModifier contact && (contact.Settings.Self.Consume || contact.Settings.Other.Consume))
                                Consumption(condition);
                            else if (condition.FindPropertyRelative("FilterConsumed").boolValue)
                                HoverControls.Field(condition, "FilterConsumed", "Consumed Filter");
                            break;
                        case UnlockTrigger.InputAction:
                            StudioInputActionMenu.Draw(draft.serializedObject, condition.FindPropertyRelative("Action").propertyPath,
                                "ObjectsLogicStudio.Unlock." + index, ExtendedInteractionControls.Button);
                            HoverControls.Field(condition, "Distance");
                            if (condition.FindPropertyRelative("FilterConsumed").boolValue)
                                HoverControls.Field(condition, "FilterConsumed", "Consumed Filter");
                            break;
                    }
                }
            }
            if (StudioButton.Draw(new GUIContent("+ Add Condition", "Add an interaction event or a dedicated player input condition.")))
            {
                conditions.arraySize++;
                sources.arraySize = conditions.arraySize;
                SerializedProperty added = conditions.GetArrayElementAtIndex(conditions.arraySize - 1);
                added.FindPropertyRelative("Trigger").enumValueIndex = 0;
                added.FindPropertyRelative("Source").objectReferenceValue = null;
                added.FindPropertyRelative("Moment").enumValueIndex = (int)InteractionMoment.Completed;
                added.FindPropertyRelative("Action").objectReferenceValue = null;
                added.FindPropertyRelative("Distance").floatValue = 3f;
                added.FindPropertyRelative("Count").intValue = 1;
                added.FindPropertyRelative("FilterConsumed").boolValue = false;
                added.FindPropertyRelative("ConsumedFlags").arraySize = 0;
                sources.GetArrayElementAtIndex(sources.arraySize - 1).longValue = 0;
            }
        }

        /// <summary>Shows the consumed-item alternatives only for completed consumption conditions.</summary>
        /// <param name="condition">Serialized event condition.</param>
        internal static void Consumption(SerializedProperty condition)
        {
            // Preserve inactive values for correction when the source or event moment changes.
            if (condition.FindPropertyRelative("Moment").enumValueIndex != (int)InteractionMoment.Completed
                && !condition.FindPropertyRelative("FilterConsumed").boolValue)
                return;
            HoverControls.Field(condition, "FilterConsumed", "Consumed Filter");
            if (condition.FindPropertyRelative("FilterConsumed").boolValue)
                ObjectFlagSelector.Draw(condition.FindPropertyRelative("ConsumedFlags"), new GUIContent("Consumed Flags", "Accept any selected flag on the item consumed by this completion."));
        }

        #endregion

        #endregion
    }
}
