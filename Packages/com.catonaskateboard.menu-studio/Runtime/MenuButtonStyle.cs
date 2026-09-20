using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Keeps per-menu motion, content, sprite, font and color controls independent.</summary>
    [Serializable]
    public sealed class MenuButtonStyle
    {
        #region Fields
        [Header("Availability")]
        [Tooltip("Apply this interaction profile.")]
        public bool Enabled = true;
        [Header("Content")]
        [Tooltip("Use the label or the per-button state images.")]
        public MenuContentMode ContentMode = MenuContentMode.Text;
        [Tooltip("Image content matched by stable generated button IDs.")]
        public List<MenuImageContent> Images = new List<MenuImageContent>();
        [Header("Motion")]
        [Tooltip("Transform transitions, clips, both or no animation.")]
        public MenuMotionMode Motion = MenuMotionMode.Transform;
        [Tooltip("Animate the whole button or only its content.")]
        public MenuMotionTarget MotionTarget = MenuMotionTarget.WholeButton;
        [Tooltip("Duration of state transitions.")]
        public float TransitionSeconds = 0.12f;
        [Tooltip("Keep feedback animated while time scale is zero.")]
        public bool UnscaledTime = true;
        [Tooltip("Pulse from the baseline to the hover state and back.")]
        public bool HoverPulse = false;
        [Tooltip("Duration of one complete hover pulse.")]
        public float PulseSeconds = 0.34f;
        [Tooltip("Completed pulse cycles before settling when not looping.")]
        public int PulseCycles = 1;
        [Tooltip("Repeat hover pulses while selected.")]
        public bool LoopPulse = false;
        [Header("Appearance")]
        [Tooltip("Override the button background sprite in each state.")]
        public bool OverrideSprites = false;
        [Tooltip("Treat missing state sprites as invisible while retaining hit testing.")]
        public bool AllowEmptySprites = false;
        [Tooltip("Apply per-state background tints.")]
        public bool OverrideGraphicColors = true;
        [Tooltip("Apply per-state label fonts, sizes, styles and colors.")]
        public bool OverrideText = true;
        [Header("States")]
        [Tooltip("Normal button appearance.")]
        public MenuVisualState Normal = new MenuVisualState();
        [Tooltip("Pointer hover and keyboard or gamepad focus.")]
        public MenuVisualState Hover = new MenuVisualState { Scale = new Vector3(1.04f, 1.04f, 1f), GraphicColor = new Color(0.18f, 0.35f, 0.45f), FontStyle = FontStyle.Bold, FontSize = 26 };
        [Tooltip("Pressed button appearance.")]
        public MenuVisualState Pressed = new MenuVisualState { Scale = new Vector3(0.97f, 0.97f, 1f), GraphicColor = new Color(0.15f, 0.6f, 0.65f) };
        [Tooltip("Non-interactable button appearance.")]
        public MenuVisualState Disabled = new MenuVisualState { GraphicColor = new Color(0.1f, 0.12f, 0.15f, 0.45f), TextColor = new Color(1f, 1f, 1f, 0.45f) };
        [Header("Audio Signals")]
        [Tooltip("Optional project-owned audio key emitted on hover; no audio backend is included.")]
        public string HoverCue = "";
        [Tooltip("Optional project-owned audio key emitted on press.")]
        public string PressCue = "";
        #endregion
    }
}
