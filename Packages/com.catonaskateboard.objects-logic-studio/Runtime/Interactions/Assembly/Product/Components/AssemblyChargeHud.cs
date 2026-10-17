using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents assembly progress through preauthored graphics on the shared scene Canvas.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Assembly Charge HUD")]
    public sealed class AssemblyChargeHud : ObserverHudPanel
    {
        #region Serialized Fields

        [Header("Assembly Graphics")]
        [Tooltip("Existing ring with a CanvasRenderer, receiving charge progress and the default symbol.")]
        [SerializeField]
        private AssemblyChargeRing ring;
        [Tooltip("Existing image displaying an optional product icon. An empty icon uses the ring's built-in symbol.")]
        [SerializeField]
        private Image icon;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Checks the actual rendering dependencies, including the ring's native CanvasRenderer.</summary>
        /// <returns>True when both existing graphics can render inside the shared panel.</returns>
        public override bool IsValid()
        {
            return base.IsValid() && ring != null && ring.canvasRenderer != null && ring.transform.IsChildOf(Panel)
                && icon != null && icon.canvasRenderer != null && icon.transform.IsChildOf(Panel);
        }

        /// <summary>Applies the selected product appearance before revealing its existing graphics.</summary>
        /// <param name="settings">Validated product-specific progress configuration.</param>
        /// <param name="view">Gameplay camera defining the fake cursor's viewport.</param>
        internal void Show(AssemblyChargeSettings settings, Camera view)
        {
            // A missing custom sprite affects only the center image, never the track or progress arc.
            Panel.anchorMin = Panel.anchorMax = view.rect.center;
            Panel.anchoredPosition = new Vector2(settings.OffsetX, settings.OffsetY);
            Panel.sizeDelta = Vector2.one * settings.Size;
            icon.sprite = settings.Icon;
            icon.color = settings.IconColor;
            icon.enabled = settings.Icon != null;
            ring.Configure(settings);
            ShowPanel();
        }

        /// <summary>Updates the existing progress mesh during an uninterrupted hold.</summary>
        /// <param name="amount">Normalized elapsed charging time.</param>
        internal void SetProgress(float amount)
        {
            ring.SetProgress(amount);
        }

        #endregion

        #endregion
    }
}
