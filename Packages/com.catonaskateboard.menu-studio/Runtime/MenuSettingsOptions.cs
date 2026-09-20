using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Controls generated settings sections and their saved preferences.</summary>
    [Serializable]
    public sealed class MenuSettingsOptions
    {
        #region Fields
        [Header("Persistence")]
        [Tooltip("Project-specific PlayerPrefs prefix.")]
        public string PreferenceKey = "MenuStudio";
        [Header("Sections")]
        [Tooltip("Generate audio settings.")]
        public bool Audio = true;
        [Tooltip("Generate display settings.")]
        public bool Video = true;
        [Tooltip("Generate the project controls reference tab.")]
        public bool Controls = true;
        [Header("Audio")]
        [Tooltip("Apply Master volume to Unity AudioListener. Disable when a project audio adapter owns it.")]
        public bool UseUnityAudioListener = true;
        [Header("Video")]
        [Tooltip("Expose fullscreen/windowed switching.")]
        public bool Fullscreen = true;
        [Tooltip("Expose supported display resolutions.")]
        public bool Resolution = true;
        [Tooltip("Expose vertical synchronization.")]
        public bool VSync = true;
        [Tooltip("Expose a frame-rate target when VSync is disabled.")]
        public bool FrameRate = true;
        [Tooltip("Frame-rate choices; -1 uses the platform default.")]
        public List<int> FrameRates = new List<int> { 30, 60, 90, 120, 144, 240, -1 };
        #endregion
    }
}
