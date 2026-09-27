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

        /// <summary>Validates tool selection against the player's initial action map and configured identities.</summary>
        /// <param name="master">Master or detached validation copy.</param>
        /// <param name="warning">Receives a missing tool or incompatible action ownership.</param>
        /// <returns>True when all active tool roles can resolve in this player.</returns>
        internal static bool ValidateInput(PlayerMasterPreset master, out string warning)
        {
            // Shared Input presets may be used by players without a Tools module.
            PlayerToolInputSettings settings = master.InputPreset.Tools;
            if (!settings.TryValidate(out warning) || master.ToolsPreset == null)
                return warning.Length == 0;
            using SerializedObject data = new SerializedObject(master.InputPreset);
            InputActionReference movement = (InputActionReference)data.FindProperty("movementAction").objectReferenceValue;
            warning = "Use Tool actions must share the initial movement map and reference tools assigned to this master.";
            if (settings.Mode == PlayerToolInputMode.SharedCycle)
            {
                if (settings.UseTool != null && settings.UseTool.action.actionMap != movement.action.actionMap)
                    return false;
            }
            else
                foreach (PlayerToolInputBinding binding in settings.Bindings)
                    if (master.ToolsPreset.IndexOf(binding.Tool) < 0 || binding.Action.action.actionMap != movement.action.actionMap)
                        return false;
            warning = string.Empty;
            return true;
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
