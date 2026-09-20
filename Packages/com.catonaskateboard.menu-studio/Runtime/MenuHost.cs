using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Controls existing main or pause panels without creating UI or assuming a gameplay architecture.</summary>
    public sealed class MenuHost : MonoBehaviour
    {
        #region Fields
        [Header("Configuration")]
        [Tooltip("Reusable menu preset used during generation and runtime interaction.")]
        public MenuPreset Preset;
        [Tooltip("Main menu or gameplay pause menu.")]
        public MenuKind Kind;
        [Tooltip("Preauthored page roots and initial focus targets.")]
        public MenuPage[] Pages = Array.Empty<MenuPage>();
        [Tooltip("Preauthored settings controller, when included.")]
        public MenuSettingsController Settings;
        [Header("Project Callbacks")]
        [Tooltip("Raised when a portable menu command is requested.")]
        public UnityEvent<MenuCommand> CommandRequested = new UnityEvent<MenuCommand>();
        [Tooltip("Raised when menu visibility changes; use it to gate project input.")]
        public UnityEvent<bool> VisibilityChanged = new UnityEvent<bool>();
        [Tooltip("Optional audio cue keys. Connect a project-specific audio adapter here.")]
        public UnityEvent<string> AudioCue = new UnityEvent<string>();
        private MenuPageKind current;
        private GameObject previousSelection;
        private GameObject homeSelection;
        private bool visible;
        private bool transitioning;
        private int lastNavigationFrame = -1;
        #endregion

        #region Properties
        public bool Visible => visible;
        public MenuPageKind CurrentPage => current;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Initializes visibility after all generated controls have awakened.</summary>
        private void Start()
        {
            // A pause host stays active to receive input while its page roots remain hidden.
            if (Preset == null)
            {
                Debug.LogWarning("Menu Host needs a preset.", this);
                enabled = false;
                return;
            }
            Settings?.Initialize();
            SetVisible(Kind == MenuKind.Main);
        }

        /// <summary>Releases time and cursor ownership when the host leaves the scene.</summary>
        private void OnDisable()
        {
            // Lifecycle cleanup is independent of how the menu was closed.
            if (visible)
                SetVisible(false);
            MenuSession.Release(this);
        }
        #endregion

        #region Commands
        /// <summary>Executes a portable menu command against existing panels and scene paths.</summary>
        /// <param name="command">Action selected by a generated button or project code.</param>
        public void Execute(MenuCommand command)
        {
            // Ignore duplicate scene requests while an asynchronous transition is underway.
            if (transitioning || Preset == null)
                return;
            CommandRequested.Invoke(command);
            switch (command)
            {
                case MenuCommand.Play: LoadScene(Preset.GameplayScene); break;
                case MenuCommand.Resume: SetVisible(false); break;
                case MenuCommand.Restart: LoadScene(gameObject.scene.path); break;
                case MenuCommand.MainMenu: LoadScene(Preset.MainMenuScene); break;
                case MenuCommand.Settings:
                    if (Settings != null)
                    {
                        ShowPage(MenuPageKind.Settings);
                        Settings.BeginEdit();
                    }
                    break;
                case MenuCommand.Credits: ShowPage(MenuPageKind.Credits); break;
                case MenuCommand.Back: Back(); break;
                case MenuCommand.Quit:
                    if (Preset.ConfirmQuit)
                        ShowPage(MenuPageKind.Confirmation);
                    else
                        Application.Quit();
                    break;
                case MenuCommand.ConfirmQuit: Application.Quit(); break;
            }
        }

        /// <summary>Handles the pause action once per input frame.</summary>
        public void TogglePause()
        {
            // Escape can be shared by Pause and Cancel without opening and closing in one frame.
            if (Kind != MenuKind.Pause || lastNavigationFrame == Time.frameCount)
                return;
            lastNavigationFrame = Time.frameCount;
            if (visible && current != MenuPageKind.Home)
                ReturnHome();
            else
                SetVisible(!visible);
        }

        /// <summary>Closes the top overlay or resumes gameplay from the pause home page.</summary>
        public void Back()
        {
            // Main menus remain visible when Cancel is pressed on their home page.
            if (!visible || lastNavigationFrame == Time.frameCount)
                return;
            lastNavigationFrame = Time.frameCount;
            if (current != MenuPageKind.Home)
                ReturnHome();
            else if (Kind == MenuKind.Pause)
                SetVisible(false);
        }

        /// <summary>Discards settings proposals and restores home-button focus.</summary>
        public void ReturnHome()
        {
            // Applying settings clears its edit session before returning through this path.
            if (current == MenuPageKind.Settings)
                Settings?.Discard();
            ShowPage(MenuPageKind.Home);
            if (homeSelection != null && homeSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(homeSelection);
        }
        #endregion

        #region Pages
        /// <summary>Opens or closes the host and coordinates focus, cursor and pause state.</summary>
        /// <param name="requested">Desired visibility.</param>
        public void SetVisible(bool requested)
        {
            // Capture external focus before opening any owned panel.
            if (requested && !visible)
            {
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                MenuSession.Acquire(this);
            }
            visible = requested;
            if (requested)
                ShowPage(MenuPageKind.Home);
            else
            {
                Settings?.Discard();
                foreach (MenuPage page in Pages)
                    if (page.Root != null)
                        page.Root.SetActive(false);
                MenuSession.Release(this);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            }
            VisibilityChanged.Invoke(requested);
        }

        /// <summary>Activates one generated page and restores its initial selectable.</summary>
        /// <param name="pageKind">Target page kind.</param>
        private void ShowPage(MenuPageKind pageKind)
        {
            // Missing optional pages leave the current page untouched.
            MenuPage target = Array.Find(Pages, page => page.Kind == pageKind && page.Root != null);
            if (target == null)
                return;
            if (current == MenuPageKind.Home && pageKind != MenuPageKind.Home && EventSystem.current != null)
                homeSelection = EventSystem.current.currentSelectedGameObject;
            current = pageKind;
            foreach (MenuPage page in Pages)
                if (page.Root != null)
                    page.Root.SetActive(page == target);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.FirstSelection != null ? target.FirstSelection.gameObject : null);
        }

        /// <summary>Loads a configured scene only when it exists in the build scene list.</summary>
        /// <param name="path">Full scene asset path authored by the project.</param>
        private void LoadScene(string path)
        {
            // Validation failures keep the current menu and pause ownership intact.
            if (string.IsNullOrWhiteSpace(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogWarning("Menu scene is missing from Build Settings: " + path, this);
                return;
            }
            transitioning = true;
            SetVisible(false);
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }
        #endregion
        #endregion
    }

    /// <summary>Links one preauthored panel to its navigation entry point.</summary>
    [Serializable]
    public sealed class MenuPage
    {
        #region Fields
        [Tooltip("Purpose of this panel.")]
        public MenuPageKind Kind;
        [Tooltip("Existing panel root toggled by the host.")]
        public GameObject Root;
        [Tooltip("Control receiving focus when this page opens.")]
        public Selectable FirstSelection;
        #endregion
    }
}
