using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws reusable project-tag and positive-quantity lists for recipes and consumption conditions.</summary>
    internal static class TagRequirementControls
    {
        #region Methods

        #region Drawing

        /// <summary>Edits concrete tag rows with explicit add/remove controls and optional recipe flags.</summary>
        /// <param name="requirements">Serialized array containing Tag and Count on each element.</param>
        /// <param name="addLabel">Context-specific add button label.</param>
        /// <param name="optional">Expose the recipe ingredient's Optional flag.</param>
        /// <param name="drawTag">Optional recipe-specific selector; absent uses the project tag catalog.</param>
        internal static void Draw(SerializedProperty requirements, string addLabel, bool optional, Action<SerializedProperty> drawTag = null)
        {
            // Invalid quantities remain visible instead of being silently clamped.
            for (int index = 0; index < requirements.arraySize; index++)
            {
                SerializedProperty requirement = requirements.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (drawTag != null)
                            drawTag(requirement.FindPropertyRelative("Tag"));
                        else
                            ExtendedInteractionControls.Tag(requirement.FindPropertyRelative("Tag"));
                        if (GUILayout.Button(new GUIContent("−", "Remove this tag requirement."), GUILayout.Width(28f)))
                        {
                            requirements.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    SerializedProperty count = requirement.FindPropertyRelative("Count");
                    count.intValue = EditorGUILayout.IntField(new GUIContent("Quantity", count.tooltip), count.intValue);
                    if (optional)
                        HoverControls.Field(requirement, "Optional");
                    if (count.intValue <= 0)
                        EditorGUILayout.LabelField("Quantity must be a positive whole number.", EditorStyles.miniLabel);
                }
            }
            if (GUILayout.Button(new GUIContent(addLabel, "Add a project tag with a positive whole-number quantity.")))
            {
                requirements.arraySize++;
                SerializedProperty added = requirements.GetArrayElementAtIndex(requirements.arraySize - 1);
                added.FindPropertyRelative("Tag").stringValue = "Untagged";
                added.FindPropertyRelative("Count").intValue = 1;
                if (optional)
                    added.FindPropertyRelative("Optional").boolValue = false;
            }
        }

        #endregion

        #endregion
    }
}
