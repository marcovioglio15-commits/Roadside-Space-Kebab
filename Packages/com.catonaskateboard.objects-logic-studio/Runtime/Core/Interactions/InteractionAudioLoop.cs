using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one process sound and repeats completed one-shots only while that process remains active.</summary>
    internal sealed class InteractionAudioLoop
    {
        #region State

        private IAudioVoice voice;
        private float retry;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Changes playback only at process and native voice boundaries; missing backends use a bounded retry.</summary>
        /// <param name="active">Whether the process is currently producing its audible effect.</param>
        /// <param name="key">Audio Studio key retained for this process.</param>
        /// <param name="source">Moving emitter supplying its final position if destroyed.</param>
        internal void Tick(bool active, string key, Transform source)
        {
            if (!active)
            {
                Stop();
                return;
            }
            if (Time.timeScale <= 0f || voice != null && voice.IsPlaying || Time.unscaledTime < retry)
                return;
            voice = StudioAudio.Play(key, source);
            retry = Time.unscaledTime + (voice == null ? 1f : 0f);
        }

        /// <summary>Releases this process's voice without affecting unrelated emitters.</summary>
        internal void Stop()
        {
            voice?.Stop();
            voice = null;
            retry = 0f;
        }

        #endregion
        #endregion
    }
}
