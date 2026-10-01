using System.Collections;
using CatOnASkateboard.AudioStudio;
using CatOnASkateboard.MenuStudio;
using UnityEngine;

namespace RoadsideSpaceKebab.UI
{
    /// <summary>Connects Menu Studio volume and cue events to the project's existing FMOD buses.</summary>
    [RequireComponent(typeof(MenuHost))]
    public sealed class MenuAudioRouting : MonoBehaviour
    {
        #region Fields

        [Header("Audio Configuration")]
        [Tooltip("Audio Studio preset supplying FMOD bus paths, baseline gains and optional menu cue keys.")]
        [SerializeField]
        private AudioPreset preset;
        private MenuHost host;
        private FMOD.Studio.Bus master;
        private FMOD.Studio.Bus music;
        private FMOD.Studio.Bus sfx;
        private float masterVolume = 1f;
        private float musicVolume = 1f;
        private float sfxVolume = 1f;
        private bool ready;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Subscribes before the menu host publishes its saved settings.</summary>
        private void Awake()
        {
            // Runtime callbacks avoid serialized links to transient FMOD handles.
            host = GetComponent<MenuHost>();
            if (host.Settings != null)
            {
                host.Settings.MasterApplied.AddListener(Master);
                host.Settings.MusicApplied.AddListener(Music);
                host.Settings.SfxApplied.AddListener(Sfx);
            }
            host.AudioCue.AddListener(Cue);
        }

        /// <summary>Waits for the project's bank loading before resolving each configured bus once.</summary>
        /// <returns>A startup routine that ends after bank readiness or a reported timeout.</returns>
        private IEnumerator Start()
        {
            // Startup polling ends permanently; volume changes subsequently update cached handles only.
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!FMODUnity.RuntimeManager.HaveAllBanksLoaded && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (preset == null || !FMODUnity.RuntimeManager.HaveAllBanksLoaded)
            {
                Debug.LogWarning("Menu audio needs an Audio Preset and loaded FMOD banks.", this);
                yield break;
            }
            master = Resolve(preset.Routing.MasterBus);
            music = Resolve(preset.Routing.MusicBus);
            sfx = Resolve(preset.Routing.SfxBus);
            ready = true;
            Master(masterVolume);
            Music(musicVolume);
            Sfx(sfxVolume);
        }

        /// <summary>Removes callbacks before the menu hierarchy is released.</summary>
        private void OnDestroy()
        {
            // FMOD owns the buses; menu teardown never releases or stops shared audio resources.
            if (host == null)
                return;
            if (host.Settings != null)
            {
                host.Settings.MasterApplied.RemoveListener(Master);
                host.Settings.MusicApplied.RemoveListener(Music);
                host.Settings.SfxApplied.RemoveListener(Sfx);
            }
            host.AudioCue.RemoveListener(Cue);
        }

        #endregion
        #region Routing

        /// <summary>Resolves an authored bus and reports missing bank routing without substituting another bus.</summary>
        /// <param name="path">FMOD bus path from Audio Studio.</param>
        /// <returns>A valid bus handle or an empty handle after a warning.</returns>
        private FMOD.Studio.Bus Resolve(string path)
        {
            // Incorrect Music/SFX paths remain visible instead of changing Master as an implicit fallback.
            FMOD.RESULT result = FMODUnity.RuntimeManager.StudioSystem.getBus(path, out FMOD.Studio.Bus bus);
            if (result != FMOD.RESULT.OK)
                Debug.LogWarning("Menu audio bus is unavailable: " + path + " (" + result + ").", this);
            return bus;
        }

        /// <summary>Applies the confirmed master preference over the project's baseline mix.</summary>
        /// <param name="value">Normalized user volume.</param>
        private void Master(float value)
        {
            // Retain early settings events until asynchronous bank loading finishes.
            masterVolume = value;
            if (ready && master.isValid())
                master.setVolume(value * preset.Playback.MasterVolume);
        }

        /// <summary>Applies the confirmed music preference to its dedicated bus.</summary>
        /// <param name="value">Normalized user volume.</param>
        private void Music(float value)
        {
            // Music settings never affect effects routed to another bus.
            musicVolume = value;
            if (ready && music.isValid())
                music.setVolume(value * preset.Routing.MusicVolume);
        }

        /// <summary>Applies the confirmed effects preference to its dedicated bus.</summary>
        /// <param name="value">Normalized user volume.</param>
        private void Sfx(float value)
        {
            // Retain the baseline gain authored in Audio Studio.
            sfxVolume = value;
            if (ready && sfx.isValid())
                sfx.setVolume(value * preset.Routing.SfxVolume);
        }

        /// <summary>Plays a configured menu cue using the Audio Studio event catalog.</summary>
        /// <param name="key">Authored hover or press cue key.</param>
        private void Cue(string key)
        {
            // Empty or unassigned cues intentionally remain silent.
            if (!ready || string.IsNullOrEmpty(key) || !preset.Playback.Enabled)
                return;
            foreach (AudioBinding binding in preset.Events)
                if (binding.Key == key && !string.IsNullOrEmpty(binding.EventPath))
                {
                    FMODUnity.RuntimeManager.PlayOneShot(binding.EventPath);
                    return;
                }
        }

        #endregion
        #endregion
    }
}
