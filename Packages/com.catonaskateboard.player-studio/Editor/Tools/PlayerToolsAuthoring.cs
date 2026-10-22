using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Checks cross-module tool input and creates explicitly requested tool assets.</summary>
    internal static class PlayerToolsAuthoring
    {
        #region Methods

        #region Validation

        /// <summary>Resolves selection visibility and validation from the same pending Tools proposal.</summary>
        /// <param name="state">Workspace containing pending master and module changes.</param>
        /// <returns>True unless the resulting Tools preset enables single-tool mode.</returns>
        internal static bool AllowsSwitching(PlayerStudioState state)
        {
            // A replaced master slot takes priority over an unrelated retained module draft.
            PlayerToolsPreset tools = state.Selection.MasterSession.HasChanges ? state.Selection.MasterSession.ToolsPreset
                : state.Selection.Master?.ToolsPreset;
            if (tools != null && state.Tools.Source == tools && state.Tools.GetEditor()?.targetObject is PlayerToolsPreset draft)
                tools = draft;
            return tools == null || !tools.SingleTool;
        }

        /// <summary>Validates tool selection against the player's initial action map and configured identities.</summary>
        /// <param name="master">Master or detached validation copy.</param>
        /// <param name="warning">Receives a missing tool or incompatible action ownership.</param>
        /// <returns>True when all active tool roles can resolve in this player.</returns>
        internal static bool ValidateInput(PlayerMasterPreset master, out string warning)
        {
            // Shared Input presets may be used by players without a Tools module.
            PlayerToolInputSettings settings = master.InputPreset.Tools;
            bool switching = master.ToolsPreset == null || !master.ToolsPreset.SingleTool;
            if (!settings.TryValidate(out warning, switching) || master.ToolsPreset == null)
                return warning.Length == 0;
            using SerializedObject data = new SerializedObject(master.InputPreset);
            InputActionReference movement = (InputActionReference)data.FindProperty("movementAction").objectReferenceValue;
            warning = "Tool actions must share the initial movement map and reference tools assigned to this master.";
            if (movement == null || movement.action == null)
                return false;
            if (!SameMap(settings.Activate, movement) || !SameMap(settings.Reload, movement))
                return false;
            if (!switching)
            {
                warning = string.Empty;
                return true;
            }
            if (settings.Mode == PlayerToolInputMode.SharedCycle)
            {
                if (!SameMap(settings.UseTool, movement))
                    return false;
            }
            else
                foreach (PlayerToolInputBinding binding in settings.Bindings)
                    if (master.ToolsPreset.IndexOf(binding.Tool) < 0 || binding.Action.action.actionMap != movement.action.actionMap)
                        return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks optional tool commands against the initial player map.</summary>
        /// <param name="reference">Optional command role.</param>
        /// <param name="movement">Validated movement role defining the player's initial map.</param>
        /// <returns>True for an unassigned role or an action from the same asset and map.</returns>
        private static bool SameMap(InputActionReference reference, InputActionReference movement)
        {
            return reference == null || reference.action != null && reference.asset == movement.asset
                && reference.action.actionMap == movement.action.actionMap;
        }

        #endregion

        #region Assets

        /// <summary>Creates a new asset only after an explicit save location is selected.</summary>
        /// <typeparam name="T">Tool or animation preset type.</typeparam>
        /// <param name="name">Suggested asset name.</param>
        /// <returns>The saved asset, or null when canceled.</returns>
        internal static T Create<T>(string name) where T : ScriptableObject
        {
            // Unique paths protect existing assets from accidental replacement.
            string path = EditorUtility.SaveFilePanelInProject("Create " + name, name, "asset", "Choose where to save this preset.");
            if (string.IsNullOrEmpty(path))
                return null;
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(asset, "Create " + name);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        #endregion

        #endregion
    }
}
