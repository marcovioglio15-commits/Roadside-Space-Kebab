using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Fades an authored black overlay across asynchronous scene loading without creating UI.</summary>
    public sealed class MenuSceneTransition : MonoBehaviour
    {
        #region Fields

        private static MenuSceneTransition current;
        private AsyncOperation loading;
        private Action completed;
        private Action atBlack;
        private string destination;
        private float fadeOut;
        private float fadeIn;
        private float hold;
        private float elapsed;
        private float previousScale;
        private int phase;
        private bool disposeAfterFade;

        #endregion

        #region Properties

        /// <summary>Whether a scene fade currently owns gameplay and menu navigation.</summary>
        public static bool IsBusy => current != null;
        /// <summary>Existing fade panel on the active scene Canvas, replaced automatically across scene loads.</summary>
        public CanvasGroup Overlay => MenuTransitionOverlay.Current;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Clears retained ownership at a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // A new session never inherits an unfinished loading transaction.
            current = null;
        }

        /// <summary>Initializes an invisible overlay until a transition is explicitly requested.</summary>
        private void Awake()
        {
            // An idle component does no per-frame work.
            if (current == this)
                return;
            enabled = false;
            if (!IsBusy && Overlay != null)
            {
                Overlay.alpha = 0f;
                Overlay.blocksRaycasts = false;
            }
        }

        /// <summary>Releases input and pause state if its owner is disabled or destroyed.</summary>
        private void OnDisable()
        {
            // Scene callbacks cannot leave gameplay frozen when a flow is cancelled.
            if (current != this)
                return;
            current = null;
            Time.timeScale = previousScale;
            MenuGameplayGate.Release(this);
            if (Overlay != null)
            {
                Overlay.alpha = 0f;
                Overlay.blocksRaycasts = false;
            }
            completed = null;
            atBlack = null;
            if (disposeAfterFade)
                Destroy(gameObject);
        }

        #endregion
        #region Loading

        /// <summary>Preserves an authored standalone controller across the menu unload, then removes it after the reveal.</summary>
        /// <param name="scene">Build-registered gameplay scene.</param>
        /// <param name="profile">Flow asset supplying the same timing used between days.</param>
        /// <returns>True when the menu's fade transaction starts.</returns>
        public bool BeginPlay(string scene, MenuTransitionProfile profile)
        {
            // Only the controller persists; scene Canvases and their authored overlays unload normally.
            if (profile == null || transform.parent != null || transform.childCount != 0 || GetComponent<Canvas>() != null
                || !Begin(scene, profile.FadeOutSeconds, profile.BlackSeconds, profile.FadeInSeconds, null))
                return false;
            disposeAfterFade = true;
            DontDestroyOnLoad(gameObject);
            return true;
        }

        /// <summary>Starts a validated fade while preserving the previous gameplay time scale.</summary>
        /// <param name="scene">Build-registered destination scene path.</param>
        /// <param name="outSeconds">Time to reach full black.</param>
        /// <param name="blackSeconds">Minimum hold after scene activation.</param>
        /// <param name="inSeconds">Time to reveal the new scene.</param>
        /// <param name="onCompleted">Invoked after time and input ownership are released.</param>
        /// <returns>True when this overlay accepted the transition.</returns>
        public bool Begin(string scene, float outSeconds, float blackSeconds, float inSeconds, Action onCompleted)
        {
            return Application.CanStreamedLevelBeLoaded(scene) && StartFade(scene, outSeconds, blackSeconds, inSeconds, null, onCompleted);
        }

        /// <summary>Runs the same authored fade without unloading the current scene.</summary>
        /// <param name="outSeconds">Time to full black.</param>
        /// <param name="blackSeconds">Hold after the blackout action.</param>
        /// <param name="inSeconds">Reveal duration.</param>
        /// <param name="onBlack">Scene-local operation performed while hidden.</param>
        /// <param name="onCompleted">Continuation after input and time are restored.</param>
        /// <returns>True when the existing overlay accepts this transition.</returns>
        public bool BeginLocal(float outSeconds, float blackSeconds, float inSeconds, Action onBlack, Action onCompleted)
        {
            return onBlack != null && StartFade(null, outSeconds, blackSeconds, inSeconds, onBlack, onCompleted);
        }

        /// <summary>Captures common scene-load and scene-preserving fade state.</summary>
        /// <param name="scene">Destination path, or null for a local operation.</param>
        /// <param name="outSeconds">Fade-out duration.</param>
        /// <param name="blackSeconds">Black hold duration.</param>
        /// <param name="inSeconds">Fade-in duration.</param>
        /// <param name="onBlack">Optional local operation.</param>
        /// <param name="onCompleted">Completion callback.</param>
        /// <returns>True when this overlay acquired transition ownership.</returns>
        private bool StartFade(string scene, float outSeconds, float blackSeconds, float inSeconds, Action onBlack, Action onCompleted)
        {
            // Invalid destinations leave the current scene and time scale unchanged.
            if (IsBusy || Overlay == null
                || !Valid(outSeconds) || !Valid(blackSeconds) || !Valid(inSeconds))
                return false;
            current = this;
            destination = scene;
            fadeOut = outSeconds;
            fadeIn = inSeconds;
            hold = blackSeconds;
            completed = onCompleted;
            atBlack = onBlack;
            loading = null;
            elapsed = 0f;
            phase = 0;
            previousScale = Time.timeScale;
            Time.timeScale = 0f;
            MenuGameplayGate.Acquire(this);
            Overlay.alpha = 0f;
            Overlay.blocksRaycasts = true;
            enabled = true;
            return true;
        }

        /// <summary>Advances the fade using unscaled time and loads only while fully black.</summary>
        private void Update()
        {
            // Loading stays asynchronous; no synchronous scene or resource work blocks the fade.
            // A missing destination panel ends the transaction instead of dereferencing an unloaded Canvas.
            if (Overlay == null)
            {
                Debug.LogWarning("Add an authored Menu Transition Overlay to the destination scene Canvas.", this);
                enabled = false;
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            switch (phase)
            {
                case 0:
                    Overlay.alpha = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
                    if (Overlay.alpha < 1f)
                        return;
                    if (atBlack != null)
                    {
                        Action operation = atBlack;
                        atBlack = null;
                        phase = 2;
                        operation();
                    }
                    else
                    {
                        loading = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
                        phase = 1;
                    }
                    elapsed = 0f;
                    break;
                case 1:
                    if (!loading.isDone)
                        return;
                    phase = 2;
                    elapsed = 0f;
                    break;
                case 2:
                    if (elapsed < hold)
                        return;
                    phase = 3;
                    elapsed = 0f;
                    break;
                case 3:
                    Overlay.alpha = fadeIn > 0f ? 1f - Mathf.Clamp01(elapsed / fadeIn) : 0f;
                    if (Overlay.alpha > 0f)
                        return;
                    Action callback = completed;
                    enabled = false;
                    callback?.Invoke();
                    break;
            }
        }

        /// <summary>Rejects invalid fade durations without replacing their authored values.</summary>
        /// <param name="value">Requested duration.</param>
        /// <returns>True for finite nonnegative seconds.</returns>
        private static bool Valid(float value)
        {
            // Zero-duration phases are supported without division by zero.
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion
        #endregion
    }
}
