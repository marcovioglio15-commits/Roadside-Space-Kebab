using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.StudioColors
{
    /// <summary>Colors a preauthored uGUI graphic, including TMP text, without a frame update.</summary>
    [DisallowMultipleComponent]
    public sealed class PaletteGraphic : MonoBehaviour
    {
        #region Fields
        [Header("Color Binding")]
        [Tooltip("Palette containing the requested token.")]
        [SerializeField]
        private ColorPalette palette = null;
        [Tooltip("Existing Image, Text or TMP graphic to color.")]
        [SerializeField]
        private Graphic target = null;
        [Tooltip("Case-sensitive palette token.")]
        [SerializeField]
        private string key = string.Empty;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Applies the configured token when this graphic becomes active.</summary>
        private void OnEnable()
        {
            // Reapply only on activation or explicit theme changes.
            Apply();
        }

        /// <summary>Refreshes the existing graphic after a palette or token change.</summary>
        public void Apply()
        {
            // Preserve the authored graphic when its palette token is unavailable.
            if (palette != null && target != null && palette.TryGet(key, out Color color))
                target.color = color;
        }
        #endregion
        #endregion
    }
}
