using CatOnASkateboard.PlayerStudio;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Groups optional black hole camera motion and preauthored particle feedback.</summary>
    internal static class BlackHoleFeedbackControls
    {
        #region Methods
        #region Drawing

        /// <summary>Groups camera motion and authored air feedback independently.</summary>
        /// <param name="settings">Black hole proposal.</param>
        /// <param name="source">Current authoring object.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void Draw(SerializedProperty settings, GameObject source, ObjectStudioSections sections)
        {
            if (sections.Draw("Black Hole Camera Rumble", "Optional camera feedback within range.", settings, "Rumble"))
                using (new EditorGUI.IndentLevelScope())
                    Rumble(settings.FindPropertyRelative("Rumble"), sections);
            if (sections.Draw("Black Hole Air Suction", "Animate a particle effect prepared in the editor.", settings, "Air", "AirPath", "AirSpeed"))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Air", "Enabled");
                    if (settings.FindPropertyRelative("Air").boolValue)
                        Air(settings, source);
                }
        }

        /// <summary>Shows pulse controls only when a start or end pulse uses them.</summary>
        /// <param name="settings">Camera feedback proposal.</param>
        /// <param name="sections">Retained foldout state.</param>
        private static void Rumble(SerializedProperty settings, ObjectStudioSections sections)
        {
            HoverControls.Field(settings, "Enabled", "Camera Rumble");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            BlackHoleControls.Fields(settings, "AtStart", "Sustained", "AtEnd");
            if (!settings.FindPropertyRelative("AtStart").boolValue && !settings.FindPropertyRelative("Sustained").boolValue
                && !settings.FindPropertyRelative("AtEnd").boolValue)
                return;
            HoverControls.Field(settings, "Range");
            if (settings.FindPropertyRelative("AtStart").boolValue || settings.FindPropertyRelative("AtEnd").boolValue)
                HoverControls.Field(settings, "PulseDuration");
            if (sections.Draw("Black Hole Rumble Motion", "Amplitude, frequency and optional waveform.", settings, "Frequency", "Position", "Rotation", "CustomWave", "Wave"))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Frequency");
                    BlackHoleControls.Vector(settings, "Position", 0f, 0.3f);
                    BlackHoleControls.Vector(settings, "Rotation", 0f, 10f);
                    HoverControls.Field(settings, "CustomWave");
                    if (settings.FindPropertyRelative("CustomWave").boolValue)
                        HoverControls.Field(settings, "Wave");
                }
        }

        /// <summary>Links an existing particle system or prepares an editable effect inside the open prefab.</summary>
        /// <param name="settings">Black hole proposal.</param>
        /// <param name="source">Current authoring object.</param>
        private static void Air(SerializedProperty settings, GameObject source)
        {
            SerializedProperty path = settings.FindPropertyRelative("AirPath");
            Transform branch = source != null ? PlayerHierarchy.Resolve(source.transform, path.stringValue) : null;
            ParticleSystem current = branch != null ? branch.GetComponent<ParticleSystem>() : null;
            ParticleSystem selected = (ParticleSystem)StudioFieldGUI.ObjectField(new GUIContent("Air Particles", path.tooltip), current, typeof(ParticleSystem), true);
            if (selected != current && selected != null && source != null && selected.transform.IsChildOf(source.transform))
                path.stringValue = AnimationUtility.CalculateTransformPath(selected.transform, source.transform);
            using (new EditorGUI.DisabledScope(source == null || EditorUtility.IsPersistent(source) || Application.isPlaying))
                if (current == null && StudioButton.Draw(new GUIContent("Prepare Air Effect", "Create an editable particle system in this open prefab. Save the prefab and Apply this interaction's settings.")))
                {
                    ParticleSystem created = BlackHoleAirAuthoring.Build(source, (BlackHoleSettings)settings.boxedValue);
                    path.stringValue = AnimationUtility.CalculateTransformPath(created.transform, source.transform);
                }
            HoverControls.Field(settings, "AirSpeed");
        }

        #endregion
        #endregion
    }
}
