using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Builds complete player hierarchies in Editor from explicitly validated preset references.</summary>
    internal static class PlayerCreationUtility
    {
        #region Methods

        #region Validation

        /// <summary>Checks supported modules before creating any scene object.</summary>
        /// <param name="master">Applied configuration to instantiate.</param>
        /// <param name="actions">Receives the selected action asset supplying all input roles.</param>
        /// <param name="warning">Receives an incomplete or incompatible configuration.</param>
        /// <param name="requirePersistent">False only for an isolated validation copy of pending preset values.</param>
        /// <returns>True when all selected modules can be constructed.</returns>
        public static bool TryValidate(PlayerMasterPreset master, out InputActionAsset actions, out string warning, bool requirePersistent = true)
        {
            // Validate all optional modules before allocating a root.
            actions = null;
            warning = "Assign a saved master with a valid Body.";
            if (master == null || requirePersistent && !EditorUtility.IsPersistent(master) || !master.TryGetBodySettings(out _, out warning))
                return false;
            if (master.LocomotionPreset != null && !master.LocomotionPreset.TryGetSettings(out _, out warning)
                || master.CameraPreset != null && !master.CameraPreset.TryGetSettings(out _, out warning))
                return false;
            if (master.ToolsPreset != null && !master.ToolsPreset.TryValidate(out warning))
                return false;
            if (master.InputPreset != null)
            {
                using SerializedObject input = new SerializedObject(master.InputPreset);
                if (!master.InputPreset.TryGetMovementId(out _, out warning)
                    || !master.InputPreset.TryGetJumpId(out _, out warning)
                    || !master.InputPreset.TryGetLookIds(out _, out _, out _, out warning)
                    || !PlayerToolsAuthoring.ValidateInput(master, out warning))
                    return false;
                InputActionReference movement = (InputActionReference)input.FindProperty("movementAction").objectReferenceValue;
                actions = movement.asset;
                foreach (string role in new[] { "jumpAction", "lookDeltaAction", "lookRateAction", "cursorToggleAction" })
                {
                    InputActionReference reference = (InputActionReference)input.FindProperty(role).objectReferenceValue;
                    if (reference != null && (reference.asset != actions || reference.action.actionMap != movement.action.actionMap))
                    {
                        warning = "Creation requires input roles from the same initial action map in the selected action asset.";
                        return false;
                    }
                }
            }
            if (master.LocomotionPreset != null && actions == null)
                warning = "Locomotion requires an Input preset with an assigned movement action.";
            else if (master.LocomotionPreset != null && master.LocomotionPreset.TryGetSettings(out PlayerLocomotionSettings locomotion, out _)
                && locomotion.Jump.Enabled && (!master.InputPreset.TryGetJumpId(out Guid jump, out _) || jump == Guid.Empty))
                warning = "Enabled jumping requires an assigned Jump action.";
            else if (master.CameraPreset != null && master.CameraPreset.TryGetSettings(out PlayerCameraSettings camera, out _)
                && camera.Mode != PlayerCameraMode.Fixed && camera.LookEnabled
                && (master.InputPreset == null || !master.InputPreset.TryGetLookIds(out Guid delta, out Guid rate, out _, out _)
                    || delta == Guid.Empty && rate == Guid.Empty))
                warning = "Camera look requires at least one assigned Look action.";
            else
                warning = string.Empty;
            return warning.Length == 0;
        }

        #endregion

        #region Construction

        /// <summary>Creates a complete root without saving or changing another scene object.</summary>
        /// <param name="master">Validated applied master.</param>
        /// <param name="scene">Destination scene, including an isolated prefab-building scene.</param>
        /// <param name="name">Root name chosen by the caller.</param>
        /// <returns>New configured player; the caller owns creation Undo and cleanup.</returns>
        public static GameObject Create(PlayerMasterPreset master, Scene scene, string name)
        {
            // Refuse incomplete input before any hierarchy changes.
            if (!TryValidate(master, out InputActionAsset actions, out string warning))
                throw new InvalidOperationException(warning);
            GameObject root = new GameObject(name, typeof(CharacterController), typeof(PlayerHost));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            try
            {
                PlayerHost host = root.GetComponent<PlayerHost>();
                CharacterController controller = root.GetComponent<CharacterController>();
                // Small frame durations must still produce contact flags and ground requests.
                controller.minMoveDistance = 0f;
                SetReferences(host, ("masterPreset", master), ("bodyController", controller));
                using (SerializedObject serialized = new SerializedObject(host))
                {
                    serialized.FindProperty("bodyBinding").intValue = (int)PlayerBodyBinding.CharacterController;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                master.TryGetBodySettings(out PlayerBodySettings body, out _);
                if (!PlayerCharacterControllerBody.TryConfigure(controller, root.transform, body, out warning))
                    throw new InvalidOperationException(warning);
                PlayerInput input = null;
                PlayerCharacterControllerMotor motor = null;
                if (actions != null)
                {
                    input = root.AddComponent<PlayerInput>();
                    input.actions = actions;
                    input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                    using SerializedObject preset = new SerializedObject(master.InputPreset);
                    InputActionReference movement = (InputActionReference)preset.FindProperty("movementAction").objectReferenceValue;
                    input.defaultActionMap = movement.action.actionMap.id.ToString();
                    PlayerInputBridge bridge = root.AddComponent<PlayerInputBridge>();
                    SetReferences(bridge, ("host", host), ("playerInput", input));
                    if (master.LocomotionPreset != null)
                    {
                        motor = root.AddComponent<PlayerCharacterControllerMotor>();
                        SetReferences(motor, ("host", host), ("input", bridge));
                    }
                }
                if (master.ToolsPreset != null)
                    root.AddComponent<PlayerTools>();
                if (master.CameraPreset != null)
                {
                    GameObject cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
                    cameraObject.transform.SetParent(root.transform, false);
                    Camera view = cameraObject.GetComponent<Camera>();
                    PlayerCameraRig rig = root.AddComponent<PlayerCameraRig>();
                    SetReferences(rig, ("host", host), ("view", view), ("input", input), ("motor", motor));
                    master.CameraPreset.TryGetSettings(out PlayerCameraSettings camera, out _);
                    rig.ApplyConfiguration(camera);
                }
                root.SetActive(true);
                return root;
            }
            catch
            {
                // Construction failure never leaves a partial player behind.
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }



        /// <summary>Assigns references while constructing new objects before their creation Undo snapshot.</summary>
        /// <param name="target">New component or preset to configure.</param>
        /// <param name="references">Serialized field names and explicit reference values.</param>
        internal static void SetReferences(UnityEngine.Object target, params (string Name, UnityEngine.Object Value)[] references)
        {
            // This helper is used only at creation boundaries, never during gameplay.
            using SerializedObject serialized = new SerializedObject(target);
            foreach ((string Name, UnityEngine.Object Value) reference in references)
                serialized.FindProperty(reference.Name).objectReferenceValue = reference.Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        #endregion
    }
}
