using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Stores one interaction state's optional motion and appearance.</summary>
    [Serializable]
    public sealed class MenuVisualState
    {
        #region Fields
        [Header("Transform")]
        [Tooltip("Scale multiplier relative to the generated baseline.")]
        public Vector3 Scale = Vector3.one;
        [Tooltip("Local position offset from the baseline.")]
        public Vector3 Offset = Vector3.zero;
        [Tooltip("Local Euler rotation offset.")]
        public Vector3 Rotation = Vector3.zero;
        [Header("Clip")]
        [Tooltip("Optional authored clip sampled on the selected motion target.")]
        public AnimationClip Clip = null;
        [Header("Graphics")]
        [Tooltip("Optional background sprite for this state.")]
        public Sprite Sprite = null;
        [Tooltip("Background graphic tint.")]
        public Color GraphicColor = new Color(0.09f, 0.15f, 0.21f);
        [Header("Text")]
        [Tooltip("Optional label font; empty retains the generated font.")]
        public Font Font = null;
        [Tooltip("Label size in reference pixels.")]
        public int FontSize = 24;
        [Tooltip("Label style for this state.")]
        public FontStyle FontStyle = FontStyle.Normal;
        [Tooltip("Label color.")]
        public Color TextColor = Color.white;
        #endregion
    }
}
