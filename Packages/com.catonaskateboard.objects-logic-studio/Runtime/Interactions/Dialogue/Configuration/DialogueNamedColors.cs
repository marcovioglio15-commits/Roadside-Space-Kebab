using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Associates one color token name with an entry-local color.</summary>
    [Serializable]
    public struct DialogueNamedColor
    {
        #region Fields

        [Header("Text Color")]
        [Tooltip("Name used by color tokens. Names ignore letter case and cannot contain token delimiters.")]
        public string Name;
        [Tooltip("Color applied to the selected text, including its opacity.")]
        public Color Color;

        #endregion
    }

    /// <summary>Validates palettes and resolves token names once when a page is parsed.</summary>
    public static class DialogueNamedColors
    {
        #region State

        private static readonly char[] delimiters = { '[', ']', '=', '/', '\\', '\n', '\r' };

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects duplicate names, reserved delimiters and invalid color channels without altering values.</summary>
        /// <param name="colors">Entry palette or reusable palette preset.</param>
        /// <param name="warning">Receives the first invalid color definition.</param>
        /// <returns>True when the palette can be used by color tokens.</returns>
        public static bool TryValidate(DialogueNamedColor[] colors, out string warning)
        {
            warning = "Text Colors must contain a valid color list.";
            if (colors == null)
                return false;
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Token names remain readable while delimiters retain their parsing meaning.
            foreach (DialogueNamedColor color in colors)
            {
                warning = "Text colors need unique names without outer spaces, brackets, slashes or equals signs.";
                if (string.IsNullOrWhiteSpace(color.Name) || color.Name != color.Name.Trim()
                    || color.Name.IndexOfAny(delimiters) >= 0 || !names.Add(color.Name))
                    return false;
                warning = color.Name + ": color channels must be finite and between zero and one.";
                if (!Channel(color.Color.r) || !Channel(color.Color.g) || !Channel(color.Color.b) || !Channel(color.Color.a))
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks one ordinary color channel rather than silently normalizing it.</summary>
        /// <param name="value">Color or opacity channel.</param>
        /// <returns>True for finite values between zero and one.</returns>
        private static bool Channel(float value)
        {
            return float.IsFinite(value) && value >= 0f && value <= 1f;
        }

        #endregion

        #region Lookup

        /// <summary>Finds a named color without allocating a runtime palette dictionary.</summary>
        /// <param name="colors">Validated entry palette.</param>
        /// <param name="name">Name inside a color token.</param>
        /// <param name="color">Receives the selected color.</param>
        /// <returns>True when the palette contains this token name.</returns>
        internal static bool TryFind(DialogueNamedColor[] colors, string name, out Color color)
        {
            foreach (DialogueNamedColor entry in colors)
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    color = entry.Color;
                    return true;
                }
            color = Color.white;
            return false;
        }

        #endregion

        #endregion
    }
}
