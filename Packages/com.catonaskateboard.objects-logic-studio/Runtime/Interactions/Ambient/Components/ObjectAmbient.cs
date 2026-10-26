using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Plays local ambience or an interactive radio, replacing playback and input when its mode changes.</summary>
    [AddComponentMenu("Objects Logic Studio/Play Ambient")]
    public sealed class ObjectAmbient : ObjectRequestedInteraction
    {
        #region Fields

        [Header("Ambient")]
        [Tooltip("Audio Studio event and listening radius.")]
        [SerializeField]
        private AmbientSettings settings = new AmbientSettings();
        private IAudioVoice voice;
        private bool ready;
        private AmbientMode mode;
        private string ambientEvent;
        private float retry;
        private readonly RadioAudioRun radio = new RadioAudioRun();

        #endregion
        #region Properties

        /// <summary>Saved emitter settings used by presets and the tool.</summary>
        public AmbientSettings Settings => settings;
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.PlayAmbient;
        /// <summary>Only a radio participates in player input routing.</summary>
        public override bool UsesInput => settings.Mode == AmbientMode.Radio;
        /// <summary>Reach and aim required to change the local radio channel.</summary>
        public override TransferTargetSettings Target => settings.Radio.Target;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Validates once when the ambient interaction becomes active.</summary>
        protected override void OnEnable()
        {
            // Re-enabling uses the current settings without retaining stale emitter ownership.
            Configure();
        }

        /// <summary>Rebuilds retained emitter state at Play entry without carrying a previous session's radio channel.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectAmbient ambient in FindObjectsByType<ObjectAmbient>())
                if (ambient.isActiveAndEnabled)
                    ambient.Configure(true);
        }

        /// <summary>Starts or stops playback only at availability and voice lifetime boundaries.</summary>
        private void Update()
        {
            // Mode and ambient event edits replace playback and input membership at one boundary.
            if (settings != null && (settings.Mode != mode || mode == AmbientMode.Ambient && settings.Event != ambientEvent))
                Configure();
            // Locks, consumption and inventory suspension release the owned ambient voice.
            if (!ready || !Available(InteractionChannels.Passive))
            {
                Stop();
                return;
            }
            if (settings.Mode == AmbientMode.Radio)
            {
                radio.Tick(this, settings.Radio, settings.Radius);
                return;
            }
            if (Time.timeScale <= 0f || voice != null && voice.IsPlaying || Time.unscaledTime < retry)
                return;
            voice = StudioAudio.Play(settings.Event, transform, radius: settings.Radius);
            retry = Time.unscaledTime + 1f;
        }

        /// <summary>Releases this emitter's audio when its object is suspended or destroyed.</summary>
        protected override void OnDisable()
        {
            // Other ambient emitters retain their own instances.
            Stop();
            base.OnDisable();
        }

        /// <summary>Stops this emitter without affecting the global mix.</summary>
        /// <param name="reset">Whether a new Play session restores the initial radio channel.</param>
        private void Stop(bool reset = false)
        {
            // Null handles also cover scenes without an audio adapter.
            voice?.Stop();
            voice = null;
            radio.Stop(this, reset);
        }

        /// <summary>Replaces the active emitter and its input registration after a mode or event change.</summary>
        /// <param name="reset">Whether the next radio playback must start from its authored initial channel.</param>
        private void Configure(bool reset = false)
        {
            // Unregister before validating so a passive or invalid emitter cannot retain a radio binding.
            Stop(reset);
            base.OnDisable();
            mode = settings != null ? settings.Mode : AmbientMode.Ambient;
            ambientEvent = settings?.Event;
            retry = 0f;
            ready = TryValidate(out string warning);
            if (ready)
                base.OnEnable();
            else
                Debug.LogWarning(warning, this);
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
            return settings != null && settings.TryValidate(out warning) && ValidateInput(UsesInput, Action, out warning);
        }

        #endregion
        #region Radio Requests

        /// <summary>Checks availability before the observer tests radio reach and visibility.</summary>
        /// <returns>True when one channel change may claim the bound action.</returns>
        internal override bool CanRequest()
        {
            return ready && UsesInput && Available(InteractionChannels.Passive);
        }

        /// <summary>Cycles one local channel after normal observer input arbitration succeeds.</summary>
        /// <returns>True when the selected radio changed its channel.</returns>
        internal override bool Request()
        {
            if (!CanRequest())
                return false;
            Signal(InteractionMoment.Started);
            radio.Cycle(this, settings.Radio, settings.Radius);
            Signal(InteractionMoment.Completed);
            return true;
        }

        #endregion
        #endregion
    }
}
