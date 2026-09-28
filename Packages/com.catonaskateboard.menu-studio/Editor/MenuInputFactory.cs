using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Connects user-owned input actions and reuses an existing EventSystem safely.</summary>
    internal static class MenuInputFactory
    {
        #region Methods
        #region Creation
        /// <summary>Connects a generated menu to existing project actions without creating another input asset.</summary>
        /// <param name="host">New menu host whose preset may override individual actions.</param>
        internal static void Configure(MenuHost host)
        {
            // Project actions and their bindings remain entirely owned by the user.
            InputActionAsset asset = InputSystem.actions;
            Dictionary<string, InputActionReference> references = new Dictionary<string, InputActionReference>();
            foreach (string role in new[] { "UI/Move", "UI/Submit", "UI/Cancel", "UI/Point", "UI/Click", "UI/Scroll",
                "Menu/Pause", "Menu/PreviousTab", "Menu/NextTab" })
                references.Add(role.Substring(role.IndexOf('/') + 1), FindReference(asset, role));
            MenuNavigation settings = host.Preset.Navigation;
            MenuInput input = host.gameObject.AddComponent<MenuInput>();
            input.Host = host;
            input.Pause = settings.Pause != null ? settings.Pause : references["Pause"];
            input.Cancel = settings.Cancel != null ? settings.Cancel : references["Cancel"];
            input.PreviousTab = settings.PreviousTab != null ? settings.PreviousTab : references["PreviousTab"];
            input.NextTab = settings.NextTab != null ? settings.NextTab : references["NextTab"];
            input.CreditsClose = settings.CreditsClose;
            ConfigureEventSystem(host, asset, references);
            if (asset == null)
                Debug.LogWarning("Assign project-wide actions in Input System settings and configure the menu's input roles.", host);
        }

        /// <summary>Finds a persistent imported reference while leaving missing roles unassigned.</summary>
        /// <param name="asset">User-selected project action asset.</param>
        /// <param name="path">Optional conventional map and action name.</param>
        /// <returns>The saved reference, or null until the user configures that role.</returns>
        private static InputActionReference FindReference(InputActionAsset asset, string path)
        {
            // The factory never invents actions or binds devices on the user's behalf.
            InputAction action = asset != null ? asset.FindAction(path) : null;
            if (action == null)
                return null;
            foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)))
                if (candidate is InputActionReference reference && reference.action == action)
                    return reference;
            return null;
        }

        /// <summary>Creates a scene EventSystem only when no loaded active one already exists.</summary>
        /// <param name="host">Generated menu and destination scene.</param>
        /// <param name="asset">User-selected project action asset.</param>
        /// <param name="references">Existing action references.</param>
        private static void ConfigureEventSystem(MenuHost host, InputActionAsset asset, Dictionary<string, InputActionReference> references)
        {
            // Existing project navigation is intentionally preserved and reported by the creation window.
            foreach (GameObject sceneRoot in host.gameObject.scene.GetRootGameObjects())
                if (sceneRoot.GetComponentInChildren<EventSystem>(true) != null)
                    return;
            GameObject root = new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(root, host.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Create menu EventSystem");
            EventSystem system = root.GetComponent<EventSystem>();
            system.sendNavigationEvents = host.Preset.Navigation.Enabled;
            InputSystemUIInputModule module = root.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = asset;
            module.point = references["Point"];
            module.leftClick = references["Click"];
            module.scrollWheel = references["Scroll"];
            module.move = host.Preset.Navigation.Move != null ? host.Preset.Navigation.Move : references["Move"];
            module.submit = host.Preset.Navigation.Submit != null ? host.Preset.Navigation.Submit : references["Submit"];
            module.cancel = host.Preset.Navigation.Cancel != null ? host.Preset.Navigation.Cancel : references["Cancel"];
            module.moveRepeatDelay = host.Preset.Navigation.RepeatDelay;
            module.moveRepeatRate = host.Preset.Navigation.RepeatInterval;
        }
        #endregion
        #endregion
    }
}
