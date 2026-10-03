using System;
using System.IO;
using System.Linq;
using CatOnASkateboard.AudioStudio;
using CatOnASkateboard.AudioStudio.Editor;
using CatOnASkateboard.ObjectsLogicStudio;
using CatOnASkateboard.PlayerStudio;
using RoadsideSpaceKebab.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideSpaceKebab.Editor
{
    /// <summary>Authors FMOD adapters and listeners so gameplay performs no component creation at runtime.</summary>
    public static class ProjectAudioSetup
    {
        #region Properties

        /// <summary>Shared gameplay event catalog edited in Audio Studio.</summary>
        public static AudioPreset Preset => AssetDatabase.LoadAssetAtPath<AudioPreset>("Assets/ScriptableObjects/Presets_Modules/Audio/AudioPreset.asset");

        #endregion

        #region Methods

        #region Player Setup

        /// <summary>Rejects missing player camera or audio dependencies before editing the scene.</summary>
        /// <param name="player">Selected gameplay scene's single player.</param>
        public static void ValidatePlayer(PlayerHost player)
        {
            // The observer's configured camera is authoritative when several cameras exist.
            if (Preset == null || CameraFor(player) == null)
                throw new InvalidOperationException("Assign AudioPreset and an enabled player camera before connecting gameplay audio.");
        }

        /// <summary>Connects the reusable player prefab used by the main scene and recipe test scenes.</summary>
        public static void ConfigurePlayerPrefab()
        {
            // Existing instances inherit the saved adapter and listener without per-scene duplicate components.
            ConnectBanks();
            ConfigurePrefab("Assets/Prefabs/PF_Player.prefab");
            ConfigurePrefab("Packages/com.catonaskateboard.player-studio.content/Players/PF_Player.prefab");
        }

        /// <summary>Connects one project-authored player prefab and preserves its existing hierarchy.</summary>
        /// <param name="path">Player prefab used by the project's scenes.</param>
        private static void ConfigurePrefab(string path)
        {
            // Prefab contents are released even if validation or saving fails.
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PlayerHost player = root.GetComponent<PlayerHost>();
                ValidatePlayer(player);
                Configure(player);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Applies Audio Studio's source and loading policy to the installed FMOD integration.</summary>
        public static void ConnectBanks()
        {
            // Explicit refresh keeps preview and builds on the same bank source as gameplay.
            string bankPath = Preset.Connection.UseStudioProject
                ? FmodBankPaths.FromProject(Path.GetFullPath(Preset.Connection.ProjectPath))
                : Path.GetFullPath(Preset.Connection.BankPath);
            if (!Directory.Exists(bankPath))
                throw new InvalidOperationException("Build the FMOD banks in the project's configured output directory first.");
            string relative = FileUtil.GetProjectRelativePath(bankPath.Replace('\\', '/'));
            FMODUnity.Settings settings = FMODUnity.Settings.Instance;
            Undo.RecordObject(settings, "Connect gameplay FMOD banks");
            settings.HasSourceProject = Preset.Connection.UseStudioProject;
            settings.HasPlatforms = Preset.Connection.BanksHavePlatforms;
            settings.SourceProjectPath = Preset.Connection.ProjectPath;
            settings.SourceBankPath = string.IsNullOrEmpty(relative) ? bankPath.Replace('\\', '/') : relative;
            settings.BankLoadType = Preset.Connection.AutomaticBankLoading ? FMODUnity.BankLoadType.All : FMODUnity.BankLoadType.None;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            FMODUnity.EventManager.RefreshBanks();
            FMODUnity.EventManager.CopyToStreamingAssets(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>Connects the selected scene's player and removes redundant adapters left on separate roots.</summary>
        /// <param name="scene">Gameplay scene being configured.</param>
        /// <param name="player">Its validated Player Studio host.</param>
        public static void ConfigureScene(Scene scene, PlayerHost player)
        {
            // One player owns the gameplay catalog; a pause scene owns only menu bus controls.
            GameplayAudio selected = Configure(player);
            foreach (GameplayAudio duplicate in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayAudio>(true)))
                if (duplicate != selected)
                    Undo.DestroyObjectImmediate(duplicate);
        }

        /// <summary>Assigns the project catalog and radio key, preserving all event-specific configuration.</summary>
        /// <param name="player">Prefab contents or scene player receiving the authored components.</param>
        /// <returns>The adapter retained as this scene's audio owner.</returns>
        private static GameplayAudio Configure(PlayerHost player)
        {
            // Native components are added only by this explicit editor operation.
            GameplayAudio audio = player.GetComponent<GameplayAudio>();
            if (audio == null)
                audio = Undo.AddComponent<GameplayAudio>(player.gameObject);
            using (SerializedObject data = new SerializedObject(audio))
            {
                data.FindProperty("preset").objectReferenceValue = Preset;
                data.FindProperty("music").stringValue = "mus_radio";
                data.ApplyModifiedProperties();
            }
            Undo.RecordObject(audio, "Enable gameplay FMOD adapter");
            audio.enabled = true;
            Camera camera = CameraFor(player);
            FMODUnity.StudioListener listener = camera.GetComponent<FMODUnity.StudioListener>();
            if (listener == null)
                listener = Undo.AddComponent<FMODUnity.StudioListener>(camera.gameObject);
            Undo.RecordObject(listener, "Enable gameplay FMOD listener");
            listener.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
            PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
            return audio;
        }

        /// <summary>Finds the observer camera or the player's sole enabled camera.</summary>
        /// <param name="player">Host whose hierarchy contains the gameplay view.</param>
        /// <returns>An unambiguous authored gameplay camera, or null.</returns>
        private static Camera CameraFor(PlayerHost player)
        {
            // An ambiguous multi-camera rig needs an explicit observer binding.
            if (player == null)
                return null;
            HoverObserver observer = player.GetComponentInChildren<HoverObserver>(true);
            if (observer != null && observer.ConfiguredView != null && observer.ConfiguredView.enabled)
                return observer.ConfiguredView;
            Camera[] cameras = player.GetComponentsInChildren<Camera>(true).Where(camera => camera.enabled).ToArray();
            return cameras.Length == 1 ? cameras[0] : null;
        }

        #endregion

        #endregion
    }
}
