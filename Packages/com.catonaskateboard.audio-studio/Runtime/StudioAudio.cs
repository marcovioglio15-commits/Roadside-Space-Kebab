using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Controls a single backend-owned sound without exposing native audio handles.</summary>
    public interface IAudioVoice
    {
        /// <summary>Includes queued, paused and currently audible playback.</summary>
        bool IsPlaying { get; }
        /// <summary>Updates the local numeric parameter selected when this voice was created.</summary>
        /// <param name="value">Finite parameter value retained even while event banks are still loading.</param>
        /// <returns>True when the voice accepts the requested value.</returns>
        bool SetParameter(float value);
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
        /// <param name="value">Optional numeric value used instead of a parameter label.</param>
        /// <returns>A controllable voice, or null when playback is unavailable.</returns>
        IAudioVoice Play(string key, Transform source, Vector3 position, string parameter, string label, float radius, float? value = null);
    }

    /// <summary>Optionally exposes scene music control without requiring it from effects-only backends.</summary>
    public interface IStudioMusicBackend
    {
        /// <summary>Restarts the configured scene music from its beginning without interrupting unrelated effects.</summary>
        void RestartMusic();
        /// <summary>Suspends scene-wide music while an authored local radio owns music, including its OFF channel.</summary>
        /// <param name="owner">Radio component retaining local music ownership.</param>
        /// <param name="active">Whether this radio is available in the scene.</param>
        void SetLocalMusicOwner(Object owner, bool active);
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

        /// <summary>Resets scene music at a gameplay boundary, including days that preserve the loaded scene.</summary>
        public static void RestartMusic()
        {
            // Effects-only adapters remain valid without implementing music control.
            if (Backend is IStudioMusicBackend music)
                music.RestartMusic();
        }

        /// <summary>Reserves music for a local radio without changing global mixer settings.</summary>
        /// <param name="owner">Available radio component.</param>
        /// <param name="active">Whether the radio retains ownership, even while tuned to OFF.</param>
        public static void SetLocalMusicOwner(Object owner, bool active)
        {
            // Effects-only backends do not need to participate in scene music ownership.
            if (Backend is IStudioMusicBackend music)
                music.SetLocalMusicOwner(owner, active);
        }

        /// <summary>Plays a cue at a captured position after its source's lifetime has ended.</summary>
        /// <param name="key">Audio Preset event key.</param>
        /// <param name="position">Final world position of the source.</param>
        /// <returns>The backend-owned voice, or null when audio is unavailable.</returns>
        public static IAudioVoice PlayAt(string key, Vector3 position)
        {
            // Captured positions remain usable after the emitter has been consumed or destroyed.
            return Backend?.Play(key, null, position, null, null, 0f);
        }

        /// <summary>Starts a numeric local parameter before the event begins, retaining its value during bank loading.</summary>
        /// <param name="key">Audio Preset event key.</param>
        /// <param name="source">Moving emitter.</param>
        /// <param name="parameter">Exact local parameter name.</param>
        /// <param name="value">Initial parameter value.</param>
        /// <param name="radius">Optional attenuation radius in metres.</param>
        /// <returns>The backend-owned parameterized voice, or null when unavailable.</returns>
        public static IAudioVoice Play(string key, Transform source, string parameter, float value, float radius = 0f)
        {
            // Pass the value in the original request so queued startup never briefly plays the preset's other channel.
            return Backend?.Play(key, source, source != null ? source.position : Vector3.zero, parameter, null, radius, value);
        }

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
