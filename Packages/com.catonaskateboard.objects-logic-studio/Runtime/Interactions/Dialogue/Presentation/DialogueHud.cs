using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents dialogue pages through one existing panel on the scene Canvas.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Dialogue HUD")]
    public sealed class DialogueHud : ObserverHudPanel
    {
        #region Serialized Fields

        [Header("Dialogue Graphics")]
        [Tooltip("Existing text graphic receiving the current dialogue page.")]
        [SerializeField]
        private TMP_Text body;
        [Tooltip("Optional existing text graphic receiving the speaker name.")]
        [SerializeField]
        private Text speaker;
        [Tooltip("Saved Canvas Group on the text box. Controls its shared fade without creating UI during Play.")]
        [SerializeField]
        private CanvasGroup box;

        #endregion

        #region State

        private readonly DialogueTextRun textRun = new DialogueTextRun();
        private float fadeOut;
        private float fadeFrom;
        private float fadeTo;
        private float fadeStarted;
        private float fadeDuration;
        private bool fading;
        private bool visible;
        private float speakerPosition = 0.5f;

        #endregion

        #region Properties

        /// <summary>Current reveal time captured by the dialogue's interruption policy.</summary>
        internal float PageTime => textRun.Elapsed;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Checks existing text references before the scene HUD claims this panel.</summary>
        /// <returns>True when the page and optional speaker belong to this panel.</returns>
        public override bool IsValid()
        {
            return base.IsValid() && body != null && body.font != null && body.transform.IsChildOf(Panel)
                && box != null && box.transform == Panel && (speaker == null || speaker.transform.IsChildOf(Panel));
        }

        /// <summary>Updates one saved page without altering sibling panels or input ownership.</summary>
        /// <param name="line">Current dialogue page and speaker.</param>
        /// <param name="effects">Entry-wide letter and word settings.</param>
        /// <param name="transition">Box transition settings shared by this dialogue.</param>
        /// <param name="elapsed">Retained reveal time when resuming a page.</param>
        internal void Show(DialogueLine line, DialogueTextSettings effects, DialogueBoxSettings transition, float elapsed)
        {
            // Open saved graphics before asking TMP for the full, stable page geometry.
            bool opening = !Panel.gameObject.activeSelf;
            ShowPanel();
            if (opening)
                box.alpha = 0f;
            visible = true;
            fadeOut = transition.FadeOut;
            Fade(1f, transition.FadeIn);
            textRun.Begin(body, line, effects, elapsed);
            if (speaker != null)
            {
                speaker.text = line.Speaker;
                speaker.enabled = !string.IsNullOrEmpty(line.Speaker);
                speakerPosition = transition.SpeakerPosition;
                if (speaker.enabled)
                    DialogueSpeakerLayout.Apply(speaker, Panel, speakerPosition);
            }
        }

        /// <summary>Keeps the saved speaker aligned when the existing HUD changes size.</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (visible && speaker != null && speaker.enabled && Panel != null)
                DialogueSpeakerLayout.Apply(speaker, Panel, speakerPosition);
        }

        /// <summary>Uses an advance press to reveal pending letters before the owner changes pages.</summary>
        /// <returns>True when this press completed the current text reveal.</returns>
        internal bool RevealPage()
        {
            return textRun.RevealAll();
        }

        /// <summary>Fades the existing box out while freezing the page at its retained reveal time.</summary>
        public override void Hide()
        {
            textRun.Pause();
            if (visible && isActiveAndEnabled && Application.isPlaying && box != null)
            {
                visible = false;
                Fade(0f, fadeOut);
                return;
            }
            if (isActiveAndEnabled && fading && fadeTo <= 0f)
                return;
            visible = fading = false;
            if (box != null)
                box.alpha = 0f;
            textRun.Stop();
            base.Hide();
        }

        /// <summary>Updates only active letter effects and an unfinished box transition.</summary>
        private void Update()
        {
            if (visible)
                textRun.Tick();
            if (!fading || Time.deltaTime <= 0f)
                return;
            float progress = Mathf.Clamp01((Time.time - fadeStarted) / fadeDuration);
            box.alpha = Mathf.Lerp(fadeFrom, fadeTo, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
                EndFade();
        }

        /// <summary>Retargets an interrupted transition from the current box alpha.</summary>
        /// <param name="alpha">Final opacity.</param>
        /// <param name="duration">Scaled seconds, or zero for immediate presentation.</param>
        private void Fade(float alpha, float duration)
        {
            fadeFrom = box.alpha;
            fadeTo = alpha;
            fadeDuration = duration;
            fadeStarted = Time.time;
            fading = duration > 0f && !Mathf.Approximately(fadeFrom, alpha);
            if (!fading)
                EndFade();
        }

        /// <summary>Closes graphics only after their requested fade has finished.</summary>
        private void EndFade()
        {
            fading = false;
            box.alpha = fadeTo;
            if (fadeTo > 0f)
                return;
            textRun.Stop();
            base.Hide();
        }

        #endregion

        #endregion
    }
}
