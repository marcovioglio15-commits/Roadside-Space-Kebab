using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Creates missing defaults in one embedded content package without overwriting existing assets.</summary>
    [InitializeOnLoad]
    internal static class PlayerDefaultAssets
    {
        #region Paths

        internal const string ContentRoot = "Packages/com.catonaskateboard.player-studio.content";
        internal const string DefaultsRoot = ContentRoot + "/Defaults";

        #endregion

        private static bool resolving;
        private static double resolveDeadline;

        #region Methods

        #region Bootstrap

        /// <summary>Defers first-import setup until Unity can safely import newly created assets.</summary>
        static PlayerDefaultAssets()
        {
            // Batch verification invokes Ensure explicitly; ordinary imports prepare defaults once.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += Initialize;
        }

        /// <summary>Prepares a project-owned content package, leaving the installed code package untouched.</summary>
        private static void Initialize()
        {
            // Never create assets during Play, import workers or an active compilation.
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsAssetImportWorkerProcess())
                return;
            try
            {
                Ensure();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player Studio defaults could not be prepared: " + exception.Message);
            }
        }

        /// <summary>Creates missing editable defaults and returns the existing or newly created master.</summary>
        /// <returns>The project-owned default master; existing values are preserved.</returns>
        internal static PlayerMasterPreset Ensure()
        {
            // A separate embedded package remains writable when the code package comes from a registry.
            Directory.CreateDirectory(ContentRoot);
            string manifest = ContentRoot + "/package.json";
            if (!File.Exists(manifest))
                File.WriteAllText(manifest, @"{""name"":""com.catonaskateboard.player-studio.content"",""version"":""1.0.0"",""displayName"":""Player Studio Content"",""description"":""Editable Player Studio defaults owned by this project.""}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(ContentRoot))
            {
                if (!resolving)
                {
                    resolving = true;
                    resolveDeadline = EditorApplication.timeSinceStartup + 60d;
                    Client.Resolve();
                    EditorApplication.update += FinishResolution;
                }
                return null;
            }
            PlayerDefaultRecovery.RestoreMissing();
            PlayerMasterPreset master = CreateMissing(DefaultsRoot);
            PlayerTestScene.Ensure();
            PlayerDefaultRecovery.CaptureMissing();
            return master;
        }

        /// <summary>Builds a complete default collection in an explicit writable package folder.</summary>
        /// <param name="folder">Content folder; distribution preparation also uses this for bundled templates.</param>
        /// <returns>The master connecting this collection.</returns>
        internal static PlayerMasterPreset CreateMissing(string folder)
        {
            // Reuse each asset independently; no personalized preset receives default values again.
            EnsureFolder(folder);
            EnsureFolder(folder + "/Presets");
            EnsureFolder(folder + "/Player");
            PlayerBodyPreset body = Preset<PlayerBodyPreset>(folder, "Body", null);
            Preset<PlayerLocomotionPreset>(folder, "Locomotion", null);
            Preset<PlayerInputPreset>(folder, "Input", null);
            Preset<PlayerCameraPreset>(folder, "Camera", null);
            PlayerToolsPreset tools = Preset<PlayerToolsPreset>(folder, "Tools", null);
            // Input, locomotion and camera are assigned after the user selects their action roles.
            PlayerMasterPreset master = Preset<PlayerMasterPreset>(folder, "Master",
                value => PlayerCreationUtility.SetReferences(value, ("bodyPreset", body), ("toolsPreset", tools)));
            string prefabPath = folder + "/Player/Player.prefab";
            if (!File.Exists(prefabPath))
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = PlayerCreationUtility.Create(master, preview, "Player");
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }
            return master;
        }

        /// <summary>Continues first-import setup after Unity registers the embedded content package.</summary>
        private static void FinishResolution()
        {
            // Wait through Editor callbacks without blocking Unity's package manager.
            if (!AssetDatabase.IsValidFolder(ContentRoot) && EditorApplication.timeSinceStartup < resolveDeadline)
                return;
            EditorApplication.update -= FinishResolution;
            resolving = false;
            if (!AssetDatabase.IsValidFolder(ContentRoot))
                Debug.LogWarning("Player Studio content package is not registered yet. Reopen the tool after Package Manager finishes.");
            else
                Initialize();
        }

        #endregion

        #region Assets

        /// <summary>Loads an existing preset or initializes a new one before saving it.</summary>
        /// <typeparam name="T">Preset type to create.</typeparam>
        /// <param name="folder">Default collection root.</param>
        /// <param name="name">Stable module file name.</param>
        /// <param name="configure">New-asset initialization only.</param>
        /// <returns>The preserved or newly configured preset.</returns>
        private static T Preset<T>(string folder, string name, Action<T> configure) where T : ScriptableObject
        {
            // A conflicting file is an error, never permission to replace it.
            string path = folder + "/Presets/" + name + ".asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            if (File.Exists(path))
                throw new IOException("An incompatible asset already exists at " + path);
            T created = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(created);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }







        /// <summary>Creates only the missing directory segments through the Asset Database.</summary>
        /// <param name="path">Project-relative package content path.</param>
        private static void EnsureFolder(string path)
        {
            // Native creation makes new folders immediately addressable by subsequent asset imports.
            if (AssetDatabase.IsValidFolder(path))
                return;
            if (string.IsNullOrEmpty(path) || path == "Packages")
                throw new IOException("The content package must be registered before creating defaults.");
            string parent = Path.GetDirectoryName(path).Replace(Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #endregion

        #endregion
    }
}
