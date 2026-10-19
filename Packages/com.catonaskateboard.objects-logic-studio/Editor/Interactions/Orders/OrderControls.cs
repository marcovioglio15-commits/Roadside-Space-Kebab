using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns a shared order catalog and local extraction settings.</summary>
    internal static class OrderControls
    {
        #region Methods
        #region Drawing

        /// <summary>Edits the board, extraction budget and shared catalog reference.</summary>
        /// <param name="settings">Serialized detached order proposal.</param>
        /// <param name="owner">Current prefab object.</param>
        /// <param name="sections">Retained workspace section expansion.</param>
        internal static void Draw(SerializedProperty settings, GameObject owner, ObjectStudioSections sections)
        {
            // Scene links use a stable board ID because prefab assets cannot store scene component references.
            if (sections.Draw("Order Board & Reveal", "Board destination and the optional interaction that reveals the orders.", settings,
                "Board", "WaitForCompletion", "CompletionSource"))
                using (new EditorGUI.IndentLevelScope())
                {
                    OrderBoardBinding.Draw(settings.FindPropertyRelative("Board"));
                    Completion(settings, owner);
                }
            // Hide the interaction-level mode when every reachable catalog defines its own pool policy.
            bool fallbackSelection = UsesFallbackSelection(settings);
            string[] selectionFields = fallbackSelection
                ? new[] { "RandomDrawUnits", "DrawUnitRange", "DrawCount", "Selection" }
                : new[] { "RandomDrawUnits", "DrawUnitRange", "DrawCount" };
            if (sections.Draw("Order Selection", "Draw budget and selection policy.", settings, selectionFields))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "RandomDrawUnits");
                    HoverControls.Field(settings, settings.FindPropertyRelative("RandomDrawUnits").boolValue ? "DrawUnitRange" : "DrawCount", "Draw Units");
                    if (fallbackSelection)
                        HoverControls.Field(settings, "Selection");
                }
            if (sections.Draw("Order Catalog", "Shared order definitions and optional per-spawn catalog overrides.", settings, "Catalog", "UseSpawnCatalogs", "SpawnCatalogs"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Catalog(settings.FindPropertyRelative("Catalog"));
                    SpawnCatalogs(settings);
                }
            if (sections.Draw("Order Timer", "Elapsed time from order reveal until Day Flow completes the order dialogue.", settings,
                "UseTimer", "TimerLabel", "TimerTenths", "KeepTimerResult"))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "UseTimer", "Enabled");
                    if (settings.FindPropertyRelative("UseTimer").boolValue)
                        SurfaceInteractionControls.Fields(settings, "TimerLabel", "TimerTenths", "KeepTimerResult");
                }
        }

        /// <summary>Shows each spawn override only when catalog selection depends on its origin.</summary>
        /// <param name="settings">Detached available-orders configuration.</param>
        private static void SpawnCatalogs(SerializedProperty settings)
        {
            HoverControls.Field(settings, "UseSpawnCatalogs");
            if (settings.FindPropertyRelative("UseSpawnCatalogs").boolValue)
            {
                SerializedProperty rules = settings.FindPropertyRelative("SpawnCatalogs");
                for (int index = 0; index < rules.arraySize; index++)
                {
                    SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                    if (StudioArrayGUI.Header(rules, index, new GUIContent("Spawn Catalog " + (index + 1), rules.tooltip)))
                        break;
                    if (!rule.isExpanded)
                        continue;
                    using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                    HoverControls.Field(rule, "Match");
                    HoverControls.Field(rule, "Spawn");
                    Catalog(rule.FindPropertyRelative("Catalog"));
                }
                StudioArrayGUI.Add(rules, "Add Spawn Catalog", () => new OrderSpawnCatalog());
            }
        }

        /// <summary>Shows the shared fallback mode only when some assigned catalog has no custom pools.</summary>
        /// <param name="settings">Available-orders configuration, including enabled spawn overrides.</param>
        /// <returns>True when a reachable catalog uses the interaction-level selection mode.</returns>
        private static bool UsesFallbackSelection(SerializedProperty settings)
        {
            // The default catalog is used whenever no exact or threshold spawn rule applies.
            if (UsesFallbackSelection((OrderCatalog)settings.FindPropertyRelative("Catalog").objectReferenceValue))
                return true;
            if (!settings.FindPropertyRelative("UseSpawnCatalogs").boolValue)
                return false;
            SerializedProperty rules = settings.FindPropertyRelative("SpawnCatalogs");
            // Spawn overrides can still rely on this mode even when the default catalog has custom pools.
            for (int index = 0; index < rules.arraySize; index++)
                if (UsesFallbackSelection((OrderCatalog)rules.GetArrayElementAtIndex(index).FindPropertyRelative("Catalog").objectReferenceValue))
                    return true;
            return false;
        }

        /// <summary>Checks whether one catalog delegates candidate selection to the interaction.</summary>
        /// <param name="catalog">Default or spawn-specific catalog.</param>
        /// <returns>True for a missing catalog or a catalog without explicit pools.</returns>
        private static bool UsesFallbackSelection(OrderCatalog catalog)
        {
            return catalog == null || catalog.Pools == null || catalog.Pools.Length == 0;
        }

        /// <summary>Shares catalog assignment and independent edit/create commands across default and spawn overrides.</summary>
        /// <param name="property">Catalog reference in the current detached proposal.</param>
        private static void Catalog(SerializedProperty property)
        {
            StudioFieldGUI.PropertyField(property);
            OrderCatalog catalog = (OrderCatalog)property.objectReferenceValue;
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(catalog == null))
                    if (StudioButton.Draw(new GUIContent("Edit Catalog", "Edit shared order definitions in their own Apply/Discard session.")))
                        OrderCatalogWindow.Open(catalog);
                if (StudioButton.Draw(new GUIContent("New Catalog", "Create a reusable order catalog asset.")))
                    Create(property);
            }
        }

        /// <summary>Selects an exact local completion source while retaining portable preset mappings.</summary>
        /// <param name="settings">Order configuration being edited.</param>
        /// <param name="owner">Applied object, or null for a preset.</param>
        private static void Completion(SerializedProperty settings, GameObject owner)
        {
            HoverControls.Field(settings, "WaitForCompletion");
            if (!settings.FindPropertyRelative("WaitForCompletion").boolValue)
                return;
            ObjectWorkspace workspace = settings.serializedObject.targetObject as ObjectWorkspace;
            AvailableOrdersPreset preset = settings.serializedObject.targetObject as AvailableOrdersPreset;
            owner = owner != null ? owner : HierarchyPathMenu.Source(settings, true);
            SerializedProperty selection = workspace != null ? settings.serializedObject.FindProperty("Extended.Draft.OrdersCompletionId")
                : preset != null ? settings.serializedObject.FindProperty("Completion") : settings.FindPropertyRelative("CompletionSource");
            ObjectInteraction current = workspace != null ? workspace.Extended.Draft.ResolveOrders(owner).CompletionSource
                : preset == null ? (ObjectInteraction)selection.objectReferenceValue : null;
            string label = current != null ? current.InteractionName : preset is { Completion: { Assigned: true } } ? preset.Completion.Name : "Select Interaction";
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, selection);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(selection));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("After Completion", "Reveal the drawn orders after this local interaction completes."));
            using EditorGUI.DisabledScope unavailable = new EditorGUI.DisabledScope(owner == null);
            if (!GUI.Button(rect, label, EditorStyles.popup))
                return;
            // Only build the candidate list when its menu is opened.
            UnityEngine.Object target = selection.serializedObject.targetObject;
            string path = selection.propertyPath;
            System.Func<bool> guard = StudioFieldMenu.Guard(target, path);
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            foreach (ObjectInteraction candidate in owner.GetComponents<ObjectInteraction>())
            {
                if (candidate is ObjectAvailableOrders or ObjectInteractionUnlock)
                    continue;
                long identity = ObjectWorkspaceTarget.FileId(candidate);
                InteractionTemplateReference mapping = preset != null ? UnlockPresetMapping.Capture(owner.transform, candidate) : null;
                menu.AddItem(new GUIContent(candidate.InteractionName), candidate == current, () =>
                {
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(target);
                    SerializedProperty field = data.FindProperty(path);
                    if (workspace != null)
                        field.longValue = identity;
                    else if (preset != null)
                        field.boxedValue = mapping;
                    else
                        field.objectReferenceValue = candidate;
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(target);
                });
            }
            menu.DropDown(rect);
        }

        /// <summary>Creates and assigns an empty catalog without changing the applied interaction.</summary>
        /// <param name="property">Draft catalog reference receiving the new asset.</param>
        private static void Create(SerializedProperty property)
        {
            // Cancelling the picker leaves the interaction unchanged.
            string path = EditorUtility.SaveFilePanelInProject("New Order Catalog", "Order Catalog", "asset", "Choose where to save the catalog.");
            if (string.IsNullOrEmpty(path))
                return;
            OrderCatalog catalog = ScriptableObject.CreateInstance<OrderCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(catalog, "Create Order Catalog");
            property.objectReferenceValue = catalog;
            OrderCatalogWindow.Open(catalog);
        }

        #endregion
        #endregion
    }
}
