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
            OrderCatalog catalog = (OrderCatalog)target;
            SerializedProperty entries = serializedObject.FindProperty("Entries");
            for (int index = 0; index < entries.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (StudioArrayGUI.Header(entries, index, new GUIContent(entry.FindPropertyRelative("Name").stringValue, "Available order candidate and its unit quantity.")))
                        break;
                    if (!entry.isExpanded)
                        continue;
                    using StudioIndentScope indent = new StudioIndentScope();
                    HoverControls.Field(entry, "Name");
                    Presentation(entry, false);
                    HoverControls.Field(entry, "OverridePrice");
                    if (entry.FindPropertyRelative("OverridePrice").boolValue)
                        HoverControls.Field(entry, "Price");
                    if (OrderSections.Draw(entry, "Quantity & Extraction", "Unit quantity, probability and required draws.",
                        "Quantity", "Weight", "MaximumDraws", "RandomRequiredDraws", "RequiredDrawRange", "MinimumDraws"))
                        using (new StudioIndentScope())
                        {
                            HoverControls.Field(entry, "Quantity");
                            HoverControls.Field(entry, "Weight");
                            DrawLimits(entry);
                        }
                    if (OrderSections.Draw(entry, "Accepted Objects", "Flags required by the main order.", "Flags", "RequireAllFlags"))
                        using (new StudioIndentScope())
                            Flags(entry, false);
                    if (OrderSections.Draw(entry, "Recipe Variants & Combos", "Independent recipe candidates and their combinations.",
                        "UseRecipeModifiers", "IncludeBaseOrder", "Variants", "Combos"))
                        using (new StudioIndentScope())
                            Variants(entry);
                }
            StudioArrayGUI.Add(entries, "Add Available Order", () => new OrderEntry { Name = "Order " + entries.arraySize });
            DrawPools(catalog, serializedObject.FindProperty("Pools"));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Authors dynamic pools that can reference any base order, variant or generated combo.</summary>
        /// <param name="catalog">Detached catalog currently edited in the order window.</param>
        /// <param name="pools">Serialized custom extraction pools.</param>
        private static void DrawPools(OrderCatalog catalog, SerializedProperty pools)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, pools);
            pools.isExpanded = EditorGUI.Foldout(rect, pools.isExpanded,
                new GUIContent("Extraction Pools", "Choose a pool first, then draw one of its named candidates."), true);
            if (!pools.isExpanded)
                return;
            string[] candidateNames = System.Linq.Enumerable.ToArray(catalog.CandidateNames());
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < pools.arraySize; index++)
            {
                SerializedProperty pool = pools.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(pools, index, new GUIContent(pool.FindPropertyRelative("Name").stringValue,
                    "Pool chance and its internal candidate list.")))
                    break;
                if (!pool.isExpanded)
                    continue;
                using EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                using StudioIndentScope poolIndent = new StudioIndentScope();
                HoverControls.Field(pool, "Name");
                HoverControls.Field(pool, "Weight", "Pool Weight");
                HoverControls.Field(pool, "Selection");
                SerializedProperty members = pool.FindPropertyRelative("Candidates");
                // Each row selects a stable catalog name; generated combo names appear with the other candidates.
                for (int memberIndex = 0; memberIndex < members.arraySize; memberIndex++)
                {
                    SerializedProperty member = members.GetArrayElementAtIndex(memberIndex);
                    if (StudioArrayGUI.Header(members, memberIndex, new GUIContent("Candidate " + (memberIndex + 1),
                        member.FindPropertyRelative("Candidate").stringValue)))
                        break;
                    if (!member.isExpanded)
                        continue;
                    using StudioIndentScope memberIndent = new StudioIndentScope();
                    SerializedProperty candidate = member.FindPropertyRelative("Candidate");
                    int selected = System.Array.IndexOf(candidateNames, candidate.stringValue);
                    if (candidateNames.Length > 0)
                    {
                        int next = EditorGUILayout.Popup(new GUIContent("Candidate", candidate.tooltip), selected, candidateNames);
                        if (next >= 0)
                            candidate.stringValue = candidateNames[next];
                    }
                    else
                        EditorGUILayout.LabelField("Candidate", "Add an order or variant first.");
                    if (pool.FindPropertyRelative("Selection").enumValueIndex == (int)OrderSelection.WeightedRandom)
                        HoverControls.Field(member, "Weight", "Candidate Weight");
                }
                StudioArrayGUI.Add(members, "Add Pool Candidate", () => new OrderPoolEntry
                {
                    Candidate = candidateNames.Length > 0 ? candidateNames[0] : string.Empty
                });
            }
            StudioArrayGUI.Add(pools, "Add Extraction Pool", () => new OrderPool { Name = "Pool " + (pools.arraySize + 1) });
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
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < variants.arraySize; index++)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(variants, index, new GUIContent(variant.FindPropertyRelative("Name").stringValue, "Recipe-specific order candidate and independent dialogue selection.")))
                    break;
                if (!variant.isExpanded)
                    continue;
                using StudioIndentScope variantIndent = new StudioIndentScope();
                HoverControls.Field(variant, "Name");
                Presentation(variant, true);
                if (OrderSections.Draw(variant, "Variant Extraction", "Independent probability, quantity and draw limits.",
                    "Weight", "UseDrawLimits", "MaximumDraws", "RandomRequiredDraws", "RequiredDrawRange", "MinimumDraws", "UseQuantity", "Quantity"))
                    using (new StudioIndentScope())
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
                    using (new StudioIndentScope())
                        Flags(variant, true);
            }
            StudioArrayGUI.Add(variants, "Add Recipe Variant", () => new OrderRecipeVariant());
            if (OrderSections.Draw(entry, "Variant Combinations", "Combine independent variants while retaining each paragraph's own text style.", "Combos"))
                using (new StudioIndentScope())
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
            using StudioIndentScope indent = new StudioIndentScope();
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
            HoverControls.Field(combos, "Weight", "Default Combo Weight");
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
