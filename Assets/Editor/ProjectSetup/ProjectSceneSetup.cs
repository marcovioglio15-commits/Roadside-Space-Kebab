using System;
using System.Collections.Generic;
using System.Linq;
using CatOnASkateboard.MenuStudio;
using CatOnASkateboard.ObjectsLogicStudio;
using CatOnASkateboard.PlayerStudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadsideSpaceKebab.Editor
{
    /// <summary>Synchronizes the project's gameplay scene references without rebuilding menu layouts.</summary>
    public static class ProjectSceneSetup
    {
        #region Properties

        /// <summary>Shared source of the single gameplay entry scene.</summary>
        public static MenuPreset Menu => AssetDatabase.LoadAssetAtPath<MenuPreset>("Assets/ScriptableObjects/Presets_Modules/Menu/MenuPreset.asset");

        #endregion

        #region Methods

        #region Applying

        /// <summary>Connects a saved gameplay scene to the menus, existing day references and FMOD player setup.</summary>
        /// <param name="path">Existing gameplay scene asset path.</param>
        public static void Apply(string path)
        {
            // Preflight before editing the shared presets or build order.
            if (EditorApplication.isPlayingOrWillChangePlaymode || Menu == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Choose a saved gameplay scene outside Play Mode.");
            if (path == Menu.MainMenuScene || path == Menu.PauseScene)
                throw new InvalidOperationException("The gameplay scene must differ from Main Menu and Pause Menu.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Menu.MainMenuScene) == null
                || !string.IsNullOrEmpty(Menu.PauseScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(Menu.PauseScene) == null)
                throw new InvalidOperationException("Assign existing Main Menu and Pause scenes in MenuPreset first.");
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isDirty)
                throw new InvalidOperationException("Save the selected gameplay scene before applying its project links.");
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                PlayerHost[] players = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerHost>(true)).ToArray();
                if (players.Length != 1 || !players[0].gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The main scene needs one active Player Studio host before audio can be connected.");
                ProjectAudioSetup.ValidatePlayer(players[0]);
                string previous = Menu.GameplayScene;
                ProjectAudioSetup.ConfigurePlayerPrefab();
                ProjectAudioSetup.ConfigureScene(scene, players[0]);
                MenuSceneOverlay overlay = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSceneOverlay>(true)).FirstOrDefault();
                if (overlay == null)
                    overlay = Undo.AddComponent<MenuSceneOverlay>(players[0].gameObject);
                Undo.RecordObject(overlay, "Connect pause menu");
                overlay.Preset = Menu;
                PrefabUtility.RecordPrefabInstancePropertyModifications(overlay);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unity could not save the selected gameplay scene.");

                // Every menu uses the same entry scene; only days targeting the former main scene are retargeted.
                foreach (MenuPreset preset in Assets<MenuPreset>())
                {
                    Undo.RecordObject(preset, "Change main gameplay scene");
                    preset.GameplayScene = path;
                    EditorUtility.SetDirty(preset);
                    AssetDatabase.SaveAssetIfDirty(preset);
                }
                foreach (SpawnFlowPlan plan in Assets<SpawnFlowPlan>())
                {
                    Undo.RecordObject(plan, "Reconnect day scenes");
                    foreach (SpawnFlowDay day in plan.Days)
                        if (day != null && day.Scene == previous)
                            day.Scene = path;
                    EditorUtility.SetDirty(plan);
                    AssetDatabase.SaveAssetIfDirty(plan);
                }
                BuildScenes(path, previous);
            }
            finally
            {
                // Additive inspection never closes the user's other open scenes.
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Places the menu first and the single gameplay entry second while retaining other day scenes.</summary>
        /// <param name="gameplay">New main gameplay scene.</param>
        /// <param name="previous">Former main scene to remove unless another active flow still needs it.</param>
        private static void BuildScenes(string gameplay, string previous)
        {
            // Preserve unrelated build entries, deduplicate paths and explicitly register every flow scene.
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            HashSet<string> included = new HashSet<string>();
            AddScene(Menu.MainMenuScene, scenes, included);
            AddScene(gameplay, scenes, included);
            AddScene(Menu.PauseScene, scenes, included);
            foreach (SpawnFlowPlan plan in Assets<SpawnFlowPlan>())
            {
                foreach (SpawnFlowDay day in plan.Days)
                    if (day != null)
                        AddScene(day.Scene, scenes, included);
                AddScene(plan.PauseScene, scenes, included);
                if (!plan.Loop)
                    AddScene(plan.MainMenu, scenes, included);
            }
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != previous && included.Add(scene.path))
                    scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Adds one valid referenced scene without changing other paths.</summary>
        /// <param name="path">Optional existing scene asset path.</param>
        /// <param name="scenes">Ordered build entries.</param>
        /// <param name="included">Paths already added.</param>
        private static void AddScene(string path, List<EditorBuildSettingsScene> scenes, HashSet<string> included)
        {
            // Empty optional destinations have no build entry.
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null && included.Add(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        /// <summary>Enumerates project-owned configuration assets without searching package examples.</summary>
        /// <typeparam name="T">Configuration asset type.</typeparam>
        /// <returns>Existing project assets of the requested type.</returns>
        private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        {
            // Asset discovery occurs only when the explicit setup command runs.
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" }))
                yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        #endregion

        #endregion
    }
}
