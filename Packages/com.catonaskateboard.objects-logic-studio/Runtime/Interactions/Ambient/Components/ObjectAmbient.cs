using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Plays a local ambient event while passive interaction availability permits it.</summary>
    [AddComponentMenu("Objects Logic Studio/Play Ambient")]
    public sealed class ObjectAmbient : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Ambient")]
        [Tooltip("Audio Studio event and listening radius.")]
        [SerializeField]
        private AmbientSettings settings = new AmbientSettings();
        private IAudioVoice voice;
        private bool ready;
        private float retry;

        #endregion
        #region Properties

        /// <summary>Saved emitter settings used by presets and the tool.</summary>
        public AmbientSettings Settings => settings;
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.PlayAmbient;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Validates once when the ambient interaction becomes active.</summary>
        private void OnEnable()
        {
            // Missing audio never causes repeated native allocation attempts each frame.
            retry = 0f;
            ready = TryValidate(out string warning);
            if (!ready)
                Debug.LogWarning(warning, this);
        }

        /// <summary>Starts or stops playback only at availability and voice lifetime boundaries.</summary>
        private void Update()
        {
            // Locks, consumption and inventory suspension release the owned ambient voice.
            if (!ready || !Available(InteractionChannels.Passive))
            {
                Stop();
                return;
            }
            if (Time.timeScale <= 0f || voice != null && voice.IsPlaying || Time.unscaledTime < retry)
                return;
            voice = StudioAudio.Play(settings.Event, transform, radius: settings.Radius);
            retry = Time.unscaledTime + 1f;
        }

        /// <summary>Releases this emitter's audio when its object is suspended or destroyed.</summary>
        private void OnDisable()
        {
            // Other ambient emitters retain their own instances.
            Stop();
        }

        /// <summary>Stops this emitter without affecting the global mix.</summary>
        private void Stop()
        {
            // Null handles also cover scenes without an audio adapter.
            voice?.Stop();
            voice = null;
        }

        #endregion
        #region Validation

        /// <summary>Checks saved ambient settings before playback or editor Apply.</summary>
        /// <param name="warning">Receives incomplete settings.</param>
        /// <returns>True when the event and radius are usable.</returns>
        public override bool TryValidate(out string warning)
        {
            // Shared settings validation is also used for preset import and export.
            warning = "Ambient settings are missing.";
            return settings != null && settings.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
