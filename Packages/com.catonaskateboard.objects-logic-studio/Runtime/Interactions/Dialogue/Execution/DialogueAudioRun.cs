using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Schedules timed or text-driven phrases without overlapping dialogue audio.</summary>
    internal sealed class DialogueAudioRun
    {
        #region State

        private static IAudioVoice cue;
        private IAudioVoice voice;
        private float nextVoice;
        private int printedCharacters;

        #endregion

        #region Properties

        /// <summary>Includes queued and paused speech until its audio backend releases it.</summary>
        internal bool IsPlaying => voice != null && voice.IsPlaying;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Starts a visible dialogue's audio sequence after HUD arbitration succeeds.</summary>
        /// <param name="settings">Voice and result options.</param>
        /// <param name="source">Speaking object.</param>
        internal void Begin(DialogueAudioSettings settings, Transform source)
        {
            // Arrival cues take precedence over the first voice phrase.
            Stop();
            ResetCharacters(0);
            nextVoice = Time.time;
            Tick(settings, source, 0);
        }

        /// <summary>Starts a page's cadence or skips letters exposed by an instant reveal.</summary>
        /// <param name="characters">Already visible characters that must not emit another voice.</param>
        internal void ResetCharacters(int characters)
        {
            // Retained and instantly completed text never replays its earlier letters.
            printedCharacters = characters;
        }

        /// <summary>Announces completed walk-in immediately while preventing dialogue voices from masking the cue.</summary>
        /// <param name="source">Arrived object providing the cue position.</param>
        internal static void AnnounceArrival(Transform source)
        {
            PlayCue("sfx_newcustomer", source);
        }

        /// <summary>Emits due speech from the same text snapshot used to select the Talk animation.</summary>
        /// <param name="settings">Selected voice and cadence.</param>
        /// <param name="source">Current speaker transform.</param>
        /// <param name="characters">Visible text characters revealed so far on this page.</param>
        internal void Tick(DialogueAudioSettings settings, Transform source, int characters)
        {
            // Consume reveal progress even when voices are disabled or a cue takes precedence.
            int previous = printedCharacters;
            printedCharacters = characters;
            // Result and arrival cues suspend voice emission across consecutive dialogues.
            if (settings == null || settings.Voice == DialogueVoice.None)
                return;
            if (cue != null && cue.IsPlaying)
            {
                Stop();
                nextVoice = Time.time + settings.Interval;
                return;
            }
            // Consume busy thresholds without cutting speech or queuing it after text reveal.
            if (IsPlaying)
                return;
            switch (settings.Mode)
            {
                case DialogueAudioMode.Interval:
                    if (Time.time < nextVoice)
                        return;
                    nextVoice = Time.time + settings.Interval;
                    break;
                case DialogueAudioMode.Characters:
                    if (characters <= previous || settings.CharactersPerVoice < 1
                        || characters / settings.CharactersPerVoice <= previous / settings.CharactersPerVoice)
                        return;
                    break;
                default:
                    return;
            }
            string key = settings.Voice switch
            {
                DialogueVoice.Hellfury => "vo_hellfury",
                DialogueVoice.Mothman => "vo_mothman",
                DialogueVoice.Fishy => "vo_fishy",
                _ => string.Empty
            };
            voice = StudioAudio.Play(key, source);
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
