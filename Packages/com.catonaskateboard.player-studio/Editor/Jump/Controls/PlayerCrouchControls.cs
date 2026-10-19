using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shows crouch options next to Jump in both preset and retained draft editors.</summary>
    internal static class PlayerCrouchControls
    {
        #region Methods
        #region Drawing

        /// <summary>Edits active crouch fields without exposing unused disabled settings.</summary>
        /// <param name="settings">Serialized crouch settings in a preset or a window draft.</param>
        internal static void Draw(SerializedProperty settings)
        {
            Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            Field(settings, "Toggle");
            Field(settings, "HeightRatio");
            Field(settings, "SpeedMultiplier");
            Field(settings, "Transition");
            Field(settings, "LowerCamera");
            Field(settings, "AllowJump");
            Field(settings, "ClearanceLayers");
        }

        /// <summary>Updates the serialized window proposal using native Undo.</summary>
        /// <param name="owner">Window retaining locomotion proposals.</param>
        /// <param name="sections">Persistent section visibility.</param>
        /// <returns>True when a crouch setting changed.</returns>
        internal static bool Draw(Object owner, PlayerStudioSections sections)
        {
            using SerializedObject data = new SerializedObject(owner);
            if (sections.Draw("Locomotion.Crouch", "Crouch"))
                using (new EditorGUI.IndentLevelScope())
                    Draw(data.FindProperty("state.Locomotion.crouch"));
            return data.ApplyModifiedProperties();
        }

        /// <summary>Retains the setting's tooltip and numeric slider through the shared renderer.</summary>
        /// <param name="settings">Parent settings.</param>
        /// <param name="name">Field to display.</param>
        private static void Field(SerializedProperty settings, string name)
        {
            StudioFieldGUI.PropertyField(settings.FindPropertyRelative(name));
        }

        #endregion
        #endregion
    }
}
