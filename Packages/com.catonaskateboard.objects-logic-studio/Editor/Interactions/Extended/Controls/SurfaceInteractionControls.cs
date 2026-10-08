using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares conditional physical-surface controls between drafts and reusable presets.</summary>
    internal static class SurfaceInteractionControls
    {
        #region Methods
        #region Deformation
        /// <summary>Edits visual response without changing physics geometry.</summary>
        /// <param name="settings">Detached elastic configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void ElasticDeformation(SerializedProperty settings, ObjectStudioSections sections)
        {
            GameObject source = HierarchyPathMenu.Source(settings);
            HierarchyPathMenu.Draw(settings.FindPropertyRelative("Path"), source, false, transforms: true);
            Fields(settings, "Pivot", "Collisions");
            if (settings.FindPropertyRelative("Collisions").boolValue)
                Fields(settings, "Layers", "MinimumImpact", "FullImpact", "Compression", "Duration", "Oscillations", "Damping", "Cooldown", "StrongestWins");
            Fields(settings, "Volume");
            using (new EditorGUI.DisabledScope(source == null || Application.isPlaying))
                if (StudioButton.Draw(new GUIContent("Prepare Visual Meshes", "Enable mesh reading on this object's imported visual models.")))
                    ElasticAuthoring.Prepare(source);
        }
        #endregion
        #region Trails
        /// <summary>Edits collision eligibility and the shared deposit appearance.</summary>
        /// <param name="settings">Detached trail configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void DirtTrail(SerializedProperty settings, ObjectStudioSections sections)
        {
            Fields(settings, "Layers", "FilterFlags");
            if (settings.FindPropertyRelative("FilterFlags").boolValue)
                ObjectFlagSelector.Draw(settings.FindPropertyRelative("Flags"));
            Fields(settings, "MinimumSpeed", "Sliding");
            if (settings.FindPropertyRelative("Sliding").boolValue)
                Fields(settings, "Spacing");
            Fields(settings, "Interval");
            Trail(settings.FindPropertyRelative("Trail"), sections);
        }
        /// <summary>Edits wetness, palette and finite fading without destructive array sizes.</summary>
        /// <param name="settings">Shared surface-mark configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        /// <param name="deposits">Whether surface geometry and lifetime controls are used.</param>
        private static void Trail(SerializedProperty settings, ObjectStudioSections sections, bool deposits = true)
        {
            if (!sections.Draw("Surface Appearance", "Shape, composition and gradual lifetime of surface deposits.", settings))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            Fields(settings, "Liquidity", "Gloss");
            if (deposits)
                Fields(settings, "Shape", "Radius", "Stretch", "Patches", "Scatter", "Irregularity");
            Fields(settings, "DetectColors");
            SerializedProperty colors = settings.FindPropertyRelative("Colors");
            StudioArrayGUI.Add(colors, settings.FindPropertyRelative("DetectColors").boolValue ? "Add Fallback Color" : "Add Color", () => Color.white);
            for (int index = 0; index < colors.arraySize; index++)
            {
                SerializedProperty color = colors.GetArrayElementAtIndex(index);
                using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
                StudioFieldGUI.PropertyField(color, new GUIContent("Color " + (index + 1), "One sample in the liquid or food palette."));
                if (GUILayout.Button(new GUIContent("-", "Remove this colour."), GUILayout.Width(24f)))
                {
                    colors.DeleteArrayElementAtIndex(index);
                    break;
                }
            }
            if (deposits)
                Fields(settings, "ColorBlend");
            Fields(settings, "Opacity");
            if (deposits)
                Fields(settings, "Lifetime", "FadeDuration", "ProjectionDepth", "SurfaceOffset", "MaximumAngle");
        }
        #endregion
        #region Spray
        /// <summary>Shows only the targeting, capacity, squeeze and exposure branches currently enabled.</summary>
        /// <param name="settings">Detached flow configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void SpraySauce(SerializedProperty settings, ObjectStudioSections sections)
        {
            Fields(settings, "HeldOnly");
            if (!settings.FindPropertyRelative("HeldOnly").boolValue)
                TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            if (settings.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Nozzle Layout", "Position and rotate the outlet against the object preview.")))
            {
                settings.serializedObject.ApplyModifiedProperties();
                SprayNozzleWindow.Open(workspace);
            }
            SerializedProperty nozzle = settings.FindPropertyRelative("Nozzle");
            Fields(nozzle, "Position", "Rotation");
            if (sections.Draw("Liquid Flow", "Physical emission from the nozzle's forward axis.", settings,
                "Rate", "Speed", "Spread", "Cohesion", "Radius", "Mass", "Damping", "Lifetime", "InheritVelocity", "Layers"))
                Fields(settings, "Rate", "Speed", "Spread", "Cohesion", "Radius", "Mass", "Damping", "Lifetime", "InheritVelocity", "Layers");
            Fields(settings, "Limited");
            if (settings.FindPropertyRelative("Limited").boolValue)
                Fields(settings, "Capacity");
            Fields(settings, "Squeeze");
            if (settings.FindPropertyRelative("Squeeze").boolValue)
                Fields(settings, "SqueezeAxis", "Compression", "SqueezeDuration");
            Fields(settings, "LeaveTrail");
            bool deposits = settings.FindPropertyRelative("LeaveTrail").boolValue;
            if (deposits)
                Fields(settings, "DepositInterval");
            Trail(settings.FindPropertyRelative("Trail"), sections, deposits);
            Fields(settings, "ChangeAppearance");
            if (!settings.FindPropertyRelative("ChangeAppearance").boolValue)
                return;
            SerializedProperty rules = settings.FindPropertyRelative("Rules");
            StudioArrayGUI.Add(rules, "Add Surface Rule", () => new SpraySurfaceRule());
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Surface Rule " + (index + 1), "Appearance after sustained wetting.")))
                    break;
                if (!rule.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                ObjectFlagSelector.Draw(rule.FindPropertyRelative("Flags"));
                Fields(rule, "Match", "Exposure", "ContactGap");
                ItemAppearanceControls.Draw(rule.FindPropertyRelative("Appearance"), explicitSource: true);
            }
        }
        #endregion
        #region Fields
        /// <summary>Draws scalar fields through the existing context-menu and colour system.</summary>
        /// <param name="owner">Serialized configuration.</param>
        /// <param name="names">Visible field names.</param>
        private static void Fields(SerializedProperty owner, params string[] names)
        {
            foreach (string name in names)
                HoverControls.Field(owner, name);
        }
        #endregion
        #endregion
    }
}
