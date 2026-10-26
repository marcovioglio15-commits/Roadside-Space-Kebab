using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one local radio timeline with optional control over background music availability.</summary>
    internal sealed class RadioAudioRun
    {
        #region State

        private IAudioVoice voice;
        private IStudioAudioBackend backend;
        private bool separateMusic;
        private int channel = -1;
        private float retry;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Maintains a local radio event and applies its selected background music policy.</summary>
        /// <param name="owner">Radio component retaining local music ownership.</param>
        /// <param name="settings">Validated channel configuration.</param>
        /// <param name="radius">Spatial listening distance in metres.</param>
        internal void Tick(ObjectAmbient owner, RadioSettings settings, float radius)
        {
            if (channel < 0)
                channel = settings.InitialChannel;
            RefreshMusicOwner(owner, settings.SeparateBackgroundMusic);
            if (settings.Channels[channel].Value <= 0f || Time.timeScale <= 0f
                || voice != null && voice.IsPlaying || Time.unscaledTime < retry)
                return;
            voice = StudioAudio.Play("mus_radio", owner.transform, settings.Parameter, settings.Channels[channel].Value, radius);
            retry = Time.unscaledTime + (voice == null ? 1f : 0f);
        }

        /// <summary>Selects the next configured channel without restarting a playing radio timeline.</summary>
        /// <param name="owner">Radio emitting the new channel.</param>
        /// <param name="settings">Validated channel cycle.</param>
        /// <param name="radius">Emitter attenuation distance.</param>
        internal void Cycle(ObjectAmbient owner, RadioSettings settings, float radius)
        {
            channel = ((channel < 0 ? settings.InitialChannel : channel) + 1) % settings.Channels.Length;
            if (settings.Channels[channel].Value <= 0f)
            {
                voice?.Stop();
                voice = null;
            }
            else if (voice != null && voice.IsPlaying && !voice.SetParameter(settings.Channels[channel].Value))
            {
                voice.Stop();
                voice = null;
            }
            retry = 0f;
            Tick(owner, settings, radius);
        }

        /// <summary>Stops this emitter and releases music ownership while optionally resetting the next session's channel.</summary>
        /// <param name="owner">Radio leaving gameplay availability.</param>
        /// <param name="reset">Whether the next activation must use the authored initial channel.</param>
        internal void Stop(ObjectAmbient owner, bool reset = false)
        {
            voice?.Stop();
            voice = null;
            retry = 0f;
            if (backend is IStudioMusicBackend music)
                music.SetLocalMusicOwner(owner, false);
            backend = null;
            if (reset)
                channel = -1;
        }

        /// <summary>Changes background ownership only when the backend or this radio's policy changes.</summary>
        /// <param name="owner">Radio retaining or releasing music ownership.</param>
        /// <param name="separate">Whether background music continues independently of this emitter.</param>
        private void RefreshMusicOwner(ObjectAmbient owner, bool separate)
        {
            // Moving between adapters cannot leave a voice or music claim on the previous backend.
            bool changed = !ReferenceEquals(backend, StudioAudio.Backend);
            if (!changed && separateMusic == separate)
                return;
            if (changed)
            {
                voice?.Stop();
                voice = null;
                retry = 0f;
            }
            if (backend is IStudioMusicBackend previous)
                previous.SetLocalMusicOwner(owner, false);
            backend = StudioAudio.Backend;
            separateMusic = separate;
            if (!separateMusic)
                StudioAudio.SetLocalMusicOwner(owner, true);
        }

        #endregion
        #endregion
    }
}
