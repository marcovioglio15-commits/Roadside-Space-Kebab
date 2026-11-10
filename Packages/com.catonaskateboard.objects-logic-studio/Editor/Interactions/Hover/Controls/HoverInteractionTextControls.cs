using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits local label rules beside the default hover text.</summary>
    internal static class HoverInteractionTextControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows rules in priority order and selects source cards from the current object.</summary>
        /// <param name="rules">Serialized rule array.</param>
        /// <param name="sections">Workspace foldout state.</param>
        internal static void Draw(SerializedProperty rules, ObjectStudioSections sections)
        {
            if (!sections.Draw("Interaction Labels", "Use the first matching rule. Hover Text is the fallback.", rules))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Label " + (index + 1), rules.tooltip)))
                    break;
                if (!rule.isExpanded)
                    continue;
                LocalInteractionMenu.Draw(rule.FindPropertyRelative("Source"), includeAnimations: true, excludeHover: true);
                HoverControls.Field(rule, "Condition");
                HoverControls.Field(rule, "Text");
            }
            StudioArrayGUI.Add(rules, "Add Label Rule", () => new HoverInteractionText());
        }

        #endregion

        #endregion
    }
}
