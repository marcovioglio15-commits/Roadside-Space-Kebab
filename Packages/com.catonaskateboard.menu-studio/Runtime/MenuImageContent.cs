using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Assigns state images to a stable generated button without runtime object creation.</summary>
    [Serializable]
    public sealed class MenuImageContent
    {
        #region Fields
        [Header("Identity")]
        [Tooltip("Generated button name, for example Play or Resume.")]
        public string ButtonId = "";
        [Tooltip("Keep the source image proportions.")]
        public bool PreserveAspect = true;
        [Header("State Images")]
        [Tooltip("Image shown at rest.")]
        public Sprite Normal = null;
        [Tooltip("Focused image; empty falls back to Normal.")]
        public Sprite Hover = null;
        [Tooltip("Pressed image; empty falls back to Normal.")]
        public Sprite Pressed = null;
        [Tooltip("Disabled image; empty falls back to Normal.")]
        public Sprite Disabled = null;
        [Header("State Tints")]
        [Tooltip("Normal image tint.")]
        public Color NormalColor = Color.white;
        [Tooltip("Focused image tint.")]
        public Color HoverColor = Color.white;
        [Tooltip("Pressed image tint.")]
        public Color PressedColor = Color.white;
        [Tooltip("Disabled image tint.")]
        public Color DisabledColor = new Color(1f, 1f, 1f, 0.45f);
        #endregion
    }
}
