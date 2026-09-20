using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Creates a complete main or pause menu hierarchy in a selected editable scene.</summary>
    public static class MenuSceneFactory
    {
        #region Methods
        #region Creation
        /// <summary>Generates one isolated menu root without replacing existing scene content.</summary>
        /// <param name="preset">Saved project preset.</param>
        /// <param name="kind">Main or pause structure.</param>
        /// <param name="scene">Loaded destination scene.</param>
        /// <param name="assetDirectory">Existing Assets folder receiving input assets.</param>
        /// <returns>The generated host, ready for scene save or prefab export.</returns>
        public static MenuHost Create(MenuPreset preset, MenuKind kind, Scene scene, string assetDirectory)
        {
            // Validate before creating any scene object or input asset.
            List<string> warnings = new List<string>();
            MenuValidation.Collect(preset, warnings);
            if (warnings.Count > 0 || !scene.IsValid() || !scene.isLoaded || !AssetDatabase.IsValidFolder(assetDirectory))
                throw new InvalidOperationException(warnings.Count > 0 ? string.Join("\n", warnings) : "Choose a loaded scene and an existing Assets folder.");
            GameObject root = new GameObject(kind == MenuKind.Main ? "Main Menu" : "Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create " + root.name);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = preset.SortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = preset.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            MenuHost host = root.AddComponent<MenuHost>();
            host.Preset = preset;
            host.Kind = kind;
            List<MenuPage> pages = new List<MenuPage> { CreateHome(host) };
            if (preset.IncludeSettings)
                pages.Add(MenuSettingsFactory.Create(host));
            if (kind == MenuKind.Main && preset.IncludeCredits)
                pages.Add(CreateCredits(host));
            if (preset.ConfirmQuit)
                pages.Add(CreateConfirmation(host));
            host.Pages = pages.ToArray();
            foreach (MenuPage page in pages)
                page.Root.SetActive(kind == MenuKind.Main && page.Kind == MenuPageKind.Home);
            MenuInputFactory.Configure(host, assetDirectory);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            return host;
        }

        /// <summary>Builds the main or pause home panel and its portable commands.</summary>
        /// <param name="host">New menu host.</param>
        /// <returns>The home page and its default selection.</returns>
        private static MenuPage CreateHome(MenuHost host)
        {
            // Button order follows the original menu structure while excluding game-specific tools.
            RectTransform panel = MenuElements.Panel(host, "Home");
            MenuElements.Label(host, panel, "Title", host.Kind == MenuKind.Main ? host.Preset.Title : host.Preset.PauseTitle, new Vector2(0f, 310f), new Vector2(1100f, 110f), 52);
            List<MenuCommand> commands = new List<MenuCommand> { host.Kind == MenuKind.Main ? MenuCommand.Play : MenuCommand.Resume };
            if (host.Kind == MenuKind.Pause && host.Preset.IncludeRestart)
                commands.Add(MenuCommand.Restart);
            if (host.Preset.IncludeSettings)
                commands.Add(MenuCommand.Settings);
            if (host.Kind == MenuKind.Main && host.Preset.IncludeCredits)
                commands.Add(MenuCommand.Credits);
            if (host.Kind == MenuKind.Pause)
                commands.Add(MenuCommand.MainMenu);
            commands.Add(MenuCommand.Quit);
            List<Selectable> buttons = new List<Selectable>();
            for (int index = 0; index < commands.Count; index++)
            {
                MenuCommand command = commands[index];
                string label = command == MenuCommand.MainMenu ? "Main Menu" : command.ToString();
                buttons.Add(MenuElements.Button(host, panel, command.ToString(), label, command, new Vector2(0f, 170f - index * (host.Preset.ButtonSize.y + host.Preset.ButtonSpacing))));
            }
            LinkNavigation(buttons, host.Preset.Navigation.WrapButtons);
            return new MenuPage { Kind = MenuPageKind.Home, Root = panel.gameObject, FirstSelection = buttons[0] };
        }

        /// <summary>Creates a scrollable credits panel using only preauthored elements.</summary>
        /// <param name="host">Main menu host.</param>
        /// <returns>Credits page with a Back control.</returns>
        private static MenuPage CreateCredits(MenuHost host)
        {
            // ScrollRect moves existing content and does not instantiate runtime UI.
            RectTransform panel = MenuElements.Panel(host, "Credits");
            MenuElements.Label(host, panel, "Title", "CREDITS", new Vector2(0f, 310f), new Vector2(900f, 100f), 44);
            RectTransform viewport = MenuElements.Rect("Viewport", panel, new Vector2(880f, 420f), new Vector2(0f, 35f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            Text content = MenuElements.Label(host, viewport, "Content", host.Preset.Credits, Vector2.zero, new Vector2(860f, 420f));
            content.alignment = TextAnchor.UpperCenter;
            content.rectTransform.pivot = new Vector2(0.5f, 1f);
            content.rectTransform.anchorMin = content.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content.rectTransform;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            MenuButton back = MenuElements.Button(host, panel, "CreditsBack", "Back", MenuCommand.Back, new Vector2(0f, -280f));
            return new MenuPage { Kind = MenuPageKind.Credits, Root = panel.gameObject, FirstSelection = back };
        }

        /// <summary>Creates a reusable quit confirmation page.</summary>
        /// <param name="host">Owning menu host.</param>
        /// <returns>Confirmation page with Cancel focused by default.</returns>
        private static MenuPage CreateConfirmation(MenuHost host)
        {
            // Confirmation is an existing page, never a dynamically instantiated dialog.
            RectTransform panel = MenuElements.Panel(host, "Quit Confirmation");
            MenuElements.Label(host, panel, "Question", "Quit the game?", new Vector2(0f, 150f), new Vector2(900f, 90f), 38);
            MenuButton confirm = MenuElements.Button(host, panel, "ConfirmQuit", "Quit", MenuCommand.ConfirmQuit, new Vector2(0f, 30f));
            MenuButton cancel = MenuElements.Button(host, panel, "CancelQuit", "Cancel", MenuCommand.Back, new Vector2(0f, -50f));
            LinkNavigation(new List<Selectable> { confirm, cancel }, true);
            return new MenuPage { Kind = MenuPageKind.Confirmation, Root = panel.gameObject, FirstSelection = cancel };
        }
        #endregion

        #region Navigation
        /// <summary>Links a vertical group while retaining each slider's left/right adjustment behavior.</summary>
        /// <param name="controls">Controls in visual order.</param>
        /// <param name="wrap">Wrap first and last controls.</param>
        internal static void LinkNavigation(List<Selectable> controls, bool wrap)
        {
            // Explicit vertical neighbors avoid focus escaping into inactive menu pages.
            MenuNavigationUtility.Link(controls, wrap, false);
        }
        #endregion
        #endregion
    }
}
