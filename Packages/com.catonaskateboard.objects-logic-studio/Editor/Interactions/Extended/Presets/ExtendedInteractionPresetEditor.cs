using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Uses the same conditional controls for reusable presets and prefab feature cards.</summary>
    [CustomEditor(typeof(ExtendedInteractionPreset), true)]
    internal sealed class ExtendedInteractionPresetEditor : UnityEditor.Editor
    {
        #region State

        private readonly ObjectStudioSections sections = new ObjectStudioSections();

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Edits saved preset data directly while keeping input and HUD bindings on the item.</summary>
        public override void OnInspectorGUI()
        {
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(205f);
            // Preset edits affect future imports; already imported item snapshots stay independent.
            serializedObject.Update();
            InteractionToolControls.Draw(serializedObject.FindProperty("ToolRequirement"));
            if (target is AmbientPreset)
                AmbientControls.Draw(serializedObject.FindProperty("Settings"));
            else if (target is SlicePreset)
                SliceControls.Draw(serializedObject.FindProperty("Settings"), sections);
            else if (target is SpawnManagementPreset)
                SpawnManagementControls.Draw(serializedObject.FindProperty("Settings"), sections);
            else if (target is AssemblyStationPreset)
                AssemblyControls.DrawStation(serializedObject.FindProperty("Settings"), sections);
            else if (target is OutlinePreset)
                ExtendedInteractionControls.DrawOutline(serializedObject.FindProperty("Settings"), sections);
            else if (target is ContactModificationPreset)
                ExtendedInteractionControls.DrawContact(serializedObject.FindProperty("Settings"), sections);
            else
                ExtendedInteractionControls.DrawDialogue(serializedObject.FindProperty("Settings"), sections);
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
