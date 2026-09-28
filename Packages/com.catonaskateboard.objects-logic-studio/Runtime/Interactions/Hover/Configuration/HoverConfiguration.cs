using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Groups reusable detection and presentation independently of scene bindings.</summary>
    [Serializable]
    public sealed class HoverConfiguration
    {
        #region Serialized Fields

        [Header("Hover")]
        [Tooltip("Detection, placement and entry animation shared by this configuration.")]
        [SerializeField]
        private HoverSettings settings = new HoverSettings();

        [Tooltip("Text, font, background and layout applied to the existing label.")]
        [SerializeField]
        private HoverStyle style = new HoverStyle();

        #endregion

        #region Properties

        /// <summary>Reusable detection and motion values.</summary>
        public HoverSettings Settings => settings;
        /// <summary>Reusable graphics values.</summary>
        public HoverStyle Style => style;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates the selected detection mode and label style without rewriting values.</summary>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when the complete configuration is usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Missing nested data remains visible instead of being silently repaired.
            warning = "The hover configuration needs detection and style data.";
            return settings != null && style != null && settings.TryValidate(out warning) && style.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
