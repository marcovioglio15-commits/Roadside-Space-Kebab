using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;
using static CatOnASkateboard.ObjectsLogicStudio.Editor.SurfaceInteractionControls;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Organizes nozzle, jet motion and receiving-surface effects into independent authoring sections.</summary>
    internal static class SpraySauceControls
    {
        #region Methods
        #region Configuration
        /// <summary>Edits only controls relevant to the enabled flow, capacity and surface modes.</summary>
        /// <param name="settings">Detached spray proposal.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (sections.Draw("Activation And Burst", "Choose held flow or one-press timed emission after squeezing.", settings,
                "Emission", "BurstDuration", "Repeat", "HeldOnly"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Fields(settings, "Emission", "HeldOnly");
                    if (settings.FindPropertyRelative("Emission").enumValueIndex == (int)SprayEmissionMode.TimedBurst)
                        Fields(settings, "BurstDuration", "Repeat");
                }
            if (!settings.FindPropertyRelative("HeldOnly").boolValue)
                TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            Nozzle(settings, sections);
            if (sections.Draw("Jet Launch", "Initial outlet speed, power and movement inherited from the source.", settings,
                "Speed", "LaunchPower", "Spread", "InheritVelocity"))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Speed", "Speed Range (m/s)");
                    HoverControls.Field(settings, "LaunchPower", "Launch Power");
                    HoverControls.Field(settings, "Spread", "Spread (Degrees)");
                    Fields(settings, "InheritVelocity");
                }
            if (sections.Draw("Jet Continuity", "Column thickness, sampling and breakup into separate drops.", settings,
                "Radius", "Rate", "Cohesion", "BreakupTime", "StretchLimit"))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Radius", "Radius Range (m)");
                    HoverControls.Field(settings, "Rate", "Samples / Second");
                    Fields(settings, "Cohesion");
                    if (settings.FindPropertyRelative("Cohesion").floatValue > 0f)
                        Fields(settings, "BreakupTime", "StretchLimit");
                }
            Group(settings, sections, "Flight And Collisions", "Airborne lifetime, drag and surfaces that absorb the liquid.",
                "Mass", "Damping", "Lifetime", "Layers", "AllowGravitySuspension");
            if (sections.Draw("Liquid Capacity", "Optional total emission time before the source becomes empty.", settings, "Limited", "Capacity"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Fields(settings, "Limited");
                    if (settings.FindPropertyRelative("Limited").boolValue)
                        HoverControls.Field(settings, "Capacity", "Capacity (Seconds)");
                }
            if (sections.Draw("Bottle Squeeze", "Compression during held emission or before a timed burst.", settings,
                "Squeeze", "SqueezeAxis", "Compression", "SqueezeDuration", "SqueezeSound"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Fields(settings, "Squeeze");
                    if (settings.FindPropertyRelative("Squeeze").boolValue)
                        Fields(settings, "SqueezeAxis", "Compression", "SqueezeDuration", "SqueezeSound");
                }
            HoverControls.Field(settings, "FlowSound");
            bool deposits = settings.boxedValue is SpraySauceSettings configuration && configuration.UsesDeposits;
            if (sections.Draw("Surface Deposits", "Ordinary dirt on impact. Target rules can enable dedicated lasting deposits.", settings, "LeaveTrail", "DepositInterval"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Fields(settings, "LeaveTrail");
                    if (deposits)
                        Fields(settings, "DepositInterval");
                }
            Trail(settings.FindPropertyRelative("Trail"), sections, deposits);
            Rules(settings, sections);
        }

        /// <summary>Keeps the layout preview and numeric pose in one collapsible section.</summary>
        /// <param name="settings">Spray settings owning the nozzle pose.</param>
        /// <param name="sections">Retained foldout state.</param>
        private static void Nozzle(SerializedProperty settings, ObjectStudioSections sections)
        {
            SerializedProperty nozzle = settings.FindPropertyRelative("Nozzle");
            if (!sections.Draw("Nozzle", "Object-local outlet position and forward direction.", nozzle))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            if (settings.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Nozzle Layout", "Position and rotate the outlet against the object preview.")))
            {
                settings.serializedObject.ApplyModifiedProperties();
                OutletLayoutWindow.Open(workspace, false);
            }
            Fields(nozzle, "Position", "Rotation");
        }
        #endregion
        #region Surface Rules
        /// <summary>Separates receiving filters from mesh replacement, tint and deposit lifetime overrides.</summary>
        /// <param name="settings">Spray configuration containing ordered rules.</param>
        /// <param name="sections">Retained foldout state.</param>
        private static void Rules(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (!sections.Draw("Target Surface Rules", "Effects after sustained contact with objects matching identity flags.", settings, "ChangeAppearance", "Rules"))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "ChangeAppearance", "Enable Rules");
            if (!settings.FindPropertyRelative("ChangeAppearance").boolValue)
                return;
            SerializedProperty rules = settings.FindPropertyRelative("Rules");
            StudioArrayGUI.Add(rules, "Add Surface Rule", () => new SpraySurfaceRule());
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Surface Rule " + (index + 1), "First matching rule controls this receiving object.")))
                    break;
                if (!rule.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope ruleIndent = new EditorGUI.IndentLevelScope();
                ObjectFlagSelector.Draw(rule.FindPropertyRelative("Flags"));
                Fields(rule, "Match", "Exposure", "ContactGap");
                ItemAppearanceControls.Draw(rule.FindPropertyRelative("Appearance"), explicitSource: true);
                SerializedProperty tint = rule.FindPropertyRelative("Tint");
                tint.isExpanded = StudioArrayGUI.Foldout(tint, new GUIContent("Material Tint", tint.tooltip));
                if (tint.isExpanded)
                    using (new EditorGUI.IndentLevelScope())
                        MaterialTintControls.Draw(tint, true);
                Fields(rule, "TrailMode");
                if ((SprayTrailMode)rule.FindPropertyRelative("TrailMode").enumValueIndex == SprayTrailMode.Timed)
                    Fields(rule, "TrailLifetime", "TrailFadeDuration");
            }
        }
        #endregion
        #endregion
    }
}
