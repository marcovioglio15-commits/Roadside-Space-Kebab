using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Changes existing settings graphics on focus events without polling selection.</summary>
    public sealed class MenuSettingsFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        #region Fields
        [Header("Focus Presentation")]
        [Tooltip("Preset supplying the settings selection appearance.")]
        public MenuPreset Preset;
        [Tooltip("Existing option background.")]
        public Graphic Background;
        [Tooltip("Existing option label.")]
        public Text Label;
        [Tooltip("Preauthored outline effect, toggled on focus.")]
        public Outline Outline;
        #endregion

        #region Methods
        #region Focus
        /// <summary>Applies selected colors and the preauthored outline.</summary>
        /// <param name="eventData">Focus event from the active EventSystem.</param>
        public void OnSelect(BaseEventData eventData)
        {
            // Focus effects are independent of pointer press animation.
            Apply(true);
        }

        /// <summary>Restores the configured unfocused presentation.</summary>
        /// <param name="eventData">Focus event from the active EventSystem.</param>
        public void OnDeselect(BaseEventData eventData)
        {
            // No runtime graphics or materials are allocated for deselection.
            Apply(false);
        }

        /// <summary>Resets focus presentation when a settings tab closes.</summary>
        private void OnDisable()
        {
            // Inactive controls cannot retain their focused outline on the next opening.
            Apply(false);
        }

        /// <summary>Applies only enabled selection overrides to existing objects.</summary>
        /// <param name="selected">Whether this control owns selection.</param>
        private void Apply(bool selected)
        {
            // Each optional presentation group remains independently configurable.
            if (Preset == null || !Preset.Navigation.CustomizeFocus)
                return;
            MenuNavigation navigation = Preset.Navigation;
            if (navigation.FocusGraphic && Background != null)
                Background.color = selected ? navigation.SelectedGraphic : navigation.UnselectedGraphic;
            if (navigation.FocusText && Label != null)
            {
                Label.color = selected ? navigation.SelectedText : navigation.UnselectedText;
                Label.fontStyle = selected ? navigation.SelectedStyle : navigation.UnselectedStyle;
            }
            if (navigation.FocusScale)
                transform.localScale = selected ? navigation.SelectedScale : navigation.UnselectedScale;
            if (Outline != null)
            {
                Outline.enabled = selected && navigation.FocusOutline;
                Outline.effectColor = navigation.OutlineColor;
                Outline.effectDistance = navigation.OutlineDistance;
            }
        }
        #endregion
        #endregion
    }
}
