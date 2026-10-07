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
            SerializedProperty settings = serializedObject.FindProperty("Settings");
            switch (target)
            {
                case DegradationPreset:
                    DegradationControls.Draw(settings, sections);
                    break;
                case GravityGeneratorPreset:
                    GravityControls.Draw(settings, sections);
                    break;
                case AmbientPreset:
                    AmbientControls.Draw(settings);
                    break;
                case SlicePreset:
                    SliceControls.Draw(settings, sections);
                    break;
                case SpawnManagementPreset:
                    SpawnManagementControls.Draw(settings, sections);
                    break;
                case AssemblyStationPreset:
                    AssemblyControls.DrawStation(settings, sections);
                    break;
                case AssemblyProductPreset:
                    AssemblyControls.DrawProductPreset(settings, serializedObject.FindProperty("Targets"), sections);
                    break;
                case AvailableOrdersPreset:
                    OrderControls.Draw(settings, null);
                    break;
                case OutlinePreset:
                    ExtendedInteractionControls.DrawOutline(settings, sections);
                    break;
                case ContactModificationPreset:
                    ExtendedInteractionControls.DrawContact(settings, sections);
                    break;
                case DialoguePreset:
                    ExtendedInteractionControls.DrawDialogue(settings, sections);
                    break;
            }
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
