using System;
using System.Collections.Generic;
using CatOnASkateboard.MenuStudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Temporarily connects the requested gameplay scene to the existing menu preset.</summary>
public static class StudioBlockoutSetup
{
    #region Methods
    /// <summary>Saves menu routing and a single pause loader without rebuilding gameplay objects.</summary>
    public static void Run()
    {
        // The project-owned preset is shared by the already-authored main and pause scenes.
        const string path = "Assets/Scenes/Tool Testing Scenes/Test/Programming/Main/SCN_Blockout_LogicTest.unity";
        MenuPreset preset = AssetDatabase.LoadAssetAtPath<MenuPreset>("Assets/ScriptableObjects/Presets_Modules/Menu/MenuPreset.asset");
        preset.GameplayScene = path;
        preset.Title = "ROADSIDE SPACE KEBAB";
        EditorUtility.SetDirty(preset);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        MenuSceneOverlay overlay = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.TryGetComponent(out overlay))
                break;
        if (overlay == null)
            overlay = new GameObject("Pause Menu Scene").AddComponent<MenuSceneOverlay>();
        overlay.Preset = preset;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(preset.MainMenuScene, true));
        scenes.Add(new EditorBuildSettingsScene(path, true));
        scenes.Add(new EditorBuildSettingsScene(preset.PauseScene, true));
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (!existing.path.StartsWith("Assets/__StudioFlowChecks", StringComparison.Ordinal)
                && !scenes.Exists(entry => entry.path == existing.path))
                scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        Scene menuScene = EditorSceneManager.OpenScene(preset.MainMenuScene, OpenSceneMode.Single);
        if (UnityEngine.Object.FindAnyObjectByType<FMODUnity.StudioListener>() == null)
        {
            GameObject listener = new GameObject("Menu Audio Listener", typeof(FMODUnity.StudioListener));
            SceneManager.MoveGameObjectToScene(listener, menuScene);
        }
        foreach (GameObject root in menuScene.GetRootGameObjects())
            foreach (MenuHost host in root.GetComponentsInChildren<MenuHost>(true))
                foreach (MenuPage page in host.Pages)
                    if (page.Kind == MenuPageKind.Home && page.Root != null)
                        foreach (Text title in page.Root.GetComponentsInChildren<Text>(true))
                            if (title.name == "Title")
                            {
                                title.text = preset.Title;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(title);
                            }
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);
        AssetDatabase.SaveAssets();
        Debug.Log("BLOCKOUT MENU SETUP COMPLETE");
    }
    #endregion
}
