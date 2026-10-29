using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Opens blocks in their existing group or specialized preset editor.</summary>
    internal static class ObjectSettingsTreeNavigation
    {
        #region Methods

        #region Navigation

        /// <summary>Limits reference expansion to settings assets with a meaningful editor.</summary>
        /// <param name="asset">Referenced object.</param>
        /// <returns>True for supported settings presets.</returns>
        internal static bool Supports(ScriptableObject asset)
        {
            return asset is SingleInteractionPreset or ExtendedInteractionPreset or HoverPreset or DialogueEntry
                or DialogueTree or OrderCatalog or ContactModificationDefinition or SpawnFlowPlan;
        }

        /// <summary>Navigates to an existing editor without editing any value from the canvas.</summary>
        /// <param name="state">Current object workspace.</param>
        /// <param name="node">Clicked option or thematic block.</param>
        internal static void Open(ObjectWorkspace state, ObjectSettingsTreeLayout.Node node)
        {
            if (node.Asset == null)
            {
                if (node.Group != null)
                    ObjectGroupWindow.Open(state, node.Group, node.Path);
                return;
            }
            switch (node.Asset)
            {
                case DialogueEntry entry:
                    DialogueEntryWindow.Open(entry);
                    break;
#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
                case DialogueTree tree:
                    DialogueTreeWindow.Open(tree);
                    break;
#endif
                case OrderCatalog orders:
                    OrderCatalogWindow.Open(orders);
                    break;
                case ContactModificationDefinition contact:
                    ContactModificationWindow.Open(contact);
                    break;
                case SpawnFlowPlan flow:
                    SpawnFlowPlanWindow.Open(flow);
                    break;
                default:
                    Selection.activeObject = node.Asset;
                    EditorGUIUtility.PingObject(node.Asset);
                    break;
            }
            StudioAssetWindow.FocusSetting(node.Asset, node.Path);
        }

        #endregion

        #endregion
    }
}
