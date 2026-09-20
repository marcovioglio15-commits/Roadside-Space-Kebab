using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Stores action references and focus behavior for preauthored menu controls.</summary>
    [Serializable]
    public sealed class MenuNavigation
    {
        #region Fields
        [Header("Availability")]
        [Tooltip("Enable keyboard and gamepad navigation.")]
        public bool Enabled = true;
        [Tooltip("Wrap from last to first button in a menu.")]
        public bool WrapButtons = true;
        [Tooltip("Pointer hover also changes keyboard and gamepad focus.")]
        public bool FollowPointer = true;
        [Header("Input Actions")]
        [Tooltip("Vector2 navigation action; empty uses generated UI bindings.")]
        public InputActionReference Move = null;
        [Tooltip("Button action to activate the selected control.")]
        public InputActionReference Submit = null;
        [Tooltip("Button action that closes the top overlay and discards settings edits.")]
        public InputActionReference Cancel = null;
        [Tooltip("Button action that opens or closes a pause menu.")]
        public InputActionReference Pause = null;
        [Tooltip("Button action selecting the previous Settings tab.")]
        public InputActionReference PreviousTab = null;
        [Tooltip("Button action selecting the next Settings tab.")]
        public InputActionReference NextTab = null;
        [Tooltip("Optional dedicated close action for Credits.")]
        public InputActionReference CreditsClose = null;
        [Header("Settings Tabs")]
        [Tooltip("Wrap settings tab navigation at either end.")]
        public bool WrapTabs = true;
        [Header("Repeat")]
        [Tooltip("Unscaled delay before held navigation repeats.")]
        public float RepeatDelay = 0.32f;
        [Tooltip("Unscaled delay between repeated navigation moves.")]
        public float RepeatInterval = 0.1f;
        [Tooltip("Stick deadzone applied to the generated navigation action.")]
        public float Deadzone = 0.55f;
        [Header("Settings Focus")]
        [Tooltip("Override the selected Settings option appearance.")]
        public bool CustomizeFocus = true;
        [Tooltip("Change the focused option background.")]
        public bool FocusGraphic = true;
        [Tooltip("Unfocused settings background.")]
        public Color UnselectedGraphic = new Color(0.09f, 0.13f, 0.16f);
        [Tooltip("Focused settings background.")]
        public Color SelectedGraphic = new Color(0.18f, 0.31f, 0.39f);
        [Tooltip("Change settings text color and style.")]
        public bool FocusText = true;
        [Tooltip("Unfocused settings text.")]
        public Color UnselectedText = new Color(0.82f, 0.86f, 0.9f);
        [Tooltip("Focused settings text.")]
        public Color SelectedText = Color.white;
        [Tooltip("Unfocused text style.")]
        public FontStyle UnselectedStyle = FontStyle.Normal;
        [Tooltip("Focused text style.")]
        public FontStyle SelectedStyle = FontStyle.Bold;
        [Tooltip("Scale the selected settings row.")]
        public bool FocusScale = true;
        [Tooltip("Unfocused row scale.")]
        public Vector3 UnselectedScale = Vector3.one;
        [Tooltip("Focused row scale.")]
        public Vector3 SelectedScale = new Vector3(1.025f, 1.025f, 1f);
        [Tooltip("Show the preauthored outline on the selected option.")]
        public bool FocusOutline = true;
        [Tooltip("Focus outline tint.")]
        public Color OutlineColor = new Color(0.98f, 0.78f, 0.15f);
        [Tooltip("Focus outline displacement.")]
        public Vector2 OutlineDistance = new Vector2(3f, -3f);
        #endregion
    }
}
