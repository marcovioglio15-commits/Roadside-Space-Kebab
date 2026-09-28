using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Copies a complete configuration while retaining the selected input actions.</summary>
    internal static class PlayerConfigurationCopy
    {
        #region Methods

        #region Configuration Copies

        /// <summary>Copies every assigned preset and rewires the master before constructing the new player.</summary>
        /// <param name="source">Default master whose stored choices supply the initial values.</param>
        /// <param name="folder">New configuration folder owned by this creation attempt.</param>
        /// <returns>The saved independent master.</returns>
        internal static PlayerMasterPreset Copy(PlayerMasterPreset source, string folder)
        {
            // Null slots stay optional, and each copied asset retains its original values.
            PlayerMasterPreset master = Copy(source, folder, "Master");
            PlayerInputPreset input = Copy(source.InputPreset, folder, "Input");
            PlayerCreationUtility.SetReferences(master, ("bodyPreset", Copy(source.BodyPreset, folder, "Body")),
                ("inputPreset", input), ("locomotionPreset", Copy(source.LocomotionPreset, folder, "Locomotion")),
                ("cameraPreset", Copy(source.CameraPreset, folder, "Camera")), ("toolsPreset", Copy(source.ToolsPreset, folder, "Tools")));
            AssetDatabase.SaveAssetIfDirty(master);
            return master;
        }

        /// <summary>Copies one optional preset without invoking default reset logic on an existing asset.</summary>
        /// <typeparam name="T">Concrete preset type.</typeparam>
        /// <param name="source">Assigned source asset, or null for an empty slot.</param>
        /// <param name="folder">Owned destination folder.</param>
        /// <param name="name">Role-based asset filename.</param>
        /// <returns>The saved copy, or null for an unassigned module.</returns>
        private static T Copy<T>(T source, string folder, string name) where T : ScriptableObject
        {
            // The copy keeps values and external content references while gaining a new asset identity.
            if (source == null)
                return null;
            T copy = UnityEngine.Object.Instantiate(source);
            copy.name = name;
            AssetDatabase.CreateAsset(copy, folder + "/" + name + ".asset");
            return copy;
        }



        #endregion

        #endregion
    }
}
