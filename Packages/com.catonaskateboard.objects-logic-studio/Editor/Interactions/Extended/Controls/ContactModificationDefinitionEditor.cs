using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Uses conditional contact controls inside the definition's detached editing window.</summary>
    [CustomEditor(typeof(ContactModificationDefinition))]
    internal sealed class ContactModificationDefinitionEditor : UnityEditor.Editor
    {
        #region State

        private readonly ObjectStudioSections sections = new ObjectStudioSections();

        #endregion
        #region Methods
        #region Controls

        /// <summary>Opens persistent definitions or edits the detached window proposal.</summary>
        public override void OnInspectorGUI()
        {
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Edit Modification", "Open an independent Apply/Discard session for this shared asset.")))
                    ContactModificationWindow.Open((ContactModificationDefinition)target);
                return;
            }
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(205f);
            serializedObject.Update();
            ExtendedInteractionControls.DrawContactRule(serializedObject.FindProperty("Settings"), sections);
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
