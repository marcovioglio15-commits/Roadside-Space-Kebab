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
            HoverControls.Field(settings, "DrawCount", "Draw Units");
            HoverControls.Field(settings, "Selection");
            HoverControls.Field(settings, "Catalog");
            OrderCatalog catalog = (OrderCatalog)settings.FindPropertyRelative("Catalog").objectReferenceValue;
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(catalog == null))
                    if (StudioButton.Draw(new GUIContent("Edit Catalog", "Edit shared order definitions in their own Apply/Discard session.")))
                        OrderCatalogWindow.Open(catalog);
                if (StudioButton.Draw(new GUIContent("New Catalog", "Create a reusable order catalog asset.")))
                    Create(settings.FindPropertyRelative("Catalog"));
            }
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
