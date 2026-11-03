using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Sets entry-wide reveal timing and the strength of optional text tokens.</summary>
    [Serializable]
    public sealed class DialogueTextSettings
    {
        #region Fields

        [Header("Text Reveal")]
        [Tooltip("Reveal letters progressively. Advance first reveals the full page, then advances it.")]
        public bool Typewriter;
        [Tooltip("Letters revealed per scaled second. Speed tokens override this inside their span.")]
        [Range(1f, 120f)]
        public float LettersPerSecond = 35f;
        [Tooltip("Extra scaled seconds after punctuation while revealing letters.")]
        [Range(0f, 1f)]
        public float PunctuationPause = 0.12f;
        [Header("Letter Arrival")]
        [Tooltip("Make every letter land from an enlarged pose. The slam token can enable this for selected words instead.")]
        public bool Slam;
        [Tooltip("Initial letter size relative to its final size.")]
        [Range(1f, 5f)]
        public float SlamScale = 2.5f;
        [Tooltip("Initial height above the final baseline, relative to the letter height.")]
        [Range(0f, 3f)]
        public float SlamHeight = 0.75f;
        [Tooltip("Scaled seconds spent landing each letter.")]
        [Range(0.01f, 1f)]
        public float SlamDuration = 0.15f;
        [Header("Word Effects")]
        [Tooltip("Shake displacement relative to letter height. Used by shake tokens.")]
        [Range(0f, 0.5f)]
        public float ShakeAmount = 0.07f;
        [Tooltip("Shake changes per scaled second.")]
        [Range(1f, 60f)]
        public float ShakeRate = 20f;
        [Tooltip("Wave displacement relative to letter height. Used by wave tokens.")]
        [Range(0f, 0.5f)]
        public float WaveAmount = 0.1f;
        [Tooltip("Wave cycles per scaled second.")]
        [Range(0.1f, 12f)]
        public float WaveRate = 2f;
        [Tooltip("Rainbow cycles per scaled second. Used by rainbow tokens.")]
        [Range(0.1f, 5f)]
        public float RainbowRate = 0.4f;
        [Tooltip("Degrees used by rotate tokens without an explicit angle. Positive angles turn counterclockwise.")]
        [Range(-180f, 180f)]
        public float RotationDegrees = 15f;
        [Header("Text Colors")]
        [Tooltip("Named colors shared by this entry's pages. Color tokens use these names.")]
        public DialogueNamedColor[] Colors = Array.Empty<DialogueNamedColor>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks timing and effect domains without changing an unfinished proposal.</summary>
        /// <param name="warning">Receives the invalid text setting.</param>
        /// <returns>True when reveal and effect strengths are finite and supported.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Text effects need finite timing, nonnegative strengths and a slam scale of at least one.";
            if (!InteractionValues.Positive(LettersPerSecond) || !float.IsFinite(PunctuationPause) || PunctuationPause < 0f
                || !float.IsFinite(SlamScale) || SlamScale < 1f || !float.IsFinite(SlamHeight) || SlamHeight < 0f
                || !InteractionValues.Positive(SlamDuration) || !float.IsFinite(ShakeAmount) || ShakeAmount < 0f
                || !InteractionValues.Positive(ShakeRate) || !float.IsFinite(WaveAmount) || WaveAmount < 0f
                || !InteractionValues.Positive(WaveRate) || !InteractionValues.Positive(RainbowRate))
                return false;
            if (!float.IsFinite(RotationDegrees) || RotationDegrees < -180f || RotationDegrees > 180f)
            {
                warning = "Rotation Degrees must be finite and between -180 and 180.";
                return false;
            }
            return DialogueNamedColors.TryValidate(Colors, out warning);
        }

        #endregion

        #endregion
    }

    /// <summary>Sets box transitions once for every entry in this dialogue interaction.</summary>
    [Serializable]
    public sealed class DialogueBoxSettings
    {
        #region Fields

        [Header("Dialogue Box")]
        [Tooltip("Scaled seconds for showing the text box. Zero shows it immediately.")]
        [Range(0f, 2f)]
        public float FadeIn;
        [Tooltip("Scaled seconds for hiding the text box. Zero hides it immediately.")]
        [Range(0f, 2f)]
        public float FadeOut;
        [Tooltip("Horizontal speaker position above the box. Zero aligns left, one aligns right, and 0.5 centers it.")]
        [Range(0f, 1f)]
        public float SpeakerPosition = 0.5f;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects invalid transition times while keeping the saved values editable.</summary>
        /// <param name="warning">Receives an invalid transition time.</param>
        /// <returns>True for finite nonnegative durations.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = "Dialogue box fade times must be finite and nonnegative.";
            if (!float.IsFinite(FadeIn) || FadeIn < 0f || !float.IsFinite(FadeOut) || FadeOut < 0f)
                return false;
            if (!float.IsFinite(SpeakerPosition) || SpeakerPosition < 0f || SpeakerPosition > 1f)
            {
                warning = "Speaker Position must be between zero and one.";
                return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
