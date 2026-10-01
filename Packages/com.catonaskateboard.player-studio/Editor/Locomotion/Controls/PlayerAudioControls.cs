using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits player audio in the same pending session as locomotion and gravity.</summary>
    internal static class PlayerAudioControls
    {
        #region Methods
        #region Drawing

        /// <summary>Draws conditional audio settings with native serialized Undo on the owning window.</summary>
        /// <param name="owner">Player Studio window retaining the pending session.</param>
        /// <param name="sections">Persistent section visibility.</param>
        /// <param name="headTilt">Whether the applied camera supplies step timing.</param>
        /// <returns>True when a serialized audio setting changed.</returns>
        internal static bool Draw(Object owner, PlayerStudioSections sections, bool headTilt)
        {
            // Changes remain in the draft until the shared Apply or Discard command.
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty audio = data.FindProperty("state.Locomotion.audio");
            if (sections.Draw("Locomotion.Footsteps", "Footsteps"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(audio, "Footsteps", "Enabled");
                    if (audio.FindPropertyRelative("Footsteps").boolValue)
                    {
                        if (!headTilt)
                            Field(audio, "Interval");
                        Field(audio, "MinimumSpeed", "Min Speed");
                        Field(audio, "DefaultSurface", "Default Surface");
                        Field(audio, "Surfaces", "Surface Layers");
                    }
                }
            if (sections.Draw("Locomotion.ImpactAudio", "Collision Audio"))
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty impact = audio.FindPropertyRelative("Collision");
                    Field(impact, "Enabled");
                    if (impact.FindPropertyRelative("Enabled").boolValue)
                    {
                        Field(impact, "MinimumSpeed", "Min Speed");
                        Field(impact, "Cooldown");
                    }
                }
            return data.ApplyModifiedProperties();
        }

        /// <summary>Retains field tooltips while keeping editor captions compact.</summary>
        /// <param name="parent">Settings being edited.</param>
        /// <param name="name">Serialized field.</param>
        /// <param name="label">Optional compact caption.</param>
        private static void Field(SerializedProperty parent, string name, string label = null)
        {
            // Nested layer mappings use Unity's native LayerMask selector.
            SerializedProperty property = parent.FindPropertyRelative(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label ?? property.displayName, property.tooltip), true);
        }

        #endregion
        #endregion
    }
}
