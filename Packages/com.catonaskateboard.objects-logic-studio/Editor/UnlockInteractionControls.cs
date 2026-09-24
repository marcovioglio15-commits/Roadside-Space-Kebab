using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioInput.Editor;
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
            choices.Draw(draft.FindPropertyRelative("TargetId"), "Locked Interaction", 0);
            if (!sections.Draw("Unlock Conditions", "Release this initial lock after the selected existing interactions or player commands."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            SerializedProperty settings = draft.FindPropertyRelative("Settings");
            SerializedProperty conditions = settings.FindPropertyRelative("Conditions");
            SerializedProperty sources = draft.FindPropertyRelative("SourceIds");
            if (conditions.arraySize > 1)
                HoverControls.Field(settings, "RequireAll");
            // Every condition owns its source or dedicated Button independently.
            for (int index = 0; index < conditions.arraySize; index++)
            {
                SerializedProperty condition = conditions.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Condition " + (index + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button(new GUIContent("Remove", "Remove only this unlock condition."), GUILayout.Width(60f)))
                        {
                            conditions.DeleteArrayElementAtIndex(index);
                            sources.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    HoverControls.Field(condition, "Trigger");
                    switch ((UnlockTrigger)condition.FindPropertyRelative("Trigger").enumValueIndex)
                    {
                        case UnlockTrigger.Interaction:
                            choices.Draw(sources.GetArrayElementAtIndex(index), "Source Interaction", draft.FindPropertyRelative("TargetId").longValue);
                            HoverControls.Field(condition, "Moment");
                            break;
                        case UnlockTrigger.InputAction:
                            StudioInputActionMenu.Draw(draft.serializedObject, condition.FindPropertyRelative("Action").propertyPath,
                                "ObjectsLogicStudio.Unlock." + index, ExtendedInteractionControls.Button);
                            HoverControls.Field(condition, "Distance");
                            break;
                    }
                }
            }
            if (GUILayout.Button(new GUIContent("+ Add Condition", "Add an interaction event or a dedicated player input condition.")))
            {
                conditions.arraySize++;
                sources.arraySize = conditions.arraySize;
                SerializedProperty added = conditions.GetArrayElementAtIndex(conditions.arraySize - 1);
                added.FindPropertyRelative("Trigger").enumValueIndex = 0;
                added.FindPropertyRelative("Source").objectReferenceValue = null;
                added.FindPropertyRelative("Moment").enumValueIndex = (int)InteractionMoment.Completed;
                added.FindPropertyRelative("Action").objectReferenceValue = null;
                added.FindPropertyRelative("Distance").floatValue = 3f;
                sources.GetArrayElementAtIndex(sources.arraySize - 1).longValue = 0;
            }
        }

        #endregion

        #endregion
    }
}
