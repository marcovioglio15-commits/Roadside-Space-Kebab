using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns independently reusable modification assets without editing shared data implicitly.</summary>
    internal static class ContactModificationControls
    {
        #region Methods
        #region Controls

        /// <summary>Draws safe per-element controls and opens each shared definition in its own transaction.</summary>
        /// <param name="settings">Interaction proposal containing the modification references.</param>
        internal static void Draw(SerializedProperty settings)
        {
            SerializedProperty modifications = settings.FindPropertyRelative("Modifications");
            modifications.isExpanded = StudioArrayGUI.Foldout(modifications, new GUIContent("Modifications", modifications.tooltip));
            if (!modifications.isExpanded)
                return;
            for (int index = 0; index < modifications.arraySize; index++)
            {
                SerializedProperty entry = modifications.GetArrayElementAtIndex(index);
                ContactModificationDefinition definition = entry.objectReferenceValue as ContactModificationDefinition;
                string name = definition != null && definition.Settings != null ? definition.Settings.Name : "Modification " + (index + 1);
                using (new EditorGUI.IndentLevelScope())
                    if (StudioArrayGUI.Header(modifications, index, new GUIContent(name, "Reusable flag-filtered contact modification.")))
                        break;
                if (!entry.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                StudioFieldGUI.PropertyField(entry, new GUIContent("Preset", "Shared modification asset referenced by this interaction."));
                using StudioButton.RowScope row = new StudioButton.RowScope();
                using (new EditorGUI.DisabledScope(definition == null))
                    if (StudioButton.Draw(new GUIContent("Edit", "Open this modification's independent Apply/Discard session.")))
                        ContactModificationWindow.Open(definition);
                if (StudioButton.Draw(new GUIContent("New", "Create and assign a reusable modification asset.")))
                    Create(entry);
            }
            StudioArrayGUI.Add(modifications, "Add Modification", () => null);
        }

        /// <summary>Creates a shared asset and assigns it only to the pending interaction.</summary>
        /// <param name="entry">Draft reference receiving the new definition.</param>
        private static void Create(SerializedProperty entry)
        {
            string path = EditorUtility.SaveFilePanelInProject("New Contact Modification", "Contact Modification", "asset", "Choose where to save this modification.");
            if (string.IsNullOrEmpty(path))
                return;
            ContactModificationDefinition definition = ScriptableObject.CreateInstance<ContactModificationDefinition>();
            AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(definition, "Create Contact Modification");
            entry.objectReferenceValue = definition;
            ContactModificationWindow.Open(definition);
        }

        #endregion
        #endregion
    }
}
