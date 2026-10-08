using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps shared dialogue content inside an independent editing session.</summary>
    [CustomEditor(typeof(DialogueEntry))]
    internal sealed class DialogueEntryEditor : UnityEditor.Editor
    {
        #region Methods
        #region Controls

        /// <summary>Opens saved assets in their dedicated window instead of editing them implicitly.</summary>
        public override void OnInspectorGUI()
        {
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Edit Dialogue Entry", "Open this shared preset in an independent Apply/Discard session.")))
                    DialogueEntryWindow.Open((DialogueEntry)target);
                return;
            }
            Draw(null, true, true);
        }

        /// <summary>Draws the detached entry using the requesting interaction's meaningful settings.</summary>
        /// <param name="catalog">Catalog supplying named order choices, or null for consumed flags.</param>
        /// <param name="consumption">Whether consumption filters are relevant in this context.</param>
        /// <param name="weighted">Whether relative selection weight is relevant.</param>
        internal void Draw(OrderCatalog catalog, bool consumption, bool weighted)
        {
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(205f);
            serializedObject.Update();
            StudioFieldGUI.PropertyField(serializedObject.FindProperty("Name"));
            if (weighted)
                StudioFieldGUI.PropertyField(serializedObject.FindProperty("Weight"));
            if (consumption)
                DialogueConsumptionControls.Draw(serializedObject.FindProperty("Consumption"), "Filter Entry", catalog);
            SerializedProperty pages = serializedObject.FindProperty("Lines");
            pages.isExpanded = StudioArrayGUI.Foldout(pages, new GUIContent("Pages (" + pages.arraySize + ")", pages.tooltip));
            if (pages.isExpanded)
            {
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                for (int index = 0; index < pages.arraySize; index++)
                {
                    if (StudioArrayGUI.Header(pages, index, new GUIContent("Page " + (index + 1), "Speaker and complete page text.")))
                        break;
                    SerializedProperty page = pages.GetArrayElementAtIndex(index);
                    if (!page.isExpanded)
                        continue;
                    HoverControls.Field(page, "Speaker");
                    HoverControls.Field(page, "Text");
                }
                StudioArrayGUI.Add(pages, "Add Page", () => new DialogueLine());
            }
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
