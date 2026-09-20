using System.Collections.Generic;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Preauthors settings tabs, navigation, controls and persistent button callbacks.</summary>
    internal static class MenuSettingsFactory
    {
        #region Methods
        #region Creation
        /// <summary>Creates the settings page selected by the preset's available sections.</summary>
        /// <param name="host">Menu receiving the settings controller.</param>
        /// <returns>Created settings page and its initial focus.</returns>
        internal static MenuPage Create(MenuHost host)
        {
            // Controller lives on the always-active host so initial preferences load before opening UI.
            MenuSettingsController settings = host.gameObject.AddComponent<MenuSettingsController>();
            settings.Host = host;
            host.Settings = settings;
            RectTransform panel = MenuElements.Panel(host, "Settings");
            MenuElements.Label(host, panel, "Title", "SETTINGS", new Vector2(0f, 330f), new Vector2(900f, 90f), 44);
            MenuButton apply = MenuElements.Button(host, panel, "ApplySettings", "Apply", MenuCommand.Back, new Vector2(-195f, -240f), true);
            apply.InvokeCommand = false;
            UnityEventTools.AddPersistentListener(apply.onClick, settings.Apply);
            MenuButton back = MenuElements.Button(host, panel, "DiscardSettings", "Back", MenuCommand.Back, new Vector2(195f, -240f), true);
            List<GameObject> tabs = new List<GameObject>();
            List<Selectable> first = new List<Selectable>();
            List<string> names = new List<string>();
            List<MenuTabNavigation> navigation = new List<MenuTabNavigation>();
            if (host.Preset.Settings.Audio)
                names.Add("Audio");
            if (host.Preset.Settings.Video)
                names.Add("Video");
            if (host.Preset.Settings.Controls)
                names.Add("Controls");

            for (int index = 0; index < names.Count; index++)
            {
                RectTransform tab = MenuElements.Rect(names[index] + " Tab", panel, new Vector2(900f, 430f), Vector2.zero);
                List<Selectable> controls = BuildTab(host, settings, tab, names[index]);
                MenuButton selector = MenuElements.Button(host, panel, names[index] + "Tab", names[index], MenuCommand.Settings,
                    new Vector2((index - (names.Count - 1) * 0.5f) * 220f, 250f), true);
                ((RectTransform)selector.transform).sizeDelta = new Vector2(200f, 50f);
                selector.ContentRoot.sizeDelta = new Vector2(180f, 42f);
                selector.Label.rectTransform.sizeDelta = selector.ContentRoot.sizeDelta;
                selector.InvokeCommand = false;
                UnityEventTools.AddIntPersistentListener(selector.onClick, settings.SelectTab, index);
                first.Add(controls.Count > 0 ? controls[0] : selector);
                controls.Add(apply);
                controls.Add(back);
                MenuSceneFactory.LinkNavigation(controls, host.Preset.Navigation.WrapButtons);
                navigation.Add(new MenuTabNavigation { Controls = controls.ToArray() });
                tabs.Add(tab.gameObject);
                tab.gameObject.SetActive(index == 0);
            }
            settings.Tabs = tabs.ToArray();
            settings.FirstSelections = first.ToArray();
            settings.Navigation = navigation.ToArray();
            return new MenuPage { Kind = MenuPageKind.Settings, Root = panel.gameObject, FirstSelection = first.Count > 0 ? first[0] : apply };
        }

        /// <summary>Builds one tab's enabled options and records its focus order.</summary>
        /// <param name="host">Preset source.</param>
        /// <param name="settings">Callback recipient.</param>
        /// <param name="tab">Existing generated tab root.</param>
        /// <param name="name">Settings section name.</param>
        /// <returns>Generated selectables in visual order.</returns>
        private static List<Selectable> BuildTab(MenuHost host, MenuSettingsController settings, RectTransform tab, string name)
        {
            // Hidden feature groups generate no unnecessary controls or runtime branches.
            List<Selectable> controls = new List<Selectable>();
            switch (name)
            {
                case "Audio":
                    settings.Master = MenuElements.Slider(host, tab, "Master", 120f);
                    settings.Music = MenuElements.Slider(host, tab, "Music", 30f);
                    settings.Sfx = MenuElements.Slider(host, tab, "SFX", -60f);
                    controls.AddRange(new Selectable[] { settings.Master, settings.Music, settings.Sfx });
                    break;
                case "Video":
                    if (host.Preset.Settings.Fullscreen)
                        settings.FullscreenLabel = Option(host, tab, "Fullscreen", settings.ToggleFullscreen, controls).Label;
                    if (host.Preset.Settings.Resolution)
                        settings.ResolutionLabel = Option(host, tab, "Resolution", settings.CycleResolution, controls).Label;
                    if (host.Preset.Settings.VSync)
                        settings.VSyncLabel = Option(host, tab, "VSync", settings.ToggleVSync, controls).Label;
                    if (host.Preset.Settings.FrameRate)
                    {
                        MenuButton frameRate = Option(host, tab, "FrameRate", settings.CycleFrameRate, controls);
                        settings.FrameRateLabel = frameRate.Label;
                        settings.FrameRateRow = frameRate.gameObject;
                    }
                    break;
                case "Controls":
                    MenuElements.Label(host, tab, "Controls Reference", host.Preset.ControlsText, Vector2.zero, new Vector2(850f, 350f), 26);
                    break;
            }
            return controls;
        }

        /// <summary>Creates a settings option button with an explicit callback and focus effect.</summary>
        /// <param name="host">Owning menu.</param>
        /// <param name="parent">Video tab parent.</param>
        /// <param name="name">Stable option ID.</param>
        /// <param name="action">Settings action invoked on click.</param>
        /// <param name="controls">Ordered list receiving the new selectable.</param>
        /// <returns>The generated option button.</returns>
        private static MenuButton Option(MenuHost host, Transform parent, string name, UnityAction action, List<Selectable> controls)
        {
            // Settings focus is authored once alongside the ordinary button interaction profile.
            MenuButton button = MenuElements.Button(host, parent, name, name, MenuCommand.Settings, new Vector2(0f, 140f - controls.Count * 78f), true);
            ((RectTransform)button.transform).sizeDelta = new Vector2(650f, 62f);
            button.ContentRoot.sizeDelta = button.Label.rectTransform.sizeDelta = new Vector2(620f, 54f);
            button.InvokeCommand = false;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            MenuElements.AddFocus(host, button, button.image, button.Label);
            controls.Add(button);
            return button;
        }
        #endregion
        #endregion
    }
}
