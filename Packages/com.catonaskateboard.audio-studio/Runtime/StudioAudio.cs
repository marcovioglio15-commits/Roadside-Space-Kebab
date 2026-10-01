using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Controls a single backend-owned sound without exposing native audio handles.</summary>
    public interface IAudioVoice
    {
        /// <summary>Includes queued, paused and currently audible playback.</summary>
        bool IsPlaying { get; }
        /// <summary>Stops this voice and releases its backend resources.</summary>
        void Stop();
    }

    /// <summary>Connects gameplay packages to a project-owned audio implementation.</summary>
    public interface IStudioAudioBackend
    {
        /// <summary>Creates a voice from an Audio Studio key and optional labeled parameter.</summary>
        /// <param name="key">Key in the project's Audio Preset.</param>
        /// <param name="source">Optional moving emitter.</param>
        /// <param name="position">Position retained if the emitter is destroyed.</param>
        /// <param name="parameter">Optional local parameter name.</param>
        /// <param name="label">Authored parameter label.</param>
        /// <param name="radius">Positive attenuation override, or zero for preset distances.</param>
        /// <returns>A controllable voice, or null when playback is unavailable.</returns>
        IAudioVoice Play(string key, Transform source, Vector3 position, string parameter, string label, float radius);
    }

    /// <summary>Provides optional gameplay audio while keeping packages independent of a particular SDK.</summary>
    public static class StudioAudio
    {
        #region Properties

        /// <summary>Project adapter installed by an authored scene component.</summary>
        public static IStudioAudioBackend Backend { get; set; }

        #endregion

        #region Methods
        #region Playback

        /// <summary>Plays an authored key at an emitter without requiring a runtime audio component on each item.</summary>
        /// <param name="key">Audio Preset event key.</param>
        /// <param name="source">Emitter followed until destroyed.</param>
        /// <param name="parameter">Optional local parameter name.</param>
        /// <param name="label">Parameter label from the sound project.</param>
        /// <param name="radius">Optional attenuation radius in metres.</param>
        /// <returns>The backend-owned voice, or null when no backend is installed.</returns>
        public static IAudioVoice Play(string key, Transform source, string parameter = null, string label = null, float radius = 0f)
        {
            // Missing adapters intentionally leave reusable gameplay packages silent.
            return Backend?.Play(key, source, source != null ? source.position : Vector3.zero, parameter, label, radius);
        }

        /// <summary>Clears stale adapters before Play when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Scene adapters register again during their initialization.
            Backend = null;
        }

        #endregion
        #endregion
    }
}
