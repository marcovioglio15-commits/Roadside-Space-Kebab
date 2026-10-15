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
                    DrawLimits(entry);
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
                HoverControls.Field(variant, "AppendToBaseText", "Append Base Text");
                HoverControls.Field(variant, "Weight");
                HoverControls.Field(variant, "UseDrawLimits");
                if (variant.FindPropertyRelative("UseDrawLimits").boolValue)
                {
                    DrawLimits(variant);
                    HoverControls.Field(variant, "UseQuantity");
                    if (variant.FindPropertyRelative("UseQuantity").boolValue)
                        HoverControls.Field(variant, "Quantity");
                }
                ObjectFlagSelector.Draw(variant.FindPropertyRelative("Flags"), new GUIContent("Additive Flags", "Ingredient flags required by this assembled variant."));
                if (variant.FindPropertyRelative("Flags").arraySize > 1)
                    HoverControls.Field(variant, "RequireAllFlags");
                ObjectFlagSelector.Draw(variant.FindPropertyRelative("ExcludedFlags"), new GUIContent("Subtractive Flags", "None of these flags may belong to the assembled ingredients."));
            }
            StudioArrayGUI.Add(variants, "Add Recipe Variant", () => new OrderRecipeVariant());
            Combos(entry.FindPropertyRelative("Combos"));
        }

        /// <summary>Exposes only the policies used by enabled variant combinations.</summary>
        /// <param name="combos">Combination configuration owned by one base order.</param>
        private static void Combos(SerializedProperty combos)
        {
            HoverControls.Field(combos, "Enabled", "Allow Variant Combos");
            if (!combos.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(combos, "MaximumVariants");
            HoverControls.Field(combos, "Weight", "Combo Pool Weight");
            HoverControls.Field(combos, "QuantityPolicy");
            if (combos.FindPropertyRelative("QuantityPolicy").enumValueIndex == (int)OrderComboPolicy.Fixed)
                HoverControls.Field(combos, "Quantity");
            HoverControls.Field(combos, "DrawLimitsPolicy", "Shared Draw Limits");
            if (combos.FindPropertyRelative("DrawLimitsPolicy").enumValueIndex == (int)OrderComboPolicy.Fixed)
                DrawLimits(combos);
        }

        /// <summary>Uses identical draw-limit controls for parent orders and independent variants.</summary>
        /// <param name="definition">Order or variant containing the limit fields.</param>
        private static void DrawLimits(SerializedProperty definition)
        {
            HoverControls.Field(definition, "MaximumDraws");
            HoverControls.Field(definition, "RandomRequiredDraws", "Random Minimum");
            HoverControls.Field(definition, definition.FindPropertyRelative("RandomRequiredDraws").boolValue ? "RequiredDrawRange" : "MinimumDraws");
        }

        #endregion
        #endregion
    }
}
