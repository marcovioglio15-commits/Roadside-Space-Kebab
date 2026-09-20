using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Builds a project-owned action asset and reuses an existing EventSystem safely.</summary>
    internal static class MenuInputFactory
    {
        #region Methods
        #region Creation
        /// <summary>Creates explicit input references for a generated menu.</summary>
        /// <param name="host">New menu host.</param>
        /// <param name="directory">Existing project folder receiving the action asset.</param>
        /// <returns>The generated input asset path.</returns>
        internal static string Configure(MenuHost host, string directory)
        {
            // Each generated setup gets its own asset so changing a preset cannot overwrite existing input.
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap ui = asset.AddActionMap("UI");
            InputAction move = ui.AddAction("Move", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=" + host.Preset.Navigation.Deadzone.ToString(CultureInfo.InvariantCulture) + ",max=1)");
            move.AddBinding("<Gamepad>/dpad");
            AddButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            ui.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position", expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Pointer>/press", expectedControlLayout: "Button");
            ui.AddAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            InputActionMap menu = asset.AddActionMap("Menu");
            AddButton(menu, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            AddButton(menu, "PreviousTab", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            AddButton(menu, "NextTab", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/MenuInput.asset");
            AssetDatabase.CreateAsset(asset, path);
            Dictionary<string, InputActionReference> references = new Dictionary<string, InputActionReference>();
            foreach (InputAction action in asset)
            {
                InputActionReference reference = InputActionReference.Create(action);
                reference.name = action.actionMap.name + "/" + action.name;
                AssetDatabase.AddObjectToAsset(reference, asset);
                references.Add(action.name, reference);
            }

            MenuNavigation settings = host.Preset.Navigation;
            MenuInput input = host.gameObject.AddComponent<MenuInput>();
            input.Host = host;
            input.Pause = settings.Pause != null ? settings.Pause : references["Pause"];
            input.Cancel = settings.Cancel != null ? settings.Cancel : references["Cancel"];
            input.PreviousTab = settings.PreviousTab != null ? settings.PreviousTab : references["PreviousTab"];
            input.NextTab = settings.NextTab != null ? settings.NextTab : references["NextTab"];
            input.CreditsClose = settings.CreditsClose;
            ConfigureEventSystem(host, asset, references);
            AssetDatabase.SaveAssets();
            return path;
        }

        /// <summary>Adds keyboard and gamepad bindings to one button action.</summary>
        /// <param name="map">Action map receiving the control.</param>
        /// <param name="name">Stable action name.</param>
        /// <param name="keyboard">Keyboard control path.</param>
        /// <param name="gamepad">Gamepad control path.</param>
        private static void AddButton(InputActionMap map, string name, string keyboard, string gamepad)
        {
            // Both device families resolve through the same serialized action reference.
            InputAction action = map.AddAction(name, InputActionType.Button, keyboard);
            action.AddBinding(gamepad);
        }

        /// <summary>Creates a scene EventSystem only when no loaded active one already exists.</summary>
        /// <param name="host">Generated menu and destination scene.</param>
        /// <param name="asset">Generated UI action asset.</param>
        /// <param name="references">Generated action references.</param>
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
