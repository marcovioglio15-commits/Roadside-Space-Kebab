using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows impact thresholds and per-parameter recoil ranges.</summary>
    internal static class KnockbackControls
    {
        #region Methods

        #region Drawing

        /// <summary>Draws the response and only the enabled impact modifiers.</summary>
        /// <param name="settings">Detached Knockback properties.</param>
        /// <param name="sections">Foldouts for this interaction.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (sections.Draw("Wobble", "Set fixed values or scale each value with impact.", settings,
                "Distance", "Tilt", "Duration", "Cycles", "Damping"))
                using (new StudioIndentScope())
                {
                    Range(settings, "Distance", 0f, 0.5f);
                    Range(settings, "Tilt", 0f, 45f);
                    Range(settings, "Duration", 0.1f, 2f);
                    Range(settings, "Cycles", 0.5f, 4f);
                    Range(settings, "Damping", 0f, 8f);
                }
            if (sections.Draw("Incoming Throw", "Set impact thresholds and optional throw filters.", settings,
                "ThrowWindow", "MinimumImpact", "MaximumImpact", "VolumeInfluence", "ReferenceVolume", "FilterThrows", "Filter"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(settings, "ThrowWindow");
                    HoverControls.Field(settings, "MinimumImpact");
                    HoverControls.Field(settings, "MaximumImpact");
                    HoverControls.Field(settings, "VolumeInfluence");
                    if (settings.FindPropertyRelative("VolumeInfluence").floatValue > 0f)
                        HoverControls.Field(settings, "ReferenceVolume");
                    HoverControls.Field(settings, "FilterThrows");
                    if (settings.FindPropertyRelative("FilterThrows").boolValue)
                        GravityControls.Filter(settings.FindPropertyRelative("Filter"), false);
                }
        }

        /// <summary>Shows one fixed value or two sliders in the selected parameter's units.</summary>
        /// <param name="settings">Properties that own the parameter.</param>
        /// <param name="name">Parameter field name.</param>
        /// <param name="minimum">Slider minimum.</param>
        /// <param name="maximum">Slider maximum.</param>
        private static void Range(SerializedProperty settings, string name, float minimum, float maximum)
        {
            SerializedProperty range = settings.FindPropertyRelative(name);
            if (!ObjectGroupScope.Allows(range))
                return;
            EditorGUILayout.LabelField(new GUIContent(range.displayName, range.tooltip), EditorStyles.miniBoldLabel);
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(range, "UseRange", "Scale with Impact");
            if (range.FindPropertyRelative("UseRange").boolValue)
            {
                Slider(range.FindPropertyRelative("Minimum"), minimum, maximum);
                Slider(range.FindPropertyRelative("Maximum"), minimum, maximum);
            }
            else
                Slider(range.FindPropertyRelative("Value"), minimum, maximum);
        }

        /// <summary>Edits a value using the bounds appropriate to its units.</summary>
        /// <param name="value">Serialized motion value.</param>
        /// <param name="minimum">Slider minimum.</param>
        /// <param name="maximum">Slider maximum.</param>
        private static void Slider(SerializedProperty value, float minimum, float maximum)
        {
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(new GUIContent(value.displayName, value.tooltip), value.floatValue, minimum, maximum);
            if (EditorGUI.EndChangeCheck())
                value.floatValue = next;
        }

        #endregion

        #endregion
    }
}
