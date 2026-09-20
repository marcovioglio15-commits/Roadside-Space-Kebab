using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Connects Body, Input and Locomotion to the confirmed master before their prefab is saved.</summary>
    internal static class PlayerRootConfiguration
    {
        #region Methods

        /// <summary>Creates missing root components and updates assigned action roles inside the caller's Undo group.</summary>
        /// <param name="host">Existing player with a validated master.</param>
        internal static void Apply(PlayerHost host)
        {
            // Saved presets have already passed candidate validation; no fallback values are invented.
            PlayerMasterPreset master = host.MasterPreset;
            if (!PlayerCreationUtility.TryValidate(master, out InputActionAsset actions, out string warning))
                throw new InvalidOperationException(warning);
            CharacterController controller = host.BodyController;
            if (controller == null)
                controller = GetOrAdd<CharacterController>(host.gameObject);
            SetReferences(host, ("bodyController", controller));
            using (SerializedObject configuration = new SerializedObject(host))
            {
                configuration.FindProperty("bodyBinding").intValue = (int)PlayerBodyBinding.CharacterController;
                configuration.ApplyModifiedProperties();
            }
            master.TryGetBodySettings(out PlayerBodySettings body, out _);
            Undo.RegisterCompleteObjectUndo(controller, "Configure Player Body");
            if (!PlayerCharacterControllerBody.TryConfigure(controller, host.transform, body, out warning))
                throw new InvalidOperationException(warning);

            PlayerInput input = host.GetComponent<PlayerInput>();
            PlayerInputBridge bridge = host.GetComponent<PlayerInputBridge>();
            if (actions != null)
            {
                input = GetOrAdd<PlayerInput>(host.gameObject);
                bridge = GetOrAdd<PlayerInputBridge>(host.gameObject);
                Undo.RecordObject(input, "Configure Player Input");
                using SerializedObject preset = new SerializedObject(master.InputPreset);
                InputActionReference movement = (InputActionReference)preset.FindProperty("movementAction").objectReferenceValue;
                input.actions = actions;
                input.defaultActionMap = movement.action.actionMap.id.ToString();
                input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                SetReferences(bridge, ("host", host), ("playerInput", input));
            }
            SetEnabled(input, actions != null);
            SetEnabled(bridge, actions != null);
            PlayerCharacterControllerMotor motor = host.GetComponent<PlayerCharacterControllerMotor>();
            if (master.LocomotionPreset != null)
            {
                motor = GetOrAdd<PlayerCharacterControllerMotor>(host.gameObject);
                SetReferences(motor, ("host", host), ("input", bridge));
            }
            SetEnabled(motor, master.LocomotionPreset != null);
        }

        /// <summary>Reuses a component or adds it with Undo at the explicit configuration boundary.</summary>
        /// <typeparam name="T">Required component type.</typeparam>
        /// <param name="root">Player object receiving the component.</param>
        /// <returns>The existing or newly added component.</returns>
        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            // Creation occurs only in Editor Apply, never during runtime updates.
            return root.TryGetComponent(out T component) ? component : Undo.AddComponent<T>(root);
        }

        /// <summary>Records reference updates for rollback and normal Undo.</summary>
        /// <param name="component">Owned component to configure.</param>
        /// <param name="references">Explicit serialized reference assignments.</param>
        private static void SetReferences(Component component, params (string Name, UnityEngine.Object Value)[] references)
        {
            // The creation helper writes without Undo, so capture its destination first.
            Undo.RecordObject(component, "Connect Player Modules");
            PlayerCreationUtility.SetReferences(component, references);
        }

        /// <summary>Disables absent optional modules while retaining their authored components for Undo and reuse.</summary>
        /// <param name="component">Optional existing behaviour.</param>
        /// <param name="enabled">Whether its master slot is active.</param>
        private static void SetEnabled(Behaviour component, bool enabled)
        {
            // Do not record no-op edits or remove unrelated scripts.
            if (component == null || component.enabled == enabled)
                return;
            Undo.RecordObject(component, "Configure Player Module Activation");
            component.enabled = enabled;
        }

        #endregion
    }
}
