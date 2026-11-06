using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEditor;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Checks the input presets that can equip a dispenser's specific tool.</summary>
    [InitializeOnLoad]
    internal static class DispenserToolActions
    {
        #region State

        private static List<PlayerMasterPreset> presets;

        #endregion

        #region Methods

        #region Lookup

        /// <summary>Invalidates the preset catalog when project assets change.</summary>
        static DispenserToolActions()
        {
            EditorApplication.projectChanged += () => presets = null;
        }

        /// <summary>Checks whether an applicable player preset supplies a different Use Tool action.</summary>
        /// <param name="targeting">Targeting action configured on the dispenser card.</param>
        /// <param name="tool">Specific tool configured on the card.</param>
        /// <returns>True when at least one preset can supply distinct actions from the same input asset.</returns>
        internal static bool HasDistinctAction(InputActionReference targeting, PlayerTool tool)
        {
            if (targeting == null || targeting.action == null)
                return false;
            // Cache only asset references so changes to the input or tool selection remain visible.
            if (presets == null)
            {
                presets = new List<PlayerMasterPreset>();
                foreach (string guid in AssetDatabase.FindAssets("t:PlayerMasterPreset"))
                    if (AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>(AssetDatabase.GUIDToAssetPath(guid)) is PlayerMasterPreset preset)
                        presets.Add(preset);
            }
            // Only a player that can equip this tool and bind both actions can expose the split.
            foreach (PlayerMasterPreset preset in presets)
            {
                if (preset == null || preset.ToolsPreset == null || preset.ToolsPreset.IndexOf(tool) < 0)
                    continue;
                InputActionReference activate = preset.InputPreset != null ? preset.InputPreset.Tools?.Activate : null;
                if (activate != null && activate.asset == targeting.asset && activate.action is { type: InputActionType.Button } action
                    && action.id != targeting.action.id)
                    return true;
            }
            return false;
        }

        #endregion

        #endregion
    }
}
