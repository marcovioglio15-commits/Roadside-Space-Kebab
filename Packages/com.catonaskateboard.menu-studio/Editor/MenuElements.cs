using CatOnASkateboard.MenuStudio;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Creates menu graphics exclusively during explicit editor generation.</summary>
    internal static class MenuElements
    {
        #region Methods
        #region Layout
        /// <summary>Creates a centered RectTransform under an existing parent.</summary>
        /// <param name="name">Stable hierarchy name.</param>
        /// <param name="parent">Owning transform.</param>
        /// <param name="size">Reference-pixel dimensions.</param>
        /// <param name="position">Centered anchored position.</param>
        /// <returns>The created RectTransform.</returns>
        internal static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            // All UI construction stays in the Editor assembly.
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>Stretches an existing rectangle to fill its parent.</summary>
        /// <param name="rect">Rectangle to configure.</param>
        internal static void Stretch(RectTransform rect)
        {
            // Parent-relative geometry supports different aspect ratios through CanvasScaler.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Creates a full-screen panel with the preset's background appearance.</summary>
        /// <param name="host">Owning generated menu.</param>
        /// <param name="name">Stable panel name.</param>
        /// <returns>The panel rectangle.</returns>
        internal static RectTransform Panel(MenuHost host, string name)
        {
            // A panel root blocks pointer input to gameplay while visible.
            RectTransform panel = Rect(name, host.transform, Vector2.zero, Vector2.zero);
            Stretch(panel);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = host.Preset.Background;
            background.sprite = host.Preset.BackgroundSprite;
            return panel;
        }

        /// <summary>Creates a label using a project font or Unity's built-in font.</summary>
        /// <param name="host">Owning preset source.</param>
        /// <param name="parent">Label parent.</param>
        /// <param name="name">Stable label object name.</param>
        /// <param name="text">Initial text content.</param>
        /// <param name="position">Centered position.</param>
        /// <param name="size">Text rectangle size.</param>
        /// <param name="fontSize">Font size in reference pixels.</param>
        /// <returns>The preauthored Text component.</returns>
        internal static Text Label(MenuHost host, Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize = 24)
        {
            // Font assets are referenced through Unity; no third-party font source is copied.
            Text label = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
            label.font = host.Preset.Font != null ? host.Preset.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }
        #endregion

        #region Controls
        /// <summary>Creates a fully wired menu button with both text and image content.</summary>
        /// <param name="host">Command recipient and preset source.</param>
        /// <param name="parent">Owning page or settings tab.</param>
        /// <param name="id">Stable ID used by per-button image profiles.</param>
        /// <param name="label">Visible initial text.</param>
        /// <param name="command">Command sent when clicked.</param>
        /// <param name="position">Centered anchored position.</param>
        /// <param name="settings">Use the settings interaction profile.</param>
        /// <returns>The created button.</returns>
        internal static MenuButton Button(MenuHost host, Transform parent, string id, string label, MenuCommand command, Vector2 position, bool settings = false)
        {
            // Content objects are preauthored for both presentation modes.
            RectTransform root = Rect(id, parent, host.Preset.ButtonSize, position);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.15f, 0.21f);
            MenuMotion motion = root.gameObject.AddComponent<MenuMotion>();
            motion.enabled = false;
            MenuButton button = root.gameObject.AddComponent<MenuButton>();
            button.Host = host;
            button.Preset = host.Preset;
            button.Command = command;
            button.SettingsStyle = settings;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.ContentRoot = Rect("Content", root, host.Preset.ButtonSize - new Vector2(24f, 8f), Vector2.zero);
            button.Label = Label(host, button.ContentRoot, "Label", label, Vector2.zero, button.ContentRoot.sizeDelta);
            button.ContentImage = Rect("Image", button.ContentRoot, button.ContentRoot.sizeDelta, Vector2.zero).gameObject.AddComponent<Image>();
            button.ContentImage.raycastTarget = false;
            button.ContentImage.gameObject.SetActive(false);
            button.MotionPlayer = motion;
            return button;
        }

        /// <summary>Creates an audio slider with a preauthored fill, handle and label.</summary>
        /// <param name="host">Owning menu.</param>
        /// <param name="parent">Audio settings tab.</param>
        /// <param name="name">Slider label and stable ID.</param>
        /// <param name="y">Vertical anchored position.</param>
        /// <returns>The created slider.</returns>
        internal static Slider Slider(MenuHost host, Transform parent, string name, float y)
        {
            // Slider uses existing child graphics and never builds a dropdown or popup at runtime.
            RectTransform root = Rect(name, parent, new Vector2(660f, 64f), new Vector2(0f, y));
            Image background = root.gameObject.AddComponent<Image>();
            background.color = host.Preset.Navigation.UnselectedGraphic;
            Text label = Label(host, root, "Label", name, new Vector2(-215f, 0f), new Vector2(200f, 56f));
            RectTransform track = Rect("Track", root, new Vector2(360f, 12f), new Vector2(105f, 0f));
            track.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.1f);
            Image fill = Rect("Fill", track, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform);
            fill.color = new Color(0.2f, 0.7f, 0.8f);
            RectTransform handleArea = Rect("HandleArea", track, Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            Image handle = Rect("Handle", handleArea, new Vector2(22f, 30f), Vector2.zero).gameObject.AddComponent<Image>();
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            AddFocus(host, slider, background, label);
            return slider;
        }

        /// <summary>Preauthors the focus effect required by a settings row.</summary>
        /// <param name="host">Preset source.</param>
        /// <param name="control">Existing selectable.</param>
        /// <param name="background">Existing row graphic.</param>
        /// <param name="label">Existing row label.</param>
        internal static void AddFocus(MenuHost host, Selectable control, Graphic background, Text label)
        {
            // Outline is created once in the editor and only toggled during navigation.
            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.enabled = false;
            MenuSettingsFocus focus = control.gameObject.AddComponent<MenuSettingsFocus>();
            focus.Preset = host.Preset;
            focus.Background = background;
            focus.Label = label;
            focus.Outline = outline;
        }
        #endregion
        #endregion
    }
}
