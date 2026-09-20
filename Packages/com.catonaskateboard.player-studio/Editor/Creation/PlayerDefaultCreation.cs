using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Creates a complete default player with its own prefab and editable configuration copies.</summary>
    internal static class PlayerDefaultCreation
    {
        #region Methods

        #region Destination

        /// <summary>Asks for the new prefab destination before creating any player or configuration assets.</summary>
        /// <param name="scene">Loaded scene receiving the new instance.</param>
        /// <param name="host">Receives the created player after all assets have been saved.</param>
        /// <param name="warning">Receives a validation or creation problem; empty on cancellation.</param>
        /// <returns>True when creation completed.</returns>
        internal static bool Prompt(Scene scene, out PlayerHost host, out string warning)
        {
            // The generated content folder is the default destination; the file picker can choose another project folder.
            host = null;
            warning = string.Empty;
            PlayerMasterPreset defaults = PlayerDefaultAssets.Ensure();
            if (defaults == null)
            {
                warning = "Default configuration is still being imported. Retry when package import finishes.";
                return false;
            }
            string folder = PlayerDefaultAssets.ContentRoot + "/Players";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(PlayerDefaultAssets.ContentRoot, "Players");
            string path = EditorUtility.SaveFilePanel("Create Default Player", Path.GetFullPath(folder), "Player", "prefab");
            if (string.IsNullOrEmpty(path))
                return false;
            return TryCreate(defaults, scene, FileUtil.GetProjectRelativePath(path), out host, out warning);
        }

        /// <summary>Creates independent preset and action copies, saves their player prefab, and places one instance with Undo.</summary>
        /// <param name="defaults">Saved default configuration to copy without altering its values.</param>
        /// <param name="scene">Loaded destination scene, including an unsaved scene.</param>
        /// <param name="path">New prefab path inside Assets or the editable content package.</param>
        /// <param name="host">Receives the complete scene player.</param>
        /// <param name="warning">Receives a rejected destination or construction failure.</param>
        /// <returns>True when prefab, configuration and instance are ready.</returns>
        internal static bool TryCreate(PlayerMasterPreset defaults, Scene scene, string path,
            out PlayerHost host, out string warning)
        {
            // Reject existing destinations so a failed creation can remove only files it owns.
            host = null;
            warning = string.Empty;
            path = (path ?? string.Empty).Replace('\\', '/');
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded
                || EditorSceneManager.IsPreviewScene(scene))
                warning = "Choose a loaded scene in Edit mode before creating a player.";
            else if (!(path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.StartsWith(PlayerDefaultAssets.ContentRoot + "/", StringComparison.Ordinal))
                || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsValidFolder(parent))
                warning = "Choose a prefab destination inside Assets or Player Studio Content.";
            else if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                warning = "A prefab already exists at that path. Choose a new name.";
            if (warning.Length > 0 || !PlayerCreationUtility.TryValidate(defaults, out _, out warning))
                return false;

            GameObject root = null;
            string configuration = string.Empty;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                // Each new player owns its presets and input bindings; shared visual source assets keep their authored content.
                string name = Path.GetFileNameWithoutExtension(path);
                configuration = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + name + " Configuration");
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(configuration))))
                    throw new IOException("Unity could not create the configuration folder.");
                PlayerMasterPreset master = PlayerConfigurationCopy.Copy(defaults, configuration);
                root = PlayerCreationUtility.Create(master, scene, name);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction) == null)
                    throw new IOException("Unity could not save the new player prefab.");
                Undo.SetCurrentGroupName("Create Default Player");
                Undo.RegisterCreatedObjectUndo(root, "Create Default Player");
                if (master.CameraPreset != null)
                    PlayerPlacementView.Activate(root, scene);
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                host = root.GetComponent<PlayerHost>();
                Selection.activeGameObject = root;
                SceneView.RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                // Roll back only this attempt; no existing asset or another player's configuration is replaced.
                Undo.RevertAllDownToGroup(undoGroup);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
                if (configuration.Length > 0 && AssetDatabase.IsValidFolder(configuration))
                    AssetDatabase.DeleteAsset(configuration);
                warning = "Player was not created: " + exception.Message;
                return false;
            }
            finally
            {
                Undo.IncrementCurrentGroup();
            }
        }

        #endregion

        #endregion
    }
}
