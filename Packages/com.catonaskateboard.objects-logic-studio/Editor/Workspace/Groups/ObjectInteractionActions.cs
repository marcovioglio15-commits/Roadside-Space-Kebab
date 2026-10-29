using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps existing main preset, layout and scene setup commands available beside the new groups.</summary>
    internal static class ObjectInteractionActions
    {
        #region Methods

        #region Drawing

        /// <summary>Shows the original type-specific preset commands and explicit visual editors.</summary>
        /// <param name="state">Selected interaction draft.</param>
        /// <param name="feature">Existing interaction component.</param>
        internal static void Draw(ObjectWorkspace state, ObjectInteraction feature)
        {
            switch (feature)
            {
                case ObjectAssemblyStation station:
                    using (new EditorGUI.DisabledScope(state.PrefabChanged))
                        if (StudioButton.Draw(new GUIContent("Prepare staging", "Create or reconnect the staging area for products.")))
                        {
                            AssemblyAuthoring.Prepare(station);
                            ObjectAuthoringSave.Save(feature.gameObject);
                            state.Extended.Read(feature.gameObject);
                            ObjectsLogicStudioWindow.RefreshOpen();
                        }
                    break;
                case ObjectAssemblyProduct product:
                    if (StudioButton.Draw(new GUIContent("Assembly preview", "Place ingredients and magnets in the preview.")))
                        AssemblyPreviewWindow.Open(state, product);
                    break;
                case ObjectDialogue:
                    if (StudioButton.Draw(new GUIContent("Dialogue HUD", "Show the scene's dialogue display settings.")))
                    {
                        state.Navigation.Views |= ObjectStudioNavigation.Panels.SceneSetup;
                        state.Persist();
                    }
                    break;
                case ObjectOutline outline:
                    using (new EditorGUI.DisabledScope(state.PrefabChanged))
                        if (StudioButton.Draw(new GUIContent("Collect renderers", "Refresh the meshes used by this outline.")))
                        {
                            OutlineAuthoring.Rebuild(outline);
                            ObjectAuthoringSave.Save(feature.gameObject);
                            state.Extended.Read(feature.gameObject);
                        }
                    break;
            }
        }

        /// <summary>Keeps whole-interaction preset commands in the adjacent Presets column.</summary>
        /// <param name="state">Selected interaction draft.</param>
        /// <param name="feature">Existing component.</param>
        internal static void DrawPreset(ObjectWorkspace state, ObjectInteraction feature)
        {
            if (!ObjectWorkspaceGUI.Foldout(state, "Presets.Main", "Main preset", "Import or save this interaction's full settings."))
                return;
            using StudioIndentScope sectionContent1 = new StudioIndentScope();
            switch (feature)
            {
                case ObjectHover:
                    HoverPresetView.Draw(state);
                    break;
                case ObjectSingleInteraction:
                    SingleInteractionPresetView.Draw(state);
                    break;
                case ObjectInteractionUnlock:
                    UnlockPresetView.DrawRule(state, feature.gameObject);
                    break;
                default:
                    ExtendedInteractionPresetView.Draw(state);
                    break;
            }
        }

        #endregion

        #endregion
    }
}
