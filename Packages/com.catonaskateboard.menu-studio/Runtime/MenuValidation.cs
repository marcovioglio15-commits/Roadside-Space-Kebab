using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Reports menu values that cannot produce a reliable generated scene.</summary>
    public static class MenuValidation
    {
        #region Methods
        #region Preset Validation
        /// <summary>Validates active menu features without changing any authored values.</summary>
        /// <param name="preset">Configuration to inspect.</param>
        /// <param name="messages">Destination warning list.</param>
        public static void Collect(MenuPreset preset, List<string> messages)
        {
            // Validation never clamps geometry, timings, colors or action references.
            if (preset == null)
            {
                messages.Add("Select a menu preset.");
                return;
            }
            if (!Positive(preset.ReferenceResolution.x) || !Positive(preset.ReferenceResolution.y)
                || !Positive(preset.ButtonSize.x) || !Positive(preset.ButtonSize.y)
                || !float.IsFinite(preset.ButtonSpacing) || preset.ButtonSpacing < 0f)
                messages.Add("Reference resolution and button sizes must be positive; spacing must be nonnegative.");
            ValidateStyle(preset.MainButtons, "Main menu", messages);
            ValidateStyle(preset.PauseButtons, "Pause menu", messages);
            if (preset.IncludeSettings)
            {
                ValidateStyle(preset.SettingsButtons, "Settings", messages);
                if (string.IsNullOrWhiteSpace(preset.Settings.PreferenceKey))
                    messages.Add("Settings need a nonempty preferences key.");
                if (!preset.Settings.Audio && !preset.Settings.Video && !preset.Settings.Controls)
                    messages.Add("Enable at least one Settings section or disable Settings.");
                if (preset.Settings.Video && preset.Settings.FrameRate)
                {
                    if (preset.Settings.FrameRates.Count == 0)
                        messages.Add("Frame-rate choices cannot be empty.");
                    foreach (int rate in preset.Settings.FrameRates)
                        if (rate != -1 && rate <= 0)
                            messages.Add("Frame-rate choices must be positive or -1 for the platform default.");
                }
            }
            MenuNavigation input = preset.Navigation;
            if (!input.Enabled)
                return;
            if (!Positive(input.RepeatInterval) || !Positive(input.RepeatDelay) || !float.IsFinite(input.Deadzone) || input.Deadzone < 0f || input.Deadzone >= 1f)
                messages.Add("Navigation needs positive repeat timings and a deadzone in [0, 1).");
            ValidateAction(input.Move, false, "Move", messages);
            ValidateAction(input.Submit, true, "Submit", messages);
            ValidateAction(input.Cancel, true, "Cancel", messages);
            ValidateAction(input.Pause, true, "Pause", messages);
            ValidateAction(input.PreviousTab, true, "Previous Tab", messages);
            ValidateAction(input.NextTab, true, "Next Tab", messages);
            ValidateAction(input.CreditsClose, true, "Credits Close", messages);
        }

        /// <summary>Checks enabled button profile features and image identity collisions.</summary>
        /// <param name="style">Interaction profile.</param>
        /// <param name="label">Owning menu label.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void ValidateStyle(MenuButtonStyle style, string label, List<string> messages)
        {
            // Inactive overrides may retain unfinished values until enabled.
            if (!style.Enabled)
                return;
            if (style.Motion != MenuMotionMode.None && (!float.IsFinite(style.TransitionSeconds) || style.TransitionSeconds < 0f))
                messages.Add(label + ": transition duration must be finite and nonnegative.");
            if (style.HoverPulse && (!Positive(style.PulseSeconds) || !style.LoopPulse && style.PulseCycles < 1))
                messages.Add(label + ": pulse duration and finite cycle count must be positive.");
            foreach (MenuVisualState state in new[] { style.Normal, style.Hover, style.Pressed, style.Disabled })
            {
                if (style.OverrideText && state.FontSize <= 0)
                    messages.Add(label + ": font sizes must be positive.");
                if (!Finite(state.Scale) || !Finite(state.Offset) || !Finite(state.Rotation))
                    messages.Add(label + ": transform values must be finite.");
            }
            if (style.ContentMode != MenuContentMode.Image)
                return;
            HashSet<string> ids = new HashSet<string>();
            foreach (MenuImageContent content in style.Images)
                if (string.IsNullOrWhiteSpace(content.ButtonId) || !ids.Add(content.ButtonId) || content.Normal == null)
                    messages.Add(label + ": image entries need unique button IDs and a Normal sprite.");
        }

        /// <summary>Checks the expected action shape without rejecting generated defaults.</summary>
        /// <param name="reference">Optional authored action override.</param>
        /// <param name="button">Whether the consumer expects a Button action.</param>
        /// <param name="label">Control label.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void ValidateAction(InputActionReference reference, bool button, string label, List<string> messages)
        {
            // Empty references select generated defaults; broken references remain visible warnings.
            if (reference == null)
                return;
            if (reference.action == null || reference.action.bindings.Count == 0
                || button && reference.action.type != InputActionType.Button
                || !button && reference.action.expectedControlType != "Vector2")
                messages.Add(label + ": choose a bound " + (button ? "Button" : "Vector2") + " action.");
        }
        #endregion

        #region Numeric Checks
        /// <summary>Checks a strictly positive finite value.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>Whether the value is positive and finite.</returns>
        private static bool Positive(float value)
        {
            // NaN and infinity are not valid layout or timing inputs.
            return float.IsFinite(value) && value > 0f;
        }

        /// <summary>Checks that a vector can be applied safely to a Transform.</summary>
        /// <param name="value">Authored vector.</param>
        /// <returns>Whether every component is finite.</returns>
        private static bool Finite(Vector3 value)
        {
            // Validation reports the original values instead of replacing them.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
        #endregion
        #endregion
    }
}
