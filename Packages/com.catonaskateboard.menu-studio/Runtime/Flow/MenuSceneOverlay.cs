using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Loads a saved pause-menu scene additively without duplicating requests or creating runtime UI.</summary>
    public sealed class MenuSceneOverlay : MonoBehaviour
    {
        #region Fields

        [Header("Pause Scene")]
        [Tooltip("Preset whose Pause Scene should accompany this gameplay scene.")]
        public MenuPreset Preset;
        private static readonly HashSet<string> pending = new HashSet<string>();

        #endregion

        #region Methods
        #region Loading

        /// <summary>Clears unfinished requests when entering Play without domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Asynchronous requests from a previous Play session no longer own a scene.
            pending.Clear();
        }

        /// <summary>Requests the configured pause scene after gameplay activates.</summary>
        private void Start()
        {
            // Pause UI comes entirely from the saved additive scene.
            if (Preset != null)
                Ensure(Preset.PauseScene);
        }

        /// <summary>Loads an optional overlay only once and leaves the gameplay scene active.</summary>
        /// <param name="path">Build-registered pause scene path.</param>
        public static void Ensure(string path)
        {
            // Concurrent day-flow and gameplay requests share one loading operation.
            if (string.IsNullOrWhiteSpace(path) || SceneManager.GetSceneByPath(path).isLoaded || pending.Contains(path))
                return;
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogWarning("Pause scene is missing from Build Settings: " + path);
                return;
            }
            pending.Add(path);
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive).completed += operation => pending.Remove(path);
        }

        #endregion
        #endregion
    }
}
