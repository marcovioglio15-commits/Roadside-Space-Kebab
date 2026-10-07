using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares threshold and hierarchy-based appearance controls for storage and supply.</summary>
    internal static class InventoryFillControls
    {
        #region Methods

        #region Drawing

        /// <summary>Edits complete fill states without exposing manual mesh or renderer paths.</summary>
        /// <param name="steps">Serialized fill-step list.</param>
        /// <param name="sections">Workspace or Inspector foldout state.</param>
        /// <param name="title">Feature-specific section title.</param>
        internal static void Draw(SerializedProperty steps, ObjectStudioSections sections, string title)
        {
            // Thresholds work in both directions; explicit counts avoid percentage-rounding ambiguities.
            if (!sections.Draw(title, steps.tooltip, steps))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            for (int index = 0; index < steps.arraySize; index++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    step.isExpanded = StudioArrayGUI.Foldout(step, new GUIContent("At least "
                        + step.FindPropertyRelative("Count").intValue + " items", "Highest reached threshold selects this complete appearance."));
                    if (GUILayout.Button(new GUIContent("−", "Remove this fill step."), GUILayout.Width(26f)))
                    {
                        steps.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
                if (!step.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope rowIndent = new EditorGUI.IndentLevelScope();
                HoverControls.Field(step, "Count");
                ItemAppearanceControls.Draw(step.FindPropertyRelative("Appearance"));
            }
            if (StudioButton.Draw(new GUIContent("+ Add Fill Step", "Add an item-count threshold with mesh or material changes.")))
            {
                steps.arraySize++;
                steps.GetArrayElementAtIndex(steps.arraySize - 1).boxedValue = new InventoryFillStep();
                steps.GetArrayElementAtIndex(steps.arraySize - 1).isExpanded = true;
            }
        }

        #endregion

        #endregion
    }
}
