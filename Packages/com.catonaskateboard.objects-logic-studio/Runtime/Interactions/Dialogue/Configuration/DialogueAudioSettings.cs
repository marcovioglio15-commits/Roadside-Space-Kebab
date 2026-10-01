using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects the optional voice used between dialogue pages.</summary>
    public enum DialogueVoice { None, Hellfury, Mothman, Fishy }
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
        [Tooltip("Seconds between voice phrases. A phrase never overlaps another dialogue sound.")]
        public float Interval = 3f;
        [Tooltip("Optional sound after advancing beyond the final page; interruption does not play it.")]
        public DialogueResultSound Completion;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks only audio options currently enabled in the dialogue.</summary>
        /// <returns>True when enums and the enabled voice interval are supported.</returns>
        internal bool IsValid()
        {
            // Disabled voice settings retain their values for later reuse.
            return Voice is >= DialogueVoice.None and <= DialogueVoice.Fishy
                && Completion is >= DialogueResultSound.None and <= DialogueResultSound.OrderNO
                && (Voice == DialogueVoice.None || InteractionValues.Positive(Interval));
        }

        #endregion
        #endregion
    }
}
