using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws an independent optional visual-effect configuration for every interaction card.</summary>
    internal static class InteractionVfxControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows only the timing and placement controls needed by the selected start effect.</summary>
        /// <param name="settings">This exact interaction's detached VFX proposal.</param>
        /// <param name="sections">Persistent foldout state.</param>
        /// <param name="duration">Positive predefined interaction duration, or zero when unavailable.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections, float duration)
        {
            // Every card owns its own prefab reference; this foldout never configures other interactions.
            if (!sections.Draw("Start VFX", "Optional visual effect belonging only to this interaction."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            TransferInteractionControls.Prefab(settings.FindPropertyRelative("Prefab"));
            SerializedProperty automatic = settings.FindPropertyRelative("AutoTiming");
            if (duration > 0f || automatic.boolValue)
                HoverControls.Field(settings, "AutoTiming");
            if (!automatic.boolValue)
                HoverControls.Field(settings, "Duration");
            else if (duration > 0f)
                EditorGUILayout.LabelField("Effect Duration", duration.ToString("0.###") + " s", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("Choose manual timing: this interaction has no predefined duration.", EditorStyles.miniLabel);
            HoverControls.Field(settings, "FollowObject");
            HoverControls.Field(settings, "Position");
            HoverControls.Field(settings, "Rotation");
            HoverControls.Field(settings, "Scale");
        }

        #endregion

        #endregion
    }
}
