using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns shared text without embedding duplicate pages in graph rows.</summary>
    internal static class DialogueEntryControls
    {
        #region Methods

        #region Drawing

        /// <summary>Offers existing entries and explicit independent creation or editing.</summary>
        /// <param name="property">Entry asset reference.</param>
        internal static void Draw(SerializedProperty property)
        {
            StudioFieldGUI.PropertyField(property);
            using StudioButton.RowScope row = new StudioButton.RowScope();
            using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
            {
                if (StudioButton.Draw(new GUIContent("Open Entry", "Edit this shared page sequence in its own Apply/Discard session.")))
                    DialogueEntryWindow.Open((DialogueEntry)property.objectReferenceValue);
                if (StudioButton.Draw(new GUIContent("Make Copy", "Create and select a separate entry preset.")))
                {
                    DialogueEntry copy = ObjectPresetCopy.Save((DialogueEntry)property.objectReferenceValue);
                    if (copy != null)
                        property.objectReferenceValue = copy;
                }
            }
            if (!StudioButton.Draw(new GUIContent("New Entry", "Create a separate reusable page sequence.")))
                return;
            string path = EditorUtility.SaveFilePanelInProject("New Entry", "Dialogue Entry", "asset", "Save the reusable text pages.");
            if (string.IsNullOrEmpty(path))
                return;
            DialogueEntry entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.TextEffects.Typewriter = true;
            entry.Name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(entry, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(entry, "Create Dialogue Entry");
            property.objectReferenceValue = entry;
            DialogueEntryWindow.Open(entry);
        }

        #endregion

        #endregion
    }
}
