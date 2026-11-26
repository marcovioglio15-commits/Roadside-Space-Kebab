using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the optional voice used during dialogue presentation.</summary>
    public enum DialogueVoice { None, Hellfury, Mothman, Fishy }
    /// <summary>Selects timed phrases or speech triggered by newly revealed text.</summary>
    public enum DialogueAudioMode { Interval, Characters }
    /// <summary>Selects the optional cue after the final page is completed.</summary>
    public enum DialogueResultSound { None, OrderOK, OrderNO }

    /// <summary>Stores dialogue voice cadence and successful-completion audio.</summary>
    [Serializable]
    public sealed class DialogueAudioSettings
    {
        #region Fields

        [Header("Dialogue Audio")]
        [Tooltip("Optional character voice played while this dialogue is visible.")]
        public DialogueVoice Voice;
        [Tooltip("Interval repeats Voice while the box is visible. Characters uses text reveal without interrupting active Voice. Talk follows reveal and remaining Voice in Characters mode.")]
        public DialogueAudioMode Mode = DialogueAudioMode.Characters;
        [Tooltip("Seconds between voice phrases. A phrase never overlaps another dialogue sound.")]
        [Range(0.01f, 30f)]
        public float Interval = 3f;
        [Tooltip("Visible text characters per Voice trigger. Excludes spaces, tags and sprites. Busy triggers are skipped. Resets per page. Instant reveal stays silent.")]
        [Range(1, 120)]
        public int CharactersPerVoice = 1;
        [Tooltip("Optional sound after advancing beyond the final page; interruption does not play it.")]
        public DialogueResultSound Completion;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks only audio options currently enabled in the dialogue.</summary>
        /// <returns>True when enums and the selected voice cadence are supported.</returns>
        internal bool IsValid()
        {
            // Disabled voice settings retain their values for later reuse.
            return Voice is >= DialogueVoice.None and <= DialogueVoice.Fishy
                && Mode is DialogueAudioMode.Interval or DialogueAudioMode.Characters
                && Completion is >= DialogueResultSound.None and <= DialogueResultSound.OrderNO
                && (Voice == DialogueVoice.None || (Mode == DialogueAudioMode.Characters
                    ? CharactersPerVoice > 0 : InteractionValues.Positive(Interval)));
        }

        #endregion
        #endregion
    }
}
