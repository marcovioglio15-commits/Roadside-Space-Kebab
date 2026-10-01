using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace RoadsideSpaceKebab.Audio
{
    /// <summary>Owns one native event instance, including bank startup, emitter tracking and cleanup.</summary>
    internal sealed class GameplayVoice : IAudioVoice
    {
        #region State

        private readonly AudioBinding binding;
        private readonly AudioPlayback playback;
        private readonly Transform source;
        private readonly string parameter;
        private readonly string label;
        private readonly float radius;
        private Vector3 position;
        private FMOD.Studio.EventInstance instance;
        private bool started;
        private bool stopped;
        private bool spatial;
        private bool paused;

        /// <summary>Key used for optional single-instance arbitration.</summary>
        internal string Key => binding.Key;
        /// <summary>Queued and paused voices remain owned until stopped or naturally completed.</summary>
        public bool IsPlaying => !stopped;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Captures an event request without requiring banks to have finished loading.</summary>
        /// <param name="binding">Authored event and mix options.</param>
        /// <param name="playback">Default attenuation settings.</param>
        /// <param name="source">Optional moving transform.</param>
        /// <param name="position">Initial world position.</param>
        /// <param name="parameter">Optional local parameter name.</param>
        /// <param name="label">Parameter label.</param>
        /// <param name="radius">Optional radius override.</param>
        internal GameplayVoice(AudioBinding binding, AudioPlayback playback, Transform source, Vector3 position, string parameter, string label, float radius)
        {
            // Retain source position independently from the source object's lifetime.
            this.binding = binding;
            this.playback = playback;
            this.source = source;
            this.position = position;
            this.parameter = parameter;
            this.label = label;
            this.radius = radius;
        }

        /// <summary>Starts a ready request and tracks its lifetime and moving emitter.</summary>
        /// <param name="ready">Whether FMOD finished loading the configured banks.</param>
        /// <param name="pause">Whether gameplay is paused.</param>
        /// <param name="owner">Diagnostic owner.</param>
        internal void Tick(bool ready, bool pause, GameplayAudio owner)
        {
            // Bank loading and gameplay pause retain queued requests without native allocations.
            if (stopped || !ready)
                return;
            if (source != null)
                position = source.position;
            if (!started)
            {
                if (pause)
                    return;
                Start(owner);
                return;
            }
            if (!instance.isValid() || instance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state) != FMOD.RESULT.OK
                || state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
            {
                Stop();
                return;
            }
            if (paused != pause)
            {
                paused = pause;
                instance.setPaused(pause);
            }
            Position();
        }

        /// <summary>Resolves the authored event and sets all parameters before its first sample.</summary>
        /// <param name="owner">Warning sink for missing banks or parameter labels.</param>
        private void Start(GameplayAudio owner)
        {
            // A missing event or label is reported rather than substituted with unrelated audio.
            FMOD.RESULT result = FMODUnity.RuntimeManager.StudioSystem.getEvent(binding.EventPath, out FMOD.Studio.EventDescription description);
            if (result == FMOD.RESULT.OK)
                result = description.createInstance(out instance);
            if (result != FMOD.RESULT.OK)
            {
                owner.Warn(Key, "FMOD event unavailable: " + binding.EventPath + " (" + result + ").");
                Stop();
                return;
            }
            description.is3D(out spatial);
            foreach (AudioParameter value in binding.Parameters)
                if (value.Global)
                    FMODUnity.RuntimeManager.StudioSystem.setParameterByName(value.Name, value.Value);
                else
                    instance.setParameterByName(value.Name, value.Value);
            if (!string.IsNullOrEmpty(parameter) && instance.setParameterByNameWithLabel(parameter, label) != FMOD.RESULT.OK)
            {
                owner.Warn(Key + parameter + label, "FMOD label unavailable: " + Key + " / " + parameter + " / " + label);
                Stop();
                return;
            }
            instance.setVolume(binding.Volume);
            instance.setPitch(binding.Pitch);
            if (spatial && (binding.Spatialize || radius > 0f))
            {
                float maximum = radius > 0f ? radius : binding.OverrideDistances ? binding.MaximumDistance : playback.MaximumDistance;
                float minimum = radius > 0f ? Mathf.Min(1f, radius * 0.1f) : binding.OverrideDistances ? binding.MinimumDistance : playback.MinimumDistance;
                instance.setProperty(FMOD.Studio.EVENT_PROPERTY.MINIMUM_DISTANCE, minimum);
                instance.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, maximum);
            }
            Position();
            result = instance.start();
            started = result == FMOD.RESULT.OK;
            if (!started)
            {
                owner.Warn(Key + "start", "FMOD could not start " + Key + " (" + result + ").");
                Stop();
            }
        }

        /// <summary>Updates native 3D placement or explicit radius attenuation for a 2D ambient event.</summary>
        private void Position()
        {
            // A 2D authored ambience can still obey the interaction's local listening radius.
            if (spatial)
            {
                if (!binding.Spatialize && radius <= 0f
                    && FMODUnity.RuntimeManager.StudioSystem.getListenerAttributes(0, out FMOD.ATTRIBUTES_3D anchor) == FMOD.RESULT.OK)
                    instance.set3DAttributes(anchor);
                else
                    instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(position));
            }
            else if (radius > 0f && FMODUnity.RuntimeManager.StudioSystem.getListenerAttributes(0, out FMOD.ATTRIBUTES_3D listener) == FMOD.RESULT.OK)
            {
                Vector3 offset = position - new Vector3(listener.position.x, listener.position.y, listener.position.z);
                instance.setVolume(binding.Volume * Mathf.Clamp01(1f - offset.magnitude / radius));
            }
        }

        /// <summary>Releases this instance once, including requests cancelled before bank readiness.</summary>
        public void Stop()
        {
            // Native ownership ends here; no attached emitter component is needed.
            if (instance.isValid())
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
                instance.clearHandle();
            }
            stopped = true;
        }

        #endregion
        #endregion
    }
}
