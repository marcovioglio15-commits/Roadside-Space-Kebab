using System;
using CatOnASkateboard.MenuStudio;
using UnityEditor;
using UnityEngine;

namespace RoadsideSpaceKebab.Editor
{
    /// <summary>Changes the project's single gameplay entry scene through one explicit editor operation.</summary>
    public sealed class MainSceneWindow : EditorWindow
    {
        #region State

        private SceneAsset selected;
        private string status = string.Empty;

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens the central gameplay scene and audio setup controls.</summary>
        [MenuItem("Tools/Roadside Space Kebab/Main Scene & Audio")]
        public static void Open()
        {
            // This project window does not add project-specific dependencies to reusable packages.
            GetWindow<MainSceneWindow>("Main Scene & Audio").Show();
        }

        /// <summary>Starts from the same menu preset used by the built Main Menu.</summary>
        private void OnEnable()
        {
            // The menu preset remains the single saved gameplay entry point.
            MenuPreset menu = ProjectSceneSetup.Menu;
            selected = menu != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(menu.GameplayScene) : null;
        }

        /// <summary>Offers one scene selection and an explicit synchronized apply command.</summary>
        private void OnGUI()
        {
            // Commands are unavailable during Play and never replace unsaved scene changes.
            MenuPreset menu = ProjectSceneSetup.Menu;
            if (menu == null)
            {
                EditorGUILayout.LabelField("Assign the project's MenuPreset asset before changing the main scene.", EditorStyles.wordWrappedLabel);
                return;
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("Current Scene", "Gameplay scene currently loaded by Play in every menu."),
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(menu.GameplayScene), typeof(SceneAsset), false);
            selected = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent("New Scene", "Saved gameplay scene to connect to menus, day plans and Build Settings."),
                selected, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(selected == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("Set Main Scene", "Update menu links, matching day scenes, player audio, pause overlay and build order."), GUILayout.Width(150f)))
                    Run(() => ProjectSceneSetup.Apply(AssetDatabase.GetAssetPath(selected)));
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("Repair Audio Links", "Connect the shared player to FMOD and refresh the current main scene's audio setup."), GUILayout.Width(150f)))
                    Run(() => ProjectSceneSetup.Apply(menu.GameplayScene));
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        }

        /// <summary>Reports the completed operation or a concrete authoring issue in the window.</summary>
        /// <param name="operation">Validated editor operation requested by the button.</param>
        private void Run(Action operation)
        {
            // Failed setup remains visible without changing the selected scene automatically.
            try
            {
                operation();
                status = "Main scene, menus, day plans, audio and Build Settings are connected.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogWarning(status);
            }
        }

        #endregion

        #endregion
    }
}
