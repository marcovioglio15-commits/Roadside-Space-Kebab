using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Defines reusable menu content and presentation consumed by editor-generated scene objects.</summary>
    [CreateAssetMenu(menuName = "Studio/Menu Preset", fileName = "MenuPreset")]
    public sealed class MenuPreset : ScriptableObject
    {
        #region Fields
        [Header("Content")]
        [Tooltip("Main menu heading.")]
        public string Title = "GAME TITLE";
        [Tooltip("Pause menu heading.")]
        public string PauseTitle = "PAUSED";
        [Tooltip("Full scene asset path loaded by Play.")]
        public string GameplayScene = "";
        [Tooltip("Full scene asset path loaded by Main Menu.")]
        public string MainMenuScene = "";
        [Tooltip("Generate a settings overlay.")]
        public bool IncludeSettings = true;
        [Tooltip("Generate a credits overlay for the main menu.")]
        public bool IncludeCredits = true;
        [Tooltip("Show Restart in pause menus.")]
        public bool IncludeRestart = true;
        [Tooltip("Require confirmation before exiting the application.")]
        public bool ConfirmQuit = true;
        [Tooltip("Credits text displayed in the main menu overlay.")]
        public string Credits = "Credits\n\nYour team here";
        [Tooltip("Project-owned controls text shown in Settings.")]
        public string ControlsText = "Move: WASD / Left stick\nLook: Mouse / Right stick\nPause: Esc / Start";
        [Header("Layout")]
        [Tooltip("Canvas reference size used during generation.")]
        public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Menu button size in reference pixels.")]
        public Vector2 ButtonSize = new Vector2(360f, 58f);
        [Tooltip("Vertical spacing between menu buttons.")]
        public float ButtonSpacing = 12f;
        [Tooltip("Canvas sort order above gameplay UI.")]
        public int SortingOrder = 100;
        [Tooltip("Overlay background color.")]
        public Color Background = new Color(0.025f, 0.045f, 0.07f, 0.96f);
        [Tooltip("Optional background art; no art is copied from another project.")]
        public Sprite BackgroundSprite = null;
        [Tooltip("Optional uGUI font; empty uses Unity's built-in font.")]
        public Font Font = null;
        [Header("Interaction Profiles")]
        [Tooltip("Independent main-menu and credits button behavior.")]
        public MenuButtonStyle MainButtons = new MenuButtonStyle();
        [Tooltip("Independent pause-menu button behavior.")]
        public MenuButtonStyle PauseButtons = new MenuButtonStyle();
        [Tooltip("Independent settings and tab button behavior.")]
        public MenuButtonStyle SettingsButtons = new MenuButtonStyle();
        [Header("Navigation")]
        [Tooltip("Menu input and settings focus presentation.")]
        public MenuNavigation Navigation = new MenuNavigation();
        [Header("Settings")]
        [Tooltip("Available user settings and persistence identity.")]
        public MenuSettingsOptions Settings = new MenuSettingsOptions();
        #endregion
    }
}
