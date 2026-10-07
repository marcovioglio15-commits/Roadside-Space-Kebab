using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits reusable named order candidates with safe per-element array controls.</summary>
    [CustomEditor(typeof(OrderCatalog))]
    internal sealed class OrderCatalogEditor : UnityEditor.Editor
    {
        #region Methods
        #region Drawing

        /// <summary>Draws the detached proposal in its dedicated window or opens that window from the Inspector.</summary>
        public override void OnInspectorGUI()
        {
            // Persistent assets are edited only through their own Apply/Discard session.
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Edit Order Catalog", "Open an independent editing session for this shared catalog.")))
                    OrderCatalogWindow.Open((OrderCatalog)target);
                return;
            }
            serializedObject.Update();
            SerializedProperty entries = serializedObject.FindProperty("Entries");
            for (int index = 0; index < entries.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (StudioArrayGUI.Header(entries, index, new GUIContent(entry.FindPropertyRelative("Name").stringValue, "Available order candidate and its unit quantity.")))
                        break;
                    if (!entry.isExpanded)
                        continue;
                    using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                    HoverControls.Field(entry, "Name");
                    HoverControls.Field(entry, "Text");
                    HoverControls.Field(entry, "Quantity");
                    HoverControls.Field(entry, "Weight");
                    ObjectFlagSelector.Draw(entry.FindPropertyRelative("Flags"));
                    if (entry.FindPropertyRelative("Flags").arraySize > 1)
                        HoverControls.Field(entry, "RequireAllFlags");
                }
            StudioArrayGUI.Add(entries, "Add Available Order", () => new OrderEntry { Name = "Order " + entries.arraySize });
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
