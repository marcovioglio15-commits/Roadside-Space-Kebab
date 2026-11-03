using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits only speaker and text pages; event conditions belong to trigger presets.</summary>
    [CustomEditor(typeof(DialogueEntry))]
    internal sealed class DialogueEntryEditor : UnityEditor.Editor
    {
        #region State

        private readonly DialogueTextSelection selection = new DialogueTextSelection();
        private DialoguePalettePreset palette;

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Opens saved assets or draws their detached page proposal.</summary>
        public override void OnInspectorGUI()
        {
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Open Entry", "Edit these pages in an independent Apply/Discard window.")))
                    DialogueEntryWindow.Open((DialogueEntry)target);
                return;
            }
            serializedObject.Update();
            StudioFieldGUI.PropertyField(serializedObject.FindProperty("Name"));
            DialogueTextControls.Draw(serializedObject.FindProperty("TextEffects"), ((DialogueEntry)target).Lines);
            DialoguePaletteControls.Draw(serializedObject.FindProperty("TextEffects.Colors"), ref palette);
            SerializedProperty pages = serializedObject.FindProperty("Lines");
            for (int index = 0; index < pages.arraySize; index++)
            {
                if (StudioArrayGUI.Header(pages, index, new GUIContent("Page " + (index + 1), pages.tooltip)))
                    break;
                SerializedProperty page = pages.GetArrayElementAtIndex(index);
                if (!page.isExpanded)
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                HoverControls.Field(page, "Speaker");
                selection.Draw(page.FindPropertyRelative("Text"));
                DialogueTextControls.Tokens(page.FindPropertyRelative("Text"), selection, ((DialogueEntry)target).TextEffects.Colors);
            }
            StudioArrayGUI.Add(pages, "Add Page", () => new DialogueLine());
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
