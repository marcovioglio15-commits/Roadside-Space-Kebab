using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws reusable object-flag and positive-quantity lists for recipes and consumption conditions.</summary>
    internal static class FlagRequirementControls
    {
        #region Methods

        #region Drawing

        /// <summary>Edits concrete flag rows with explicit add/remove controls and optional recipe flags.</summary>
        /// <param name="requirements">Serialized array containing Flags and Count on each element.</param>
        /// <param name="addLabel">Context-specific add button label.</param>
        /// <param name="optional">Expose the recipe ingredient's Optional flag.</param>
        /// <param name="drawFlag">Optional recipe-specific selector; absent uses the project flag catalog.</param>
        internal static void Draw(SerializedProperty requirements, string addLabel, bool optional, Action<SerializedProperty> drawFlag = null)
        {
            // Invalid quantities remain visible instead of being silently clamped.
            for (int index = 0; index < requirements.arraySize; index++)
            {
                SerializedProperty requirement = requirements.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (drawFlag != null)
                            drawFlag(requirement.FindPropertyRelative("Flags"));
                        else
                            ExtendedInteractionControls.Flag(requirement.FindPropertyRelative("Flags"));
                        if (GUILayout.Button(new GUIContent("−", "Remove this flag requirement."), GUILayout.Width(28f)))
                        {
                            requirements.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    SerializedProperty count = requirement.FindPropertyRelative("Count");
                    count.intValue = StudioGUI.IntField(StudioFieldMenu.Value(count, new GUIContent("Quantity", count.tooltip)), count.intValue);
                    if (optional)
                        HoverControls.Field(requirement, "Optional");
                    if (count.intValue <= 0)
                        EditorGUILayout.LabelField("Quantity must be a positive whole number.", EditorStyles.miniLabel);
                }
            }
            if (StudioButton.Draw(new GUIContent(addLabel, "Add alternative project flags sharing one positive whole-number quantity.")))
            {
                requirements.arraySize++;
                SerializedProperty added = requirements.GetArrayElementAtIndex(requirements.arraySize - 1);
                added.FindPropertyRelative("Flags").arraySize = 0;
                added.FindPropertyRelative("Count").intValue = 1;
                if (optional)
                    added.FindPropertyRelative("Optional").boolValue = false;
            }
        }

        #endregion

        #endregion
    }
}
