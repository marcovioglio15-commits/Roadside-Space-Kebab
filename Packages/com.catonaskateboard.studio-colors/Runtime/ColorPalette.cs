using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.StudioColors
{
    /// <summary>Shares named colors between scene graphics, editor tools and UI Toolkit panels.</summary>
    [CreateAssetMenu(menuName = "Studio/Color Palette", fileName = "ColorPalette")]
    public sealed class ColorPalette : ScriptableObject
    {
        #region Fields
        [Header("Palette")]
        [Tooltip("Unique, case-sensitive keys referenced by graphics and tools.")]
        [SerializeField]
        private List<PaletteEntry> entries = new List<PaletteEntry>();
        #endregion

        #region Methods
        #region Lookup
        /// <summary>Resolves a palette token without creating per-frame lookup objects.</summary>
        /// <param name="key">Case-sensitive token.</param>
        /// <param name="color">Resolved color, or white when absent.</param>
        /// <returns>Whether the palette contains the token.</returns>
        public bool TryGet(string key, out Color color)
        {
            // Small palettes use a scan so live asset edits need no cache invalidation.
            foreach (PaletteEntry entry in entries)
                if (entry != null && entry.Key == key)
                {
                    color = entry.Color;
                    return true;
                }

            color = Color.white;
            return false;
        }

        /// <summary>Applies one token to an existing UI Toolkit element.</summary>
        /// <param name="element">Existing label, button or container.</param>
        /// <param name="key">Palette token to resolve.</param>
        /// <param name="background">Use the background instead of the text color.</param>
        /// <returns>Whether a color was applied.</returns>
        public bool Apply(VisualElement element, string key, bool background = false)
        {
            // Missing tokens leave the original style intact.
            if (element == null || !TryGet(key, out Color color))
                return false;

            if (background)
                element.style.backgroundColor = color;
            else
                element.style.color = color;
            return true;
        }
        #endregion
        #endregion
    }

    /// <summary>Stores one portable palette token.</summary>
    [Serializable]
    public sealed class PaletteEntry
    {
        #region Fields
        [Tooltip("Unique token used by palette consumers.")]
        public string Key;
        [Tooltip("Color assigned to this token.")]
        public Color Color = Color.white;
        #endregion
    }
}
