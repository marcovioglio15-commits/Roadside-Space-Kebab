using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Schedules character phrases while reserving dialogue effects against voice overlap.</summary>
    internal sealed class DialogueAudioRun
    {
        #region State

        private static IAudioVoice cue;
        private IAudioVoice voice;
        private float nextVoice;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Starts a visible dialogue's audio sequence after HUD arbitration succeeds.</summary>
        /// <param name="settings">Voice and result options.</param>
        /// <param name="source">Speaking object.</param>
        /// <param name="arrival">Whether this opening announces a new customer.</param>
        internal void Begin(DialogueAudioSettings settings, Transform source, bool arrival)
        {
            // Arrival cues take precedence over the first voice phrase.
            Stop();
            if (arrival)
                PlayCue("sfx_newcustomer", source);
            nextVoice = Time.time;
            Tick(settings, source);
        }

        /// <summary>Plays due phrases while no dialogue cue occupies the shared audio channel.</summary>
        /// <param name="settings">Selected voice and cadence.</param>
        /// <param name="source">Current speaker transform.</param>
        internal void Tick(DialogueAudioSettings settings, Transform source)
        {
            // Result and arrival cues suspend voice emission across consecutive dialogues.
            if (settings == null || settings.Voice == DialogueVoice.None)
                return;
            if (cue != null && cue.IsPlaying)
            {
                Stop();
                nextVoice = Time.time + settings.Interval;
                return;
            }
            if (Time.time < nextVoice || voice != null && voice.IsPlaying)
                return;
            string key = settings.Voice switch
            {
                DialogueVoice.Hellfury => "vo_hellfury",
                DialogueVoice.Mothman => "vo_mothman",
                DialogueVoice.Fishy => "vo_fishy",
                _ => string.Empty
            };
            voice = StudioAudio.Play(key, source);
            nextVoice = Time.time + settings.Interval;
        }

        /// <summary>Stops speech and plays the optional successful-completion cue.</summary>
        /// <param name="settings">Dialogue result choice.</param>
        /// <param name="source">Completing speaker.</param>
        internal void Finish(DialogueAudioSettings settings, Transform source)
        {
            // Only the final page reaches this boundary; interruptions merely stop the voice.
            Stop();
            if (settings == null || settings.Completion == DialogueResultSound.None)
                return;
            PlayCue(settings.Completion == DialogueResultSound.OrderOK ? "sfx_orderOK" : "sfx_orderNO", source);
        }

        /// <summary>Stops only this dialogue's voice while leaving already emitted result effects alive.</summary>
        internal void Stop()
        {
            // A consumed speaker may disappear before its completion sound finishes.
            voice?.Stop();
            voice = null;
        }

        /// <summary>Replaces the preceding cue before opening a new dialogue audio boundary.</summary>
        /// <param name="key">Arrival or result audio key.</param>
        /// <param name="source">Speaking object.</param>
        private static void PlayCue(string key, Transform source)
        {
            // One cue owns the channel, so voices can test its actual native lifetime.
            cue?.Stop();
            cue = StudioAudio.Play(key, source);
        }

        /// <summary>Drops obsolete handles before entering Play without domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // The scene adapter releases native resources independently.
            cue = null;
        }

        #endregion
        #endregion
    }
}
