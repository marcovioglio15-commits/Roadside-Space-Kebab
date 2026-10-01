using System.Collections.Generic;
using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace RoadsideSpaceKebab.Audio
{
    /// <summary>Runs the scene's Audio Studio catalog through the installed FMOD integration.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameplayAudio : MonoBehaviour, IStudioAudioBackend
    {
        #region Fields

        [Header("Scene Audio")]
        [Tooltip("Event keys, parameter defaults and playback options used by gameplay interactions.")]
        [SerializeField]
        private AudioPreset preset;
        [Tooltip("Optional music key started when this gameplay scene opens.")]
        [SerializeField]
        private string music = "mus_radio";
        private readonly Dictionary<string, AudioBinding> bindings = new Dictionary<string, AudioBinding>();
        private readonly Dictionary<string, Queue<float>> history = new Dictionary<string, Queue<float>>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private readonly List<GameplayVoice> voices = new List<GameplayVoice>();
        private bool initialized;
        private float deadline;
        private IAudioVoice musicVoice;
        private float nextMusic;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Registers the scene catalog before player and interaction initialization.</summary>
        private void OnEnable()
        {
            // An authored scene component supplies configuration without creating runtime UI.
            Initialize();
        }

        /// <summary>Rebinds loaded components when Unity enters Play without reloading the scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Restore()
        {
            // This one-time scan also supports starting directly in the gameplay scene.
            foreach (GameplayAudio audio in FindObjectsByType<GameplayAudio>())
                if (audio.isActiveAndEnabled)
                    audio.Initialize();
        }

        /// <summary>Indexes keys once and queues music until bank loading completes.</summary>
        private void Initialize()
        {
            // Multiple scene adapters never silently replace the active catalog.
            if (initialized && ReferenceEquals(StudioAudio.Backend, this))
                return;
            Shutdown();
            if (StudioAudio.Backend != null || preset == null)
            {
                Debug.LogWarning("Gameplay audio needs one active adapter and an Audio Preset.", this);
                return;
            }
            foreach (AudioBinding binding in preset.Events)
                if (binding != null && !string.IsNullOrWhiteSpace(binding.Key) && !bindings.TryAdd(binding.Key, binding))
                    Warn(binding.Key, "Duplicate audio key: " + binding.Key);
            StudioAudio.Backend = this;
            initialized = true;
            deadline = Time.realtimeSinceStartup + 15f;
            if (!string.IsNullOrEmpty(music))
                musicVoice = Play(music, null, Vector3.zero, null, null, 0f);
        }

        /// <summary>Updates only live voices and ends startup polling after a bounded bank wait.</summary>
        private void Update()
        {
            // Native voices own their lifetimes even after an emitted item is consumed.
            if (!initialized)
                return;
            if (preset.Playback.Enabled && !string.IsNullOrEmpty(music) && Time.unscaledTime >= nextMusic
                && (musicVoice == null || !musicVoice.IsPlaying))
            {
                musicVoice = Play(music, null, Vector3.zero, null, null, 0f);
                nextMusic = Time.unscaledTime + 1f;
            }
            if (voices.Count == 0)
                return;
            bool ready = FMODUnity.RuntimeManager.HaveAllBanksLoaded;
            for (int index = voices.Count - 1; index >= 0; index--)
            {
                GameplayVoice voice = voices[index];
                if (!preset.Playback.Enabled || !ready && Time.realtimeSinceStartup > deadline)
                    voice.Stop();
                else
                    voice.Tick(ready, Time.timeScale <= 0f, this);
                if (!voice.IsPlaying)
                    voices.RemoveAt(index);
            }
            if (!ready && Time.realtimeSinceStartup > deadline)
                Warn("banks", "Gameplay audio timed out waiting for FMOD banks.");
        }

        /// <summary>Stops scene-owned music and effects when leaving the scene.</summary>
        private void OnDisable()
        {
            // Item destruction does not stop sounds; scene adapter teardown does.
            Shutdown();
        }

        /// <summary>Releases handles and registration without changing another scene adapter.</summary>
        private void Shutdown()
        {
            // Explicit cleanup supports repeated Play sessions with disabled domain reload.
            foreach (GameplayVoice voice in voices)
                voice.Stop();
            voices.Clear();
            musicVoice = null;
            nextMusic = 0f;
            bindings.Clear();
            history.Clear();
            warnings.Clear();
            initialized = false;
            if (ReferenceEquals(StudioAudio.Backend, this))
                StudioAudio.Backend = null;
        }

        #endregion
        #region Playback

        /// <summary>Queues one configured event, preserving its position if the source is consumed immediately.</summary>
        /// <param name="key">Unique Audio Studio binding key.</param>
        /// <param name="source">Optional moving emitter.</param>
        /// <param name="position">Initial world position.</param>
        /// <param name="parameter">Optional labeled local parameter.</param>
        /// <param name="label">FMOD parameter label.</param>
        /// <param name="radius">Optional positive attenuation override.</param>
        /// <returns>A queued or playing voice, or null when suppressed.</returns>
        public IAudioVoice Play(string key, Transform source, Vector3 position, string parameter, string label, float radius)
        {
            // Validate configuration at the event boundary, never through runtime reflection.
            if (!initialized || !preset.Playback.Enabled || string.IsNullOrEmpty(key))
                return null;
            if (!bindings.TryGetValue(key, out AudioBinding binding) || string.IsNullOrEmpty(binding.EventPath))
            {
                if (preset.Playback.LogMissingEvents)
                    Warn(key, "Assign an Audio Studio event for key: " + key);
                return null;
            }
            if (binding.SingleInstance)
                foreach (GameplayVoice voice in voices)
                    if (voice.Key == key && voice.IsPlaying)
                        return null;
            if (!AcceptRate(binding))
                return null;
            GameplayVoice created = new GameplayVoice(binding, preset.Playback, source, position, parameter, label, radius);
            voices.Add(created);
            created.Tick(FMODUnity.RuntimeManager.HaveAllBanksLoaded, Time.timeScale <= 0f, this);
            return created;
        }

        /// <summary>Applies the preset's optional per-key repetition window.</summary>
        /// <param name="binding">Event options.</param>
        /// <returns>True when another instance is permitted.</returns>
        private bool AcceptRate(AudioBinding binding)
        {
            // Disabled limits allocate no timing queue.
            if (!binding.LimitRate)
                return true;
            if (!history.TryGetValue(binding.Key, out Queue<float> times))
            {
                times = new Queue<float>();
                history.Add(binding.Key, times);
            }
            while (times.Count > 0 && Time.time - times.Peek() >= binding.WindowSeconds)
                times.Dequeue();
            if (times.Count >= binding.MaxPlays)
                return false;
            times.Enqueue(Time.time);
            return true;
        }

        /// <summary>Reports each missing binding or parameter once per scene lifetime.</summary>
        /// <param name="key">Diagnostic identity.</param>
        /// <param name="message">Actionable configuration warning.</param>
        internal void Warn(string key, string message)
        {
            // Repeated collisions never flood the console with an identical bank error.
            if (warnings.Add(key))
                Debug.LogWarning(message, this);
        }

        #endregion
        #endregion
    }
}
