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
        internal static void Draw(SerializedProperty settings, GameObject owner)
        {
            // Scene links use a stable board ID because prefab assets cannot store scene component references.
            OrderBoardBinding.Draw(settings.FindPropertyRelative("Board"));
            HoverControls.Field(settings, "RandomDrawUnits");
            HoverControls.Field(settings, settings.FindPropertyRelative("RandomDrawUnits").boolValue ? "DrawUnitRange" : "DrawCount", "Draw Units");
            HoverControls.Field(settings, "Selection");
            Catalog(settings.FindPropertyRelative("Catalog"));
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
            Completion(settings, owner);
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
