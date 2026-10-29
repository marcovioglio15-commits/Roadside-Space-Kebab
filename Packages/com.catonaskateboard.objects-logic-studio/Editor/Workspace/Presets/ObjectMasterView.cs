using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows the selected object's master reference and whole-object capture, import and export commands.</summary>
    internal sealed class ObjectMasterView
    {
        #region State

        private bool includeChildren = true;
        private string warning = string.Empty;

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Separates the object's current configuration from the project-wide preset library.</summary>
        /// <param name="state">Workspace retaining the selected master.</param>
        /// <param name="target">Currently selected configuration root.</param>
        internal void Draw(ObjectWorkspace state, GameObject target)
        {
            if (!ObjectWorkspaceGUI.Foldout(state, "Object.Master", "Object master", "Capture, import or export the whole object."))
                return;
            using StudioIndentScope sectionContent1 = new StudioIndentScope();
            EditorGUI.BeginChangeCheck();
            state.Master = (ObjectMasterPreset)EditorGUILayout.ObjectField(new GUIContent("Master preset", "Choose a complete object configuration."), state.Master, typeof(ObjectMasterPreset), false);
            if (EditorGUI.EndChangeCheck())
                state.Persist();
            includeChildren = EditorGUILayout.Toggle(new GUIContent("Include children", "Capture the interactions on child objects too."), includeChildren);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            if (StudioButton.Draw(new GUIContent("Save object as master", "Save this object's configuration and its group presets."), EditorStyles.miniButton))
                Save(state, target);
            using (new EditorGUI.DisabledScope(state.Master == null))
            {
                if (StudioButton.Draw(new GUIContent("Import", "Preview this master's changes before applying."), EditorStyles.miniButton))
                    ObjectMasterImportWindow.Open(state.Master, target);
                if (StudioButton.Draw(new GUIContent("Export", "Choose what to include in a portable package."), EditorStyles.miniButton))
                    ObjectMasterExportWindow.Open(state.Master);
            }
            if (state.Master != null)
                EditorGUILayout.LabelField(state.Master.Interactions.Length + " interaction presets · " + state.Master.Shared.Length + " shared presets", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Saves only an applied configuration so the master never captures half-finished object edits.</summary>
        /// <param name="state">Workspace retaining current drafts.</param>
        /// <param name="target">Applied object to capture.</param>
        private void Save(ObjectWorkspace state, GameObject target)
        {
            if (ObjectWorkspaceDrafts.Count(state) > 0 || ObjectStructureChanges.Count(state) > 0)
            {
                warning = "Apply the object before saving a master preset.";
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save object master", target.name + " Master", "asset", "Choose where to save the complete configuration.");
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                state.Master = ObjectMasterCapture.Create(target, path, includeChildren);
                state.Persist();
                warning = "Master preset saved.";
            }
            catch (Exception exception)
            {
                warning = exception.Message;
            }
        }

        #endregion

        #endregion
    }
}
