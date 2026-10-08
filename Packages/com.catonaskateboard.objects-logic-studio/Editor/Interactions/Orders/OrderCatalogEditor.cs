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
                    Variants(entry);
                }
            StudioArrayGUI.Add(entries, "Add Available Order", () => new OrderEntry { Name = "Order " + entries.arraySize });
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Edits optional recipe candidates immediately after the parent identity filters.</summary>
        /// <param name="entry">Parent order retaining shared quantity and identity settings.</param>
        private static void Variants(SerializedProperty entry)
        {
            HoverControls.Field(entry, "UseRecipeModifiers");
            if (!entry.FindPropertyRelative("UseRecipeModifiers").boolValue)
                return;
            HoverControls.Field(entry, "IncludeBaseOrder");
            SerializedProperty variants = entry.FindPropertyRelative("Variants");
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            for (int index = 0; index < variants.arraySize; index++)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(variants, index, new GUIContent(variant.FindPropertyRelative("Name").stringValue, "Recipe-specific order candidate and independent dialogue selection.")))
                    break;
                if (!variant.isExpanded)
                    continue;
                HoverControls.Field(variant, "Name");
                HoverControls.Field(variant, "Text");
                HoverControls.Field(variant, "Weight");
                ObjectFlagSelector.Draw(variant.FindPropertyRelative("Flags"));
                if (variant.FindPropertyRelative("Flags").arraySize > 1)
                    HoverControls.Field(variant, "RequireAllFlags");
            }
            StudioArrayGUI.Add(variants, "Add Recipe Variant", () => new OrderRecipeVariant());
        }

        #endregion
        #endregion
    }
}
