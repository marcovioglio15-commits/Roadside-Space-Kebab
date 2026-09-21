using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Authors reusable menu presets and generates prewired scene UI in a compact workspace.</summary>
    public sealed class MenuStudioWindow : StudioWindow
    {
        #region Fields
        private static readonly string[] sections = { "Create", "Layout", "Main", "Pause", "Settings", "Navigation", "Credits", "Validation" };
        private readonly List<string> diagnostics = new List<string>();
        [Header("Scene Placement")]
        [Tooltip("Optional scene to open additively for generation; empty uses the active scene.")]
        [SerializeField]
        private SceneAsset destination;
        [Tooltip("Main or pause menu structure to create.")]
        [SerializeField]
        private MenuKind kind;
        [Tooltip("Project folder receiving generated input assets.")]
        [SerializeField]
        private string assetDirectory = "Assets/Studio/Menu";
        #endregion

        #region Properties
        protected override Type PresetType => typeof(MenuPreset);
        protected override string[] Tabs => sections;
        private MenuPreset Preset => (MenuPreset)Draft;
        #endregion

        #region Methods
        #region Window
        /// <summary>Opens the standalone Menu Studio workspace.</summary>
        //[MenuItem("Tools/Menu Studio")]
        public static void Open()
        {
            // Menu presets and scenes are selected explicitly in the compact workspace.
            GetWindow<MenuStudioWindow>("Menu Studio");
        }

        /// <summary>Draws the selected menu domain without unrelated settings.</summary>
        /// <param name="data">Serialized menu proposal.</param>
        protected override void DrawTab(SerializedObject data)
        {
            // Each tab shares the same draft so closing a tab cannot lose unsaved edits.
            switch (Tab)
            {
                case 0: DrawCreation(data); break;
                case 1: StudioFields.Draw(data, "ReferenceResolution", "ButtonSize", "ButtonSpacing", "SortingOrder", "Background", "BackgroundSprite", "Font"); break;
                case 2:
                    StudioFields.Draw(data, "Title");
                    DrawScene(data.FindProperty("GameplayScene"), "Play scene");
                    DrawProfile(data.FindProperty("MainButtons"));
                    break;
                case 3:
                    StudioFields.Draw(data, "PauseTitle", "IncludeRestart");
                    DrawScene(data.FindProperty("MainMenuScene"), "Main menu scene");
                    DrawProfile(data.FindProperty("PauseButtons"));
                    break;
                case 4: DrawSettings(data); break;
                case 5: DrawNavigation(data.FindProperty("Navigation")); break;
                case 6:
                    StudioFields.Draw(data, "IncludeCredits");
                    if (data.FindProperty("IncludeCredits").boolValue)
                    {
                        StudioFields.Draw(data, "Credits");
                        StudioFields.Draw(data.FindProperty("Navigation"), "CreditsClose");
                    }
                    break;
                case 7:
                    diagnostics.Clear();
                    Validate(diagnostics);
                    EditorGUILayout.LabelField(diagnostics.Count > 0 ? string.Join("\n", diagnostics) : "No configuration warnings.", EditorStyles.wordWrappedLabel);
                    DrawSceneWarnings();
                    break;
            }
        }
        #endregion

        #region Scene Creation
        /// <summary>Shows explicit generation and prefab-export actions.</summary>
        /// <param name="data">Menu proposal.</param>
        private void DrawCreation(SerializedObject data)
        {
            // Creation always consumes the saved preset, never an unfinished proposal.
            StudioFields.Draw(data, "ConfirmQuit", "IncludeSettings", "IncludeCredits");
            destination = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent("Scene", "Empty uses the active scene; another scene opens additively without replacing current scenes."), destination, typeof(SceneAsset), false);
            kind = (MenuKind)EditorGUILayout.EnumPopup(new GUIContent("Menu", "Choose Main or Pause structure."), kind);
            assetDirectory = EditorGUILayout.TextField(new GUIContent("Generated assets", "Existing or new folder under Assets for project-owned input assets."), assetDirectory);
            using (new EditorGUI.DisabledScope(Pending || Source == null))
                if (StudioFields.Button("Create in scene", "Create a new root with Undo. Existing menus are not overwritten and the scene is not saved automatically."))
                    CreateMenu();
            MenuHost selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MenuHost>() : null;
            using (new EditorGUI.DisabledScope(selected == null))
                if (StudioFields.Button("Save selected menu as prefab", "Save the selected MenuHost hierarchy as a reusable prefab, keeping its project asset references."))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save menu prefab", selected.name, "prefab", "Choose a prefab destination.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        PrefabUtility.SaveAsPrefabAssetAndConnect(selected.gameObject, AssetDatabase.GenerateUniqueAssetPath(path), InteractionMode.UserAction);
                        SetStatus("Prefab saved. Input and preset references remain project-owned.");
                    }
                }
            EditorGUILayout.LabelField("Apply preset edits before creating. Generation leaves the scene open for inspection and save.", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Creates the requested hierarchy after validating paths and the saved preset.</summary>
        private void CreateMenu()
        {
            // Scene loading is additive so existing unsaved scene edits are preserved.
            try
            {
                if (!assetDirectory.StartsWith("Assets/", StringComparison.Ordinal) || assetDirectory.Contains("..") || assetDirectory.Contains("\\"))
                {
                    SetStatus("Choose a project-relative folder under Assets using forward slashes.");
                    return;
                }
                string[] segments = assetDirectory.TrimEnd('/').Split('/');
                string folder = "Assets";
                for (int index = 1; index < segments.Length; index++)
                {
                    if (!AssetDatabase.IsValidFolder(folder + "/" + segments[index]))
                        AssetDatabase.CreateFolder(folder, segments[index]);
                    folder += "/" + segments[index];
                }
                Scene scene = destination == null ? SceneManager.GetActiveScene() : SceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(destination));
                if (destination != null && !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(destination), OpenSceneMode.Additive);
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                MenuHost host = MenuSceneFactory.Create((MenuPreset)Source, kind, scene, folder);
                Undo.CollapseUndoOperations(group);
                EditorGUIUtility.PingObject(host);
                SetStatus("Menu created. Review and save the scene. Existing EventSystem settings, when present, are preserved.");
            }
            catch (Exception exception)
            {
                SetStatus(exception.GetBaseException().Message);
            }
        }

        /// <summary>Edits a runtime scene path through a validated SceneAsset selector.</summary>
        /// <param name="property">Serialized runtime path.</param>
        /// <param name="label">Visible field label.</param>
        private static void DrawScene(SerializedProperty property, string label)
        {
            // Runtime code receives a path and never references UnityEditor.SceneAsset.
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            EditorGUI.BeginChangeCheck();
            SceneAsset requested = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent(label, "Scene loaded by this command; add it to Build Settings before building."), current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = requested != null ? AssetDatabase.GetAssetPath(requested) : string.Empty;
        }
        #endregion

        #region Conditional Settings
        /// <summary>Draws settings sections only when they will be generated.</summary>
        /// <param name="data">Serialized menu proposal.</param>
        private static void DrawSettings(SerializedObject data)
        {
            // Disabled sections retain their values without occupying the workspace.
            StudioFields.Draw(data, "IncludeSettings");
            if (!data.FindProperty("IncludeSettings").boolValue)
                return;
            SerializedProperty settings = data.FindProperty("Settings");
            StudioFields.Draw(settings, "PreferenceKey", "Audio");
            if (settings.FindPropertyRelative("Audio").boolValue)
                StudioFields.Draw(settings, "UseUnityAudioListener");
            StudioFields.Draw(settings, "Video");
            if (settings.FindPropertyRelative("Video").boolValue)
            {
                StudioFields.Draw(settings, "Fullscreen", "Resolution", "VSync", "FrameRate");
                if (settings.FindPropertyRelative("FrameRate").boolValue)
                    StudioFields.Draw(settings, "FrameRates");
            }
            StudioFields.Draw(settings, "Controls");
            if (settings.FindPropertyRelative("Controls").boolValue)
                StudioFields.Draw(data, "ControlsText");
            DrawProfile(data.FindProperty("SettingsButtons"));
        }

        /// <summary>Draws enabled interaction features and per-state values.</summary>
        /// <param name="profile">Main, pause or settings button profile.</param>
        private static void DrawProfile(SerializedProperty profile)
        {
            // Conditional groups mirror the runtime profile's independent override switches.
            StudioFields.Draw(profile, "Enabled");
            if (!profile.FindPropertyRelative("Enabled").boolValue)
                return;
            StudioFields.Draw(profile, "ContentMode");
            bool image = profile.FindPropertyRelative("ContentMode").enumValueIndex == (int)MenuContentMode.Image;
            if (image)
                StudioFields.Draw(profile, "Images");
            StudioFields.Draw(profile, "Motion");
            MenuMotionMode motion = (MenuMotionMode)profile.FindPropertyRelative("Motion").enumValueIndex;
            bool transforms = motion == MenuMotionMode.Transform || motion == MenuMotionMode.TransformAndClips;
            bool clips = motion == MenuMotionMode.Clips || motion == MenuMotionMode.TransformAndClips;
            if (motion != MenuMotionMode.None)
            {
                StudioFields.Draw(profile, "MotionTarget", "TransitionSeconds", "UnscaledTime");
                if (transforms)
                {
                    StudioFields.Draw(profile, "HoverPulse");
                    if (profile.FindPropertyRelative("HoverPulse").boolValue)
                    {
                        StudioFields.Draw(profile, "PulseSeconds", "LoopPulse");
                        if (!profile.FindPropertyRelative("LoopPulse").boolValue)
                            StudioFields.Draw(profile, "PulseCycles");
                    }
                }
            }
            StudioFields.Draw(profile, "OverrideSprites");
            if (profile.FindPropertyRelative("OverrideSprites").boolValue)
                StudioFields.Draw(profile, "AllowEmptySprites");
            StudioFields.Draw(profile, "OverrideGraphicColors");
            if (!image)
                StudioFields.Draw(profile, "OverrideText");
            foreach (string name in new[] { "Normal", "Hover", "Pressed", "Disabled" })
            {
                SerializedProperty state = profile.FindPropertyRelative(name);
                state.isExpanded = EditorGUILayout.Foldout(state.isExpanded, name, true);
                if (!state.isExpanded)
                    continue;
                if (transforms)
                    StudioFields.Draw(state, "Scale", "Offset", "Rotation");
                if (clips)
                    StudioFields.Draw(state, "Clip");
                if (profile.FindPropertyRelative("OverrideSprites").boolValue)
                    StudioFields.Draw(state, "Sprite");
                if (profile.FindPropertyRelative("OverrideGraphicColors").boolValue)
                    StudioFields.Draw(state, "GraphicColor");
                if (!image && profile.FindPropertyRelative("OverrideText").boolValue)
                    StudioFields.Draw(state, "Font", "FontSize", "FontStyle", "TextColor");
            }
            StudioFields.Draw(profile, "HoverCue", "PressCue");
        }

        /// <summary>Draws action overrides, repeat settings and optional focus presentation.</summary>
        /// <param name="navigation">Navigation settings container.</param>
        private static void DrawNavigation(SerializedProperty navigation)
        {
            // Disabled navigation leaves pointer controls available without unrelated input fields.
            StudioFields.Draw(navigation, "Enabled", "FollowPointer");
            if (!navigation.FindPropertyRelative("Enabled").boolValue)
                return;
            StudioFields.Draw(navigation, "WrapButtons", "Move", "Submit", "Cancel", "Pause", "PreviousTab", "NextTab", "WrapTabs", "RepeatDelay", "RepeatInterval", "Deadzone", "CustomizeFocus");
            if (!navigation.FindPropertyRelative("CustomizeFocus").boolValue)
                return;
            StudioFields.Draw(navigation, "FocusGraphic");
            if (navigation.FindPropertyRelative("FocusGraphic").boolValue)
                StudioFields.Draw(navigation, "UnselectedGraphic", "SelectedGraphic");
            StudioFields.Draw(navigation, "FocusText");
            if (navigation.FindPropertyRelative("FocusText").boolValue)
                StudioFields.Draw(navigation, "UnselectedText", "SelectedText", "UnselectedStyle", "SelectedStyle");
            StudioFields.Draw(navigation, "FocusScale");
            if (navigation.FindPropertyRelative("FocusScale").boolValue)
                StudioFields.Draw(navigation, "UnselectedScale", "SelectedScale");
            StudioFields.Draw(navigation, "FocusOutline");
            if (navigation.FindPropertyRelative("FocusOutline").boolValue)
                StudioFields.Draw(navigation, "OutlineColor", "OutlineDistance");
        }
        #endregion

        #region Validation
        /// <summary>Collects invalid active menu settings without modifying the proposal.</summary>
        /// <param name="messages">Destination diagnostics.</param>
        protected override void Validate(List<string> messages)
        {
            // Domain validation is shared with scene generation and project integrations.
            MenuValidation.Collect(Preset, messages);
        }

        /// <summary>Reports optional scene commands that still need project-specific destinations.</summary>
        private void DrawSceneWarnings()
        {
            // Empty scene paths are valid while building a reusable menu template.
            foreach (string path in new[] { Preset.GameplayScene, Preset.MainMenuScene })
                if (string.IsNullOrEmpty(path))
                    EditorGUILayout.LabelField("A scene command is unassigned. Set destinations in Main and Pause before using those commands.", EditorStyles.wordWrappedMiniLabel);
                else if (!Array.Exists(EditorBuildSettings.scenes, scene => scene.enabled && scene.path == path))
                    EditorGUILayout.LabelField("Add to Build Settings: " + path, EditorStyles.wordWrappedMiniLabel);
        }
        #endregion
        #endregion
    }
}
