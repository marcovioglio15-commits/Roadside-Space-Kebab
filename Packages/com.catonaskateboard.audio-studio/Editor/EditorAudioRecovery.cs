using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Retries Unity's native editor output once after an explicit device-initialization failure.</summary>
    [InitializeOnLoad]
    internal static class EditorAudioRecovery
    {
        #region State

        private const string attemptedKey = "CatOnASkateboard.AudioStudio.OutputRecoveryAttempted";
        private static bool pending;
        private static bool resetting;
        private static double due;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Observes only explicit output failures and editor lifecycle boundaries.</summary>
        static EditorAudioRecovery()
        {
            // No audio reset occurs on load, repaint, normal Play entry or ordinary gameplay frames.
            Application.logMessageReceived += Observe;
            AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        }

        /// <summary>Recognizes Unity's output-initialization error without matching unrelated FMOD diagnostics.</summary>
        /// <param name="message">Native diagnostic received from the editor console.</param>
        /// <returns>True only for output error 60 and its explicit initialization failure.</returns>
        internal static bool IsOutputFailure(string message)
        {
            // Unity's built-in audio backend uses FMOD even without the optional FMOD Studio integration.
            return !string.IsNullOrEmpty(message)
                && (message.StartsWith("FMOD failed to switch back to normal output", StringComparison.Ordinal)
                    || message.StartsWith("FMOD failed to initialize the output device", StringComparison.Ordinal))
                && message.Contains("Error initializing output device", StringComparison.OrdinalIgnoreCase)
                && message.Contains("(60)", StringComparison.Ordinal);
        }

        /// <summary>Defers one retry until Unity finishes reporting and leaving any failed Play transition.</summary>
        /// <param name="message">Console message.</param>
        /// <param name="trace">Original stack trace retained by Unity.</param>
        /// <param name="type">Severity reported by the native audio backend.</param>
        private static void Observe(string message, string trace, LogType type)
        {
            // Never recurse into AudioSettings from its own log callback or reset live gameplay audio.
            if (Application.isBatchMode || EditorApplication.isPlaying || resetting || pending
                || type is not (LogType.Error or LogType.Warning) || !IsOutputFailure(message)
                || SessionState.GetBool(attemptedKey, false))
                return;
            pending = true;
            due = EditorApplication.timeSinceStartup + 0.5d;
            EditorApplication.update += RetryPending;
        }

        /// <summary>Allows a new attempt after an actual operating-system device change.</summary>
        /// <param name="deviceChanged">True for an external device change; false for AudioSettings.Reset.</param>
        private static void ConfigurationChanged(bool deviceChanged)
        {
            // A reset callback cannot rearm another automatic reset loop.
            if (deviceChanged && !resetting)
                SessionState.SetBool(attemptedKey, false);
        }

        /// <summary>Cancels deferred recovery when Play succeeds and rearms the next independent editor episode.</summary>
        /// <param name="state">Current editor Play Mode transition.</param>
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            // A successful transition needs no recovery and must not lose its audio playback state.
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;
            Cancel();
            SessionState.SetBool(attemptedKey, false);
        }

        /// <summary>Releases the temporary update subscription when recovery is no longer pending.</summary>
        private static void Cancel()
        {
            // Idle editor frames have no recovery callback.
            pending = false;
            EditorApplication.update -= RetryPending;
        }

        #endregion

        #region Recovery

        /// <summary>Waits for a safe editor boundary before retrying, with a bounded transition timeout.</summary>
        private static void RetryPending()
        {
            // A failed Play transition may need several editor updates before audio can be reset safely.
            if (EditorApplication.timeSinceStartup < due)
                return;
            if (!CanRetry())
            {
                if (EditorApplication.timeSinceStartup > due + 10d)
                    Cancel();
                return;
            }
            Cancel();
            Retry();
        }

        /// <summary>Reinitializes the existing editor audio configuration without changing project settings or forcing Play.</summary>
        //[MenuItem("Tools/Audio Studio/Retry Editor Audio Output")]
        private static void Retry()
        {
            // One attempt per failure episode prevents output-error/reset feedback loops.
            if (!CanRetry())
                return;
            Cancel();
            SessionState.SetBool(attemptedKey, true);
            resetting = true;
            try
            {
                if (AudioSettings.Reset(AudioSettings.GetConfiguration()))
                    Debug.Log("Editor audio output reset accepted. Retry Play. If error 60 returns, check the Windows output device and restart the Editor after restoring it.");
                else
                    Debug.LogWarning("Editor audio output could not be initialized. Check the active Windows output device, then use Tools > Audio Studio > Retry Editor Audio Output or restart the Editor.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Editor audio recovery failed: " + exception.Message);
            }
            finally
            {
                resetting = false;
            }
        }

        /// <summary>Restricts output recovery to idle Edit mode, outside asset loading and compilation.</summary>
        /// <returns>True when resetting cannot interrupt gameplay or an editor load operation.</returns>
        [MenuItem("Tools/Audio Studio/Retry Editor Audio Output", true)]
        private static bool CanRetry()
        {
            // AudioSettings.Reset can stall asset loading and discards playback state, so avoid those boundaries.
            return !resetting && !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        #endregion

        #endregion
    }
}
