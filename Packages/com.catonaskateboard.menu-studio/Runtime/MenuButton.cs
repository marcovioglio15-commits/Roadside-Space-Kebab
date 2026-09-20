using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Applies menu-specific feedback through uGUI's existing selectable state changes.</summary>
    public sealed class MenuButton : Button
    {
        #region Fields
        [Header("Menu Binding")]
        [Tooltip("Host receiving commands and optional audio cues.")]
        public MenuHost Host;
        [Tooltip("Profile source shared with the generated menu.")]
        public MenuPreset Preset;
        [Tooltip("Use the settings button profile instead of the host's home profile.")]
        public bool SettingsStyle;
        [Tooltip("Portable command invoked by this button.")]
        public MenuCommand Command;
        [Tooltip("Invoke the host command; disable for custom settings callbacks.")]
        public bool InvokeCommand = true;
        [Header("Preauthored Graphics")]
        [Tooltip("Existing label used in text mode.")]
        public Text Label;
        [Tooltip("Existing image used in image-content mode.")]
        public Image ContentImage;
        [Tooltip("Existing content root used for content-only motion.")]
        public RectTransform ContentRoot;
        [Tooltip("Existing motion component; no runtime component is created.")]
        public MenuMotion MotionPlayer;
        private MenuButtonStyle style;
        private MenuImageContent content;
        private Sprite originalSprite;
        private Font originalFont;
        private MenuVisualPhase previousPhase;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Caches the profile and preauthored baselines before interactions begin.</summary>
        protected override void Awake()
        {
            // Unity's Button lifecycle remains responsible for pointer and navigation behavior.
            base.Awake();
            if (Preset == null || Host == null)
                return;
            style = SettingsStyle ? Preset.SettingsButtons : Host.Kind == MenuKind.Main ? Preset.MainButtons : Preset.PauseButtons;
            content = style.Images.Find(entry => entry.ButtonId == gameObject.name);
            originalSprite = image != null ? image.sprite : null;
            originalFont = Label != null ? Label.font : null;
            MotionPlayer?.Initialize(style.MotionTarget == MenuMotionTarget.Content ? ContentRoot : (RectTransform)transform);
            onClick.AddListener(Execute);
        }

        /// <summary>Dispatches the preauthored command to the owning menu.</summary>
        private void Execute()
        {
            // Settings buttons use their own persistent callbacks and skip host commands.
            if (InvokeCommand && Host != null)
                Host.Execute(Command);
        }
        #endregion

        #region Interaction
        /// <summary>Updates presentation when uGUI changes the selectable state.</summary>
        /// <param name="selection">Native selectable state.</param>
        /// <param name="instant">Whether feedback should skip interpolation.</param>
        protected override void DoStateTransition(SelectionState selection, bool instant)
        {
            // State callbacks replace frame-by-frame polling of interactability and selection.
            base.DoStateTransition(selection, instant);
            if (style == null || !style.Enabled)
                return;
            MenuVisualPhase phase = selection switch
            {
                SelectionState.Highlighted => MenuVisualPhase.Hover,
                SelectionState.Selected => MenuVisualPhase.Hover,
                SelectionState.Pressed => MenuVisualPhase.Pressed,
                SelectionState.Disabled => MenuVisualPhase.Disabled,
                _ => MenuVisualPhase.Normal
            };
            MenuVisualState state = phase switch
            {
                MenuVisualPhase.Hover => style.Hover,
                MenuVisualPhase.Pressed => style.Pressed,
                MenuVisualPhase.Disabled => style.Disabled,
                _ => style.Normal
            };
            ApplyGraphics(state, phase);
            MotionPlayer?.Play(style, state, phase, instant);
            if (!instant && phase != previousPhase && Host != null)
            {
                string cue = phase == MenuVisualPhase.Hover ? style.HoverCue : phase == MenuVisualPhase.Pressed ? style.PressCue : string.Empty;
                if (!string.IsNullOrEmpty(cue))
                    Host.AudioCue.Invoke(cue);
            }
            previousPhase = phase;
        }

        /// <summary>Optionally transfers navigation focus to the hovered button.</summary>
        /// <param name="eventData">Pointer event supplied by the EventSystem.</param>
        public override void OnPointerEnter(PointerEventData eventData)
        {
            // Selection follows hover only when the profile explicitly permits it.
            base.OnPointerEnter(eventData);
            if (Preset != null && Preset.Navigation.FollowPointer && IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
        #endregion

        #region Graphics
        /// <summary>Applies sprite and text overrides to existing graphics.</summary>
        /// <param name="state">Requested appearance.</param>
        /// <param name="phase">Requested interaction phase.</param>
        private void ApplyGraphics(MenuVisualState state, MenuVisualPhase phase)
        {
            // Transparent empty sprites retain the Graphic raycast area.
            if (image != null)
            {
                if (style.OverrideSprites)
                    image.sprite = state.Sprite != null || style.AllowEmptySprites ? state.Sprite : originalSprite;
                if (style.OverrideGraphicColors)
                    image.color = state.GraphicColor;
                if (style.OverrideSprites && style.AllowEmptySprites && state.Sprite == null)
                    image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);
            }
            bool useImage = style.ContentMode == MenuContentMode.Image && content != null;
            if (Label != null)
            {
                Label.gameObject.SetActive(!useImage);
                if (style.OverrideText)
                {
                    Label.font = state.Font != null ? state.Font : originalFont;
                    Label.fontSize = state.FontSize;
                    Label.fontStyle = state.FontStyle;
                    Label.color = state.TextColor;
                }
            }
            if (ContentImage == null)
                return;
            ContentImage.gameObject.SetActive(useImage);
            if (!useImage)
                return;
            Sprite sprite = phase switch
            {
                MenuVisualPhase.Hover => content.Hover,
                MenuVisualPhase.Pressed => content.Pressed,
                MenuVisualPhase.Disabled => content.Disabled,
                _ => content.Normal
            };
            ContentImage.sprite = sprite != null ? sprite : content.Normal;
            ContentImage.preserveAspect = content.PreserveAspect;
            ContentImage.color = phase switch
            {
                MenuVisualPhase.Hover => content.HoverColor,
                MenuVisualPhase.Pressed => content.PressedColor,
                MenuVisualPhase.Disabled => content.DisabledColor,
                _ => content.NormalColor
            };
        }
        #endregion
        #endregion
    }
}
