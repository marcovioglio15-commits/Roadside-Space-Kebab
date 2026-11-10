using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
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
        /// <param name="allowItemFill">Whether this container supports visual copies of its contents.</param>
        internal static void Draw(SerializedProperty steps, ObjectStudioSections sections, string title, bool allowItemFill = false)
        {
            // Thresholds work in both directions; explicit counts avoid percentage-rounding ambiguities.
            if (!sections.Draw(title, steps.tooltip, steps))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
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
                using StudioIndentScope rowIndent = new StudioIndentScope();
                HoverControls.Field(step, "Count");
                SerializedProperty mode = step.FindPropertyRelative("Mode");
                if (allowItemFill || mode.enumValueIndex == (int)InventoryFillMode.ActualItemFill)
                    HoverControls.Field(step, "Mode");
                if (mode.enumValueIndex == (int)InventoryFillMode.ActualItemFill)
                    InventoryFillMagnetControls.Draw(step);
                else
                    ItemAppearanceControls.Draw(step.FindPropertyRelative("Appearance"));
                SerializedProperty flags = step.FindPropertyRelative("Flags");
                HoverControls.Field(flags, "Mode", "Flag Update Mode");
                if (flags.FindPropertyRelative("Mode").enumValueIndex == (int)InventoryFlagMode.KeepBase)
                    continue;
                ObjectFlagSelector.Draw(flags.FindPropertyRelative("Flags"));
                if (flags.FindPropertyRelative("Mode").enumValueIndex == (int)InventoryFlagMode.AddRemove)
                    ObjectFlagSelector.Draw(flags.FindPropertyRelative("Remove"));
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
