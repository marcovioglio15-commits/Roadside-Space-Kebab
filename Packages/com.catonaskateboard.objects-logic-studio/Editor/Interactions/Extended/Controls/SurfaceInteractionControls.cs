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
            if (sections.Draw("Deformable mesh", "Choose the part to bend.", settings, "Path", "Pivot", "Volume"))
            {
                using StudioIndentScope sectionContent1 = new StudioIndentScope();
                HierarchyPathMenu.Draw(settings.FindPropertyRelative("Path"), source, false, transforms: true);
                Fields(settings, "Pivot", "Volume");
                using (new EditorGUI.DisabledScope(source == null || Application.isPlaying))
                    if (StudioButton.Draw(new GUIContent("Prepare Visual Meshes", "Allow these meshes to bend.")))
                        ElasticAuthoring.Prepare(source);
            }
            if (sections.Draw("Deformation response", "Choose how the mesh reacts and recovers.", settings, "Collisions", "Layers", "MinimumImpact", "FullImpact", "Compression", "Duration", "Oscillations", "Damping", "Cooldown", "StrongestWins"))
            {
                using StudioIndentScope sectionContent2 = new StudioIndentScope();
                Fields(settings, "Collisions");
                if (settings.FindPropertyRelative("Collisions").boolValue)
                    Fields(settings, "Layers", "MinimumImpact", "FullImpact", "Compression", "Duration", "Oscillations", "Damping", "Cooldown", "StrongestWins");
            }
        }
        #endregion
        #region Trails
        /// <summary>Edits collision eligibility and the shared deposit appearance.</summary>
        /// <param name="settings">Detached trail configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void DirtTrail(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (sections.Draw("Trail detection", "Choose where and when to leave marks.", settings, "Layers", "FilterFlags", "Flags", "MinimumSpeed", "Sliding", "Spacing", "Interval"))
            {
                using StudioIndentScope sectionContent3 = new StudioIndentScope();
                Fields(settings, "Layers", "FilterFlags");
                if (settings.FindPropertyRelative("FilterFlags").boolValue && ObjectGroupScope.Allows(settings.FindPropertyRelative("Flags")))
                    ObjectFlagSelector.Draw(settings.FindPropertyRelative("Flags"));
                Fields(settings, "MinimumSpeed", "Sliding");
                if (settings.FindPropertyRelative("Sliding").boolValue)
                    Fields(settings, "Spacing");
                Fields(settings, "Interval");
            }
            Trail(settings.FindPropertyRelative("Trail"), sections);
        }
        /// <summary>Edits wetness, palette and finite fading without destructive array sizes.</summary>
        /// <param name="settings">Shared surface-mark configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        /// <param name="deposits">Whether surface geometry and lifetime controls are used.</param>
        internal static void Trail(SerializedProperty settings, ObjectStudioSections sections, bool deposits = true)
        {
            if (sections.Draw("Color And Wetness", "Shared color and shading of liquid and deposits.", settings,
                "Liquidity", "Gloss", "DetectColors", "Colors", "ColorBlend", "Opacity"))
            {
                using StudioIndentScope indent = new StudioIndentScope();
                Fields(settings, "Liquidity", "Gloss", "DetectColors");
                Colors(settings);
                if (deposits)
                    Fields(settings, "ColorBlend");
                Fields(settings, "Opacity");
            }
            if (!deposits)
                return;
            Group(settings, sections, "Deposit Shape", "Size, distribution and silhouette on receiving surfaces.",
                "Shape", "Radius", "Stretch", "Patches", "Scatter", "Irregularity");
            Group(settings, sections, "Deposit Lifetime", "Ordinary fading; target rules can override these durations.", "Lifetime", "FadeDuration");
            Group(settings, sections, "Surface Projection", "How patches conform to the receiving collider.", "ProjectionDepth", "SurfaceOffset", "MaximumAngle");
        }

        /// <summary>Edits palette entries individually while preserving manual fallback colors.</summary>
        /// <param name="settings">Shared surface appearance.</param>
        private static void Colors(SerializedProperty settings)
        {
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
        }
        #endregion
        #region Spray
        /// <summary>Shows only the targeting, capacity, squeeze and exposure branches currently enabled.</summary>
        /// <param name="settings">Detached flow configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void SpraySauce(SerializedProperty settings, ObjectStudioSections sections)
        {
            SpraySauceControls.Draw(settings, sections);
        }
        #endregion
        #region Fields
        /// <summary>Draws a themed foldout whose context menu transfers only its own fields.</summary>
        /// <param name="owner">Serialized configuration.</param>
        /// <param name="sections">Retained foldout state.</param>
        /// <param name="title">Section caption.</param>
        /// <param name="tooltip">Purpose of the section.</param>
        /// <param name="names">Fields belonging to this section.</param>
        internal static void Group(SerializedProperty owner, ObjectStudioSections sections, string title, string tooltip, params string[] names)
        {
            if (sections.Draw(title, tooltip, owner, names))
                using (new StudioIndentScope())
                    Fields(owner, names);
        }

        /// <summary>Draws scalar fields through the existing context-menu and colour system.</summary>
        /// <param name="owner">Serialized configuration.</param>
        /// <param name="names">Visible field names.</param>
        internal static void Fields(SerializedProperty owner, params string[] names)
        {
            foreach (string name in names)
                HoverControls.Field(owner, name);
        }
        #endregion
        #endregion
    }
}
