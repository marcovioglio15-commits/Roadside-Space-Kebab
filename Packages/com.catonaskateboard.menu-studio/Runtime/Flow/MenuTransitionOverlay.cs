using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Registers an existing black panel so persistent flow controllers can use each scene's Canvas.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuTransitionOverlay : MonoBehaviour
    {
        #region State

        private static readonly List<MenuTransitionOverlay> registered = new List<MenuTransitionOverlay>();
        private CanvasGroup group;

        #endregion

        #region Properties

        /// <summary>Black panel belonging to the active scene, resolved without hierarchy searches.</summary>
        internal static CanvasGroup Current
        {
            get
            {
                Scene scene = SceneManager.GetActiveScene();
                foreach (MenuTransitionOverlay overlay in registered)
                    if (overlay != null && overlay.gameObject.scene == scene)
                        return overlay.group;
                return null;
            }
        }

        #endregion

        #region Methods

        #region Registration

        /// <summary>Rebuilds registration once when entering Play without scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            registered.Clear();
            foreach (MenuTransitionOverlay overlay in FindObjectsByType<MenuTransitionOverlay>())
                overlay.Register();
        }

        /// <summary>Registers the saved panel and inherits full black while a destination scene is loading.</summary>
        private void OnEnable()
        {
            Register();
        }

        /// <summary>Captures only the authored CanvasGroup; no UI is instantiated or reparented.</summary>
        private void Register()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = MenuSceneTransition.IsBusy ? 1f : 0f;
            group.blocksRaycasts = MenuSceneTransition.IsBusy;
            if (!registered.Contains(this))
                registered.Add(this);
        }

        /// <summary>Withdraws an unloading scene without affecting another scene's fade panel.</summary>
        private void OnDisable()
        {
            registered.Remove(this);
        }

        #endregion

        #endregion
    }
}
