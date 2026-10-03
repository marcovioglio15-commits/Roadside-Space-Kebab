using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioIdentity;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Separates table bindings, product recipes and existing-feature ingredient requirements.</summary>
    internal static class AssemblyControls
    {
        #region State

        private static readonly InteractionChoiceCatalog choices = new InteractionChoiceCatalog();
        private static readonly System.Collections.Generic.HashSet<ObjectFlag> ingredientFlags = new System.Collections.Generic.HashSet<ObjectFlag>();

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Refreshes product feature names only when the edited hierarchy changes.</summary>
        /// <param name="target">Currently selected prefab branch.</param>
        internal static void Refresh(GameObject target)
        {
            // Product gates can only reference already configured interactions on this product branch.
            choices.Refresh(target, true);
        }

        #endregion

        #region Table

        /// <summary>Shows reusable table configuration and conditionally exposes sight layers.</summary>
        /// <param name="settings">Detached or preset table settings.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void DrawStation(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Product recipes are edited on their own prefab, avoiding nested interaction menus on the table.
            if (!sections.Draw("Assembly Table", "Link one configured product prefab and place its output relative to the table."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            SerializedProperty prefab = settings.FindPropertyRelative("ProductPrefab");
            prefab.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Product Prefab", prefab.tooltip),
                prefab.objectReferenceValue, typeof(GameObject), false);
            HoverControls.Field(settings, "Trigger");
            if (settings.FindPropertyRelative("Trigger").enumValueIndex == (int)AssemblyStationTrigger.InputAction)
            {
                HoverControls.Field(settings, "Distance");
                HoverControls.Field(settings, "RequireSight");
                if (settings.FindPropertyRelative("RequireSight").boolValue)
                    HoverControls.Field(settings, "Obstacles");
                HoverControls.Field(settings, "AcceptReturnedProduct");
            }
            else
            {
                if (settings.FindPropertyRelative("Trigger").enumValueIndex == (int)AssemblyStationTrigger.IngredientContact)
                    HoverControls.Field(settings, "ContactDuration");
                HoverControls.Field(settings, "ContactQueryInterval");
                HoverControls.Field(settings, "ContactTolerance");
                HoverControls.Field(settings, "IncludeTriggers");
            }
            HoverControls.Field(settings, "OutputPosition");
            HoverControls.Field(settings, "OutputRotation");
        }

        #endregion

        #region Product

        /// <summary>Edits recipe quantities, placement slots and ingredient-gated existing features.</summary>
        /// <param name="state">Workspace retaining exact menu and component identities.</param>
        /// <param name="draft">Stable-identity product proposal.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void DrawProduct(SerializedProperty draft, ObjectStudioSections sections, ObjectWorkspace state)
        {
            // Shared requirement rows use project flags and positive integer counts throughout the tool.
            SerializedProperty settings = draft.FindPropertyRelative("Settings");
            if (sections.Draw("Recipe", "Mandatory quantities determine completion; optional quantities limit additional ingredients."))
                using (new EditorGUI.IndentLevelScope())
                    FlagRequirementControls.Draw(settings.FindPropertyRelative("Ingredients"), "+ Add Ingredient", true);
            if (sections.Draw("Magnets", "One placement slot per allowed ingredient; edit their positions in the dedicated preview."))
                using (new EditorGUI.IndentLevelScope())
                    DrawMagnets(settings.FindPropertyRelative("Magnets"));
            RefreshIngredients(settings.FindPropertyRelative("Ingredients"));
            SerializedProperty completed = settings.FindPropertyRelative("CompletedAppearance");
            ItemAppearanceControls.Draw(completed);
            if (completed.isExpanded && completed.FindPropertyRelative("Meshes").arraySize > 0)
                using (new EditorGUI.IndentLevelScope())
                    HoverControls.Field(settings, "HideIngredients");
            if (sections.Draw("Product Interactions", "Unlisted interactions wait for completion. Override specific existing interactions to allow partial assembly."))
                using (new EditorGUI.IndentLevelScope())
                {
                    if (state.Target.Resolve().GetComponent<ObjectGrab>() != null)
                        HoverControls.Field(settings, "WaitForNextIngredient", "Wait for Ingredient");
                    DrawRules(settings.FindPropertyRelative("InteractionRules"), draft.FindPropertyRelative("TargetIds"), state);
                }
        }

        /// <summary>Edits named generic or flag-specific slots without exposing raw array sizes.</summary>
        /// <param name="magnets">Product-local placement slots.</param>
        internal static void DrawMagnets(SerializedProperty magnets)
        {
            // Prefab guides are visual references only and never become recipe identity.
            for (int index = 0; index < magnets.arraySize; index++)
            {
                SerializedProperty magnet = magnets.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        magnet.isExpanded = EditorGUILayout.Foldout(magnet.isExpanded,
                            new GUIContent((index + 1) + ". " + magnet.FindPropertyRelative("Name").stringValue, "Expand this magnet's placement and ingredient guide."), true);
                        if (GUILayout.Button(new GUIContent("−", "Remove this magnet."), GUILayout.Width(28f)))
                        {
                            magnets.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    if (magnet.isExpanded)
                        using (new EditorGUI.IndentLevelScope())
                            DrawMagnet(magnet);
                }
            }
            if (StudioButton.Draw(new GUIContent("+ Add Magnet", "Add one empty generic ingredient slot.")))
            {
                magnets.arraySize++;
                SerializedProperty magnet = magnets.GetArrayElementAtIndex(magnets.arraySize - 1);
                magnet.FindPropertyRelative("Name").stringValue = "Magnet " + magnets.arraySize;
                magnet.FindPropertyRelative("AnyIngredient").boolValue = true;
                magnet.FindPropertyRelative("Flags").arraySize = 0;
                magnet.FindPropertyRelative("Position").vector3Value = Vector3.zero;
                magnet.FindPropertyRelative("Rotation").vector3Value = Vector3.zero;
                magnet.FindPropertyRelative("Scale").vector3Value = Vector3.one;
                magnet.FindPropertyRelative("PreviewPrefab").objectReferenceValue = null;
                magnet.FindPropertyRelative("Order").intValue = 0;
                magnet.FindPropertyRelative("Appearance.Meshes").arraySize = 0;
                magnet.FindPropertyRelative("Appearance.Materials").arraySize = 0;
                magnet.isExpanded = true;
            }
        }

        /// <summary>Edits one slot from both the feature card and navigable preview.</summary>
        /// <param name="magnet">Selected placement slot.</param>
        internal static void DrawMagnet(SerializedProperty magnet)
        {
            // Flag restrictions are relevant only for dedicated ingredient slots.
            HoverControls.Field(magnet, "Name");
            HoverControls.Field(magnet, "AnyIngredient");
            HoverControls.Field(magnet, "Order");
            if (!magnet.FindPropertyRelative("AnyIngredient").boolValue)
                ObjectFlagSelector.Draw(magnet.FindPropertyRelative("Flags"));
            HoverControls.Field(magnet, "Position");
            HoverControls.Field(magnet, "Rotation");
            HoverControls.Field(magnet, "Scale");
            SerializedProperty prefab = magnet.FindPropertyRelative("PreviewPrefab");
            prefab.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Preview Prefab", prefab.tooltip),
                prefab.objectReferenceValue, typeof(GameObject), false);
            ItemAppearanceControls.Draw(magnet.FindPropertyRelative("Appearance"), true, prefab.objectReferenceValue as GameObject);
        }

        /// <summary>Connects ingredient requirements to named existing product interactions.</summary>
        /// <param name="rules">Per-interaction configuration rows.</param>
        /// <param name="identities">Parallel stable IDs used by draft recovery.</param>
        /// <param name="state">Workspace retaining this product's draft.</param>
        private static void DrawRules(SerializedProperty rules, SerializedProperty identities, ObjectWorkspace state)
        {
            // Each rule targets one existing feature; independent unlock owners still apply.
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        choices.Draw(identities.GetArrayElementAtIndex(index), "Interaction");
                        if (GUILayout.Button(new GUIContent("−", "Remove this override; the interaction will wait for completion."), GUILayout.Width(28f)))
                        {
                            rules.DeleteArrayElementAtIndex(index);
                            identities.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    HoverControls.Field(rule, "RequireComplete");
                    if (!rule.FindPropertyRelative("RequireComplete").boolValue)
                        HoverControls.Field(rule, "MinimumIngredients");
                    EditorGUILayout.LabelField(new GUIContent("Required Ingredients", "Additional ingredient quantities needed by the selected interaction."), EditorStyles.miniBoldLabel);
                    FlagRequirementControls.Draw(rule.FindPropertyRelative("Ingredients"), "+ Require Recipe Ingredient", false, DrawIngredient);
                }
            }
            if (StudioButton.Draw(new GUIContent("+ Configure Interaction", "Choose an interaction already configured on this product.")))
            {
                GenericMenu menu = new GenericMenu();
                long productId = state.Extended.ComponentId;
                GameObject productRoot = state.Target.Resolve();
                choices.Refresh(productRoot, true);
                choices.AddChoices(menu, state.Extended.Draft.AssemblyProduct.TargetIds,
                    (long identity) => AddRule(state, productRoot, productId, identity));
                menu.ShowAsContext();
            }
        }

        /// <summary>Appends a fully identified interaction rule after rechecking the active product.</summary>
        /// <param name="state">Workspace holding the recipe.</param>
        /// <param name="root">Prefab object that opened the menu.</param>
        /// <param name="productId">Exact product component that opened the menu.</param>
        /// <param name="identity">Existing interaction chosen from that product.</param>
        private static void AddRule(ObjectWorkspace state, GameObject root, long productId, long identity)
        {
            // Preserve pending magnet edits and reject menu callbacks from another prefab or interaction card.
            if (!state.Target.IsOpen || state.Target.Resolve() != root || state.Extended.Kind != ExtendedInteractionKind.AssemblyProduct
                || state.Extended.ComponentId != productId)
                return;
            AssemblyProductDraft draft = state.Extended.Draft.AssemblyProduct;
            if (Array.IndexOf(draft.TargetIds, identity) >= 0)
                return;
            Undo.RecordObject(state, "Configure product interaction");
            int index = draft.Settings.InteractionRules.Length;
            Array.Resize(ref draft.Settings.InteractionRules, index + 1);
            Array.Resize(ref draft.TargetIds, index + 1);
            draft.Settings.InteractionRules[index] = new AssemblyInteractionRule();
            draft.TargetIds[index] = identity;
            state.Persist();
        }

        /// <summary>Caches recipe flag references for selection validation.</summary>
        /// <param name="recipe">Current recipe rows.</param>
        private static void RefreshIngredients(SerializedProperty recipe)
        {
            // Recipe rows can accept alternatives; flatten their membership for the condition selector.
            ingredientFlags.Clear();
            for (int row = 0; row < recipe.arraySize; row++)
            {
                SerializedProperty flags = recipe.GetArrayElementAtIndex(row).FindPropertyRelative("Flags");
                for (int index = 0; index < flags.arraySize; index++)
                    if (flags.GetArrayElementAtIndex(index).objectReferenceValue is ObjectFlag flag)
                        ingredientFlags.Add(flag);
            }
        }

        /// <summary>Edits an ingredient condition and reports choices missing from the recipe.</summary>
        /// <param name="flag">Stored ingredient flag, retained when its recipe entry is removed.</param>
        private static void DrawIngredient(SerializedProperty flag)
        {
            // Keep the common searchable catalog; indicate selections absent from this recipe.
            ObjectFlagSelector.Draw(flag, new GUIContent("Recipe Ingredient", flag.tooltip));
            for (int index = 0; index < flag.arraySize; index++)
                if (flag.GetArrayElementAtIndex(index).objectReferenceValue is ObjectFlag selected && !ingredientFlags.Contains(selected))
                {
                    EditorGUILayout.LabelField("A selected flag is not in the recipe.", EditorStyles.miniLabel);
                    break;
                }
        }

        #endregion

        #endregion
    }
}
