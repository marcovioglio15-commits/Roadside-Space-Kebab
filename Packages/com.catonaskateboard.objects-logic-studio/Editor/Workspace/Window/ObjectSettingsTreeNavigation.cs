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
            return asset is ObjectGroupPreset or DialogueEntry
                or DialogueTree or DialogueTriggerPreset or OrderCatalog or ContactModificationDefinition or SpawnFlowPlan;
        }

        /// <summary>Navigates to an existing editor without editing any value from the canvas.</summary>
        /// <param name="state">Current object workspace.</param>
        /// <param name="owner">Object represented by the tree root.</param>
        /// <param name="node">Clicked option or thematic block.</param>
        internal static void Open(ObjectWorkspace state, GameObject owner, ObjectSettingsTreeLayout.Node node)
        {
            // Broad nodes open complete nested editors rather than a single selected interaction's fields.
            switch (node.Kind)
            {
                case ObjectSettingsTreeLayout.NodeKind.Root:
                    ObjectBranchWindow.Open(state, owner);
                    return;
                case ObjectSettingsTreeLayout.NodeKind.Interaction:
                    ObjectBranchWindow.Open(state, node.Branch.Feature.gameObject, node.Branch.Feature);
                    return;
            }
            if (node.Asset == null)
            {
                if (node.Group != null && node.Branch?.Feature != null && ObjectWorkspaceDrafts.Select(state, node.Branch.Feature, out _))
                {
                    ObjectGroupWindow.Open(state, node.Group, node.Path, node.Label);
                    ObjectsLogicStudioWindow.RefreshOpen();
                }
                return;
            }
            switch (node.Asset)
            {
                case ObjectGroupPreset preset:
                    ObjectGroupWindow.OpenPreset(preset);
                    break;
                case DialogueEntry entry:
                    DialogueEntryWindow.Open(entry);
                    break;
                case DialogueTriggerPreset trigger:
                    DialogueTriggerWindow.Open(trigger);
                    break;
                case DialogueTree tree:
                    DialogueTreeWindow.Open(tree);
                    break;
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
            StudioAssetWindow.FocusSetting(node.Asset, node.Path, node.Label);
        }

        #endregion

        #endregion
    }
}
