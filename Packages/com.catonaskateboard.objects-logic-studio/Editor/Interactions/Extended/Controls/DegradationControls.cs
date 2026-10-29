using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares conditional degradation controls between component drafts and presets.</summary>
    internal static class DegradationControls
    {
        #region Methods
        #region Drawing

        /// <summary>Edits explicit stages without exposing destructive array-size fields.</summary>
        /// <param name="settings">Detached or preset degradation settings.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            GameObject source = HierarchyPathMenu.Source(settings);
            bool impacts = settings.FindPropertyRelative("Mode").enumValueIndex == (int)DegradationMode.Impacts;
            if (sections.Draw("Degradation trigger", "Advance with time or impacts.", settings, "Mode", "Carry", "MinimumImpulse", "ImpactInterval"))
            {
                using StudioIndentScope sectionContent1 = new StudioIndentScope();
                HoverControls.Field(settings, "Mode");
                if (source == null || source.GetComponent<ObjectGrab>() != null || settings.FindPropertyRelative("Carry").enumValueIndex != 0)
                    HoverControls.Field(settings, "Carry");
                if (impacts)
                {
                    HoverControls.Field(settings, "MinimumImpulse");
                    HoverControls.Field(settings, "ImpactInterval");
                }
            }
            if (sections.Draw("Degradation Steps", "Enter each stage after its configured time or impact count.", settings, "Steps"))
            {
                using StudioIndentScope sectionContent2 = new StudioIndentScope();
                SerializedProperty steps = settings.FindPropertyRelative("Steps");
                StudioArrayGUI.Add(steps, "Add Degradation Step", () => new DegradationStep());
                for (int index = 0; index < steps.arraySize; index++)
                {
                    SerializedProperty step = steps.GetArrayElementAtIndex(index);
                    if (StudioArrayGUI.Header(steps, index, new GUIContent(step.FindPropertyRelative("Name").stringValue, "Edit this degradation stage.")))
                        break;
                    if (!step.isExpanded)
                        continue;
                    using StudioIndentScope indent = new StudioIndentScope();
                    HoverControls.Field(step, "Name");
                    HoverControls.Field(step, impacts ? "Impacts" : "Duration");
                    ItemAppearanceControls.Draw(step.FindPropertyRelative("Appearance"), suggested: source);
                    HoverControls.Field(step, "ChangeFlag");
                    if (!step.FindPropertyRelative("ChangeFlag").boolValue)
                        continue;
                    HoverControls.Field(step, "Operation");
                    if (step.FindPropertyRelative("Operation").enumValueIndex != (int)ObjectFlagOperation.Clear)
                        ExtendedInteractionControls.Flag(step.FindPropertyRelative("Flag"));
                }
            }
            if (sections.Draw("Degradation ending", "Choose what happens after the last stage.", settings, "DestroyAtEnd", "Fragments", "BreakSound", "BreakMaterial"))
            {
                using StudioIndentScope sectionContent3 = new StudioIndentScope();
                HoverControls.Field(settings, "DestroyAtEnd");
                if (!settings.FindPropertyRelative("DestroyAtEnd").boolValue)
                    return;
                HoverControls.Field(settings, "Fragments");
                if (impacts)
                {
                    HoverControls.Field(settings, "BreakSound");
                    if (settings.FindPropertyRelative("BreakSound").boolValue)
                        HoverControls.Field(settings, "BreakMaterial");
                }
            }
        }

        #endregion
        #endregion
    }
}
