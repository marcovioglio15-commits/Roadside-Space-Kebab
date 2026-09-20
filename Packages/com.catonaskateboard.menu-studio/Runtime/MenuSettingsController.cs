using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Edits settings as a draft on preauthored controls and applies only on confirmation.</summary>
    public sealed class MenuSettingsController : MonoBehaviour
    {
        #region Fields
        [Header("Configuration")]
        [Tooltip("Owning menu and settings configuration.")]
        public MenuHost Host;
        [Tooltip("Generated settings tab panels in display order.")]
        public GameObject[] Tabs = Array.Empty<GameObject>();
        [Tooltip("Initial focus for each generated tab.")]
        public Selectable[] FirstSelections = Array.Empty<Selectable>();
        [Tooltip("Complete focus order for each tab, including shared footer buttons.")]
        public MenuTabNavigation[] Navigation = Array.Empty<MenuTabNavigation>();
        [Header("Audio Controls")]
        [Tooltip("Preauthored Master volume slider.")]
        public Slider Master;
        [Tooltip("Preauthored Music volume slider.")]
        public Slider Music;
        [Tooltip("Preauthored SFX volume slider.")]
        public Slider Sfx;
        [Header("Video Controls")]
        [Tooltip("Preauthored fullscreen button label.")]
        public Text FullscreenLabel;
        [Tooltip("Preauthored resolution button label.")]
        public Text ResolutionLabel;
        [Tooltip("Preauthored VSync button label.")]
        public Text VSyncLabel;
        [Tooltip("Preauthored target frame-rate button label.")]
        public Text FrameRateLabel;
        [Tooltip("Existing frame-rate row, hidden when VSync controls presentation.")]
        public GameObject FrameRateRow;
        [Header("Project Audio Adapters")]
        [Tooltip("Applied master volume for a project-specific backend such as FMOD.")]
        public UnityEvent<float> MasterApplied = new UnityEvent<float>();
        [Tooltip("Applied music volume for a project-specific backend.")]
        public UnityEvent<float> MusicApplied = new UnityEvent<float>();
        [Tooltip("Applied SFX volume for a project-specific backend.")]
        public UnityEvent<float> SfxApplied = new UnityEvent<float>();
        private SettingsValues saved;
        private SettingsValues draft;
        private Resolution[] resolutions = Array.Empty<Resolution>();
        private int tab;
        private bool editing;
        private bool initialized;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Loads persisted settings once, even when the settings page starts hidden.</summary>
        public void Initialize()
        {
            // The generated host invokes initialization before opening any settings draft.
            if (initialized || Host == null || Host.Preset == null)
                return;
            initialized = true;
            resolutions = Screen.resolutions;
            string key = Host.Preset.Settings.PreferenceKey;
            saved = new SettingsValues
            {
                Master = PlayerPrefs.GetFloat(key + ".Master", 1f),
                Music = PlayerPrefs.GetFloat(key + ".Music", 1f),
                Sfx = PlayerPrefs.GetFloat(key + ".Sfx", 1f),
                Fullscreen = PlayerPrefs.GetInt(key + ".Fullscreen", Screen.fullScreen ? 1 : 0) != 0,
                VSync = PlayerPrefs.GetInt(key + ".VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0,
                FrameRate = PlayerPrefs.GetInt(key + ".FrameRate", Application.targetFrameRate),
                Width = PlayerPrefs.GetInt(key + ".Width", Screen.width),
                Height = PlayerPrefs.GetInt(key + ".Height", Screen.height)
            };
            ApplyValues(saved);
        }

        /// <summary>Copies applied settings into an isolated editing session.</summary>
        public void BeginEdit()
        {
            // Slider changes remain local until Apply, avoiding audible or visual rollback flashes.
            Initialize();
            draft = saved;
            editing = true;
            Master?.SetValueWithoutNotify(draft.Master);
            Music?.SetValueWithoutNotify(draft.Music);
            Sfx?.SetValueWithoutNotify(draft.Sfx);
            SelectTab(0);
            RefreshLabels();
        }

        /// <summary>Discards a settings proposal without modifying applied values.</summary>
        public void Discard()
        {
            // No live system changes occur during editing, so discard needs no side effects.
            editing = false;
            draft = saved;
        }
        #endregion

        #region Editing
        /// <summary>Commits current controls, persists preferences and returns to the menu home.</summary>
        public void Apply()
        {
            // Read only controls generated for the selected feature set.
            if (!editing)
                return;
            draft.Master = Master != null ? Master.value : saved.Master;
            draft.Music = Music != null ? Music.value : saved.Music;
            draft.Sfx = Sfx != null ? Sfx.value : saved.Sfx;
            saved = draft;
            editing = false;
            ApplyValues(saved);
            string key = Host.Preset.Settings.PreferenceKey;
            PlayerPrefs.SetFloat(key + ".Master", saved.Master);
            PlayerPrefs.SetFloat(key + ".Music", saved.Music);
            PlayerPrefs.SetFloat(key + ".Sfx", saved.Sfx);
            PlayerPrefs.SetInt(key + ".Fullscreen", saved.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(key + ".VSync", saved.VSync ? 1 : 0);
            PlayerPrefs.SetInt(key + ".FrameRate", saved.FrameRate);
            PlayerPrefs.SetInt(key + ".Width", saved.Width);
            PlayerPrefs.SetInt(key + ".Height", saved.Height);
            PlayerPrefs.Save();
            Host.ReturnHome();
        }

        /// <summary>Toggles the pending fullscreen mode.</summary>
        public void ToggleFullscreen()
        {
            // Display mode changes are deferred until Apply.
            draft.Fullscreen = !draft.Fullscreen;
            RefreshLabels();
        }

        /// <summary>Toggles the pending VSync setting and updates frame-rate availability.</summary>
        public void ToggleVSync()
        {
            // VSync makes a target frame-rate field irrelevant on desktop platforms.
            draft.VSync = !draft.VSync;
            RefreshLabels();
        }

        /// <summary>Cycles supported resolutions without constructing a runtime dropdown.</summary>
        public void CycleResolution()
        {
            // Some platforms report no resolutions; retain the current dimensions in that case.
            if (resolutions.Length == 0)
                return;
            int index = Array.FindIndex(resolutions, resolution => resolution.width == draft.Width && resolution.height == draft.Height);
            Resolution next = resolutions[(index + 1) % resolutions.Length];
            draft.Width = next.width;
            draft.Height = next.height;
            RefreshLabels();
        }

        /// <summary>Cycles the preset's explicitly allowed target frame rates.</summary>
        public void CycleFrameRate()
        {
            // Authored choices remain unchanged; invalid lists are reported by the editor validator.
            if (Host.Preset.Settings.FrameRates.Count == 0)
                return;
            int index = Host.Preset.Settings.FrameRates.IndexOf(draft.FrameRate);
            draft.FrameRate = Host.Preset.Settings.FrameRates[(index + 1) % Host.Preset.Settings.FrameRates.Count];
            RefreshLabels();
        }
        #endregion

        #region Presentation
        /// <summary>Selects a settings tab and restores its first control.</summary>
        /// <param name="index">Generated tab index.</param>
        public void SelectTab(int index)
        {
            // Tab content already exists in the scene; only its active state changes.
            if (index < 0 || index >= Tabs.Length)
                return;
            tab = index;
            for (int current = 0; current < Tabs.Length; current++)
                Tabs[current].SetActive(current == index);
            RefreshNavigation();
            if (index < FirstSelections.Length && FirstSelections[index] != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(FirstSelections[index].gameObject);
        }

        /// <summary>Moves between generated tabs using the preset's wrapping behavior.</summary>
        /// <param name="direction">Negative for previous, positive for next.</param>
        public void ChangeTab(int direction)
        {
            // Disabled settings sections are absent from the generated tab list.
            if (Tabs.Length == 0)
                return;
            int next = tab + direction;
            if (Host.Preset.Navigation.WrapTabs)
                next = (next + Tabs.Length) % Tabs.Length;
            SelectTab(next);
        }

        /// <summary>Updates existing labels after a settings draft changes.</summary>
        private void RefreshLabels()
        {
            // Format text on interaction only, not in a frame callback.
            if (FullscreenLabel != null)
                FullscreenLabel.text = "Display: " + (draft.Fullscreen ? "Fullscreen" : "Windowed");
            if (ResolutionLabel != null)
                ResolutionLabel.text = "Resolution: " + draft.Width + " x " + draft.Height;
            if (VSyncLabel != null)
                VSyncLabel.text = "VSync: " + (draft.VSync ? "On" : "Off");
            if (FrameRateLabel != null)
                FrameRateLabel.text = "Frame rate: " + (draft.FrameRate < 0 ? "Platform default" : draft.FrameRate.ToString());
            if (FrameRateRow != null)
                FrameRateRow.SetActive(!draft.VSync);
            RefreshNavigation();
        }

        /// <summary>Skips inactive options and reconnects shared footer buttons to the current tab.</summary>
        private void RefreshNavigation()
        {
            // VSync and tab switches can remove a selectable from the current focus path.
            if (tab >= 0 && tab < Navigation.Length)
                MenuNavigationUtility.Link(Navigation[tab].Controls, Host.Preset.Navigation.WrapButtons, true);
        }

        /// <summary>Applies only the settings sections owned by this preset.</summary>
        /// <param name="values">Confirmed settings values.</param>
        private void ApplyValues(SettingsValues values)
        {
            // Project adapters receive explicit volume events; no reflection or FMOD dependency is needed.
            MenuSettingsOptions options = Host.Preset.Settings;
            if (options.Audio)
            {
                if (options.UseUnityAudioListener)
                    AudioListener.volume = values.Master;
                MasterApplied.Invoke(values.Master);
                MusicApplied.Invoke(values.Music);
                SfxApplied.Invoke(values.Sfx);
            }
            if (!options.Video)
                return;
            if (options.VSync)
                QualitySettings.vSyncCount = values.VSync ? 1 : 0;
            if (options.FrameRate && !values.VSync)
                Application.targetFrameRate = values.FrameRate;
            if (options.Resolution || options.Fullscreen)
                Screen.SetResolution(options.Resolution ? values.Width : Screen.width, options.Resolution ? values.Height : Screen.height,
                    options.Fullscreen ? values.Fullscreen : Screen.fullScreen);
        }
        #endregion
        #endregion

        /// <summary>Keeps a lightweight settings proposal separate from the applied values.</summary>
        private struct SettingsValues
        {
            #region Fields
            internal float Master;
            internal float Music;
            internal float Sfx;
            internal bool Fullscreen;
            internal bool VSync;
            internal int FrameRate;
            internal int Width;
            internal int Height;
            #endregion
        }
    }
}
