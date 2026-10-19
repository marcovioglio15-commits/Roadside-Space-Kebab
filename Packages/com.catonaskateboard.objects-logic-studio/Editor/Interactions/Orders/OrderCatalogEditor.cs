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
                    Presentation(entry, false);
                    if (OrderSections.Draw(entry, "Quantity & Extraction", "Unit quantity, probability and required draws.",
                        "Quantity", "Weight", "MaximumDraws", "RandomRequiredDraws", "RequiredDrawRange", "MinimumDraws"))
                        using (new EditorGUI.IndentLevelScope())
                        {
                            HoverControls.Field(entry, "Quantity");
                            HoverControls.Field(entry, "Weight");
                            DrawLimits(entry);
                        }
                    if (OrderSections.Draw(entry, "Accepted Objects", "Flags required by the main order.", "Flags", "RequireAllFlags"))
                        using (new EditorGUI.IndentLevelScope())
                            Flags(entry, false);
                    if (OrderSections.Draw(entry, "Recipe Variants & Combos", "Independent recipe candidates and their combinations.",
                        "UseRecipeModifiers", "IncludeBaseOrder", "Variants", "Combos"))
                        using (new EditorGUI.IndentLevelScope())
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
                Presentation(variant, true);
                if (OrderSections.Draw(variant, "Variant Extraction", "Independent probability, quantity and draw limits.",
                    "Weight", "UseDrawLimits", "MaximumDraws", "RandomRequiredDraws", "RequiredDrawRange", "MinimumDraws", "UseQuantity", "Quantity"))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        HoverControls.Field(variant, "Weight");
                        HoverControls.Field(variant, "UseDrawLimits");
                        if (variant.FindPropertyRelative("UseDrawLimits").boolValue)
                        {
                            DrawLimits(variant);
                            HoverControls.Field(variant, "UseQuantity");
                            if (variant.FindPropertyRelative("UseQuantity").boolValue)
                                HoverControls.Field(variant, "Quantity");
                        }
                    }
                if (OrderSections.Draw(variant, "Variant Ingredients", "Required and forbidden ingredient flags.", "Flags", "RequireAllFlags", "ExcludedFlags"))
                    using (new EditorGUI.IndentLevelScope())
                        Flags(variant, true);
            }
            StudioArrayGUI.Add(variants, "Add Recipe Variant", () => new OrderRecipeVariant());
            if (OrderSections.Draw(entry, "Variant Combinations", "Combine independent variants while retaining each paragraph's own text style.", "Combos"))
                using (new EditorGUI.IndentLevelScope())
                    Combos(entry.FindPropertyRelative("Combos"));
        }

        /// <summary>Groups paragraph text and independent typography for both base orders and variants.</summary>
        /// <param name="entry">Order or variant containing board text.</param>
        /// <param name="variant">Whether to show the base-text append policy.</param>
        private static void Presentation(SerializedProperty entry, bool variant)
        {
            if (!OrderSections.Draw(entry, "Board Text", "Text and appearance used for this exact paragraph.",
                variant ? new[] { "Text", "TextStyle", "AppendToBaseText" } : new[] { "Text", "TextStyle" }))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(entry, "Text");
            if (variant)
                HoverControls.Field(entry, "AppendToBaseText", "Append Base Text");
            OrderTextControls.Draw(entry.FindPropertyRelative("TextStyle"));
        }

        /// <summary>Shares flag selection while keeping forbidden ingredients exclusive to variants.</summary>
        /// <param name="entry">Order or variant filters.</param>
        /// <param name="variant">Whether additive and subtractive ingredient flags apply.</param>
        private static void Flags(SerializedProperty entry, bool variant)
        {
            ObjectFlagSelector.Draw(entry.FindPropertyRelative("Flags"));
            if (entry.FindPropertyRelative("Flags").arraySize > 1)
                HoverControls.Field(entry, "RequireAllFlags");
            if (variant)
                ObjectFlagSelector.Draw(entry.FindPropertyRelative("ExcludedFlags"),
                    new GUIContent("Subtractive Flags", "None of these flags may belong to the assembled ingredients."));
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
