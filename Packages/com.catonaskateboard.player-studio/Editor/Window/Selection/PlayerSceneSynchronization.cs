using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Compares scene values and stages either saved configuration to the selected player.</summary>
    internal static class PlayerSceneSynchronization
    {
        #region Methods

        #region Proposal

        /// <summary>Lists differing modules without creating pending edits or changing the scene.</summary>
        /// <param name="state">Clean workspace whose selected player supplies the comparison context.</param>
        /// <param name="differences">Receives the names of the modules requiring confirmation.</param>
        /// <param name="warning">Receives invalid preset data or an unsupported player context.</param>
        /// <returns>True when the scene differs from its saved presets.</returns>
        internal static bool TryCompare(PlayerStudioState state, out string differences, out string warning)
        {
            // Opening another view never replaces work that was already pending.
            differences = warning = string.Empty;
            PlayerHost host = state.PreviewHost;
            if (EditorApplication.isPlayingOrWillChangePlaymode || state.HasChanges || host == null
                || host.MasterPreset == null || host.MasterPreset != state.Selection.Master)
                return false;
            if (!PlayerCreationUtility.TryValidate(host.MasterPreset, out InputActionAsset actions, out warning)
                || !PlayerPrefabUtility.TryGetPath(host, out string path, out warning))
                return false;

            List<string> changed = new List<string>();
            if (!BodyMatches(host))
                changed.Add("Body");
            if (!InputMatches(host, actions))
                changed.Add("Input");
            if (!MovementMatches(host))
                changed.Add("Locomotion");
            if (!CameraMatches(host))
                changed.Add("Camera");
            if (path.Length == 0 || HasManagedOverrides(host))
                changed.Add("Player prefab");
            if (changed.Count == 0)
                return false;

            differences = string.Join(", ", changed);
            return true;
        }

        /// <summary>Proposes reapplying saved presets after the synchronization direction is chosen.</summary>
        /// <param name="state">Clean workspace whose scene player receives the proposal.</param>
        /// <param name="owner">Window recorded for draft Undo.</param>
        /// <param name="warning">Receives invalid data or a pending-session warning.</param>
        /// <returns>True when the saved configuration is ready for the shared Apply action.</returns>
        internal static bool TryStage(PlayerStudioState state, Object owner, out string warning)
        {
            // Comparison is read-only; opening or cancelling the prompt must not create drafts.
            if (!TryCompare(state, out _, out warning))
            {
                if (state.HasChanges)
                    warning = "Apply or discard the current session before synchronizing saved presets.";
                return false;
            }
            Undo.RecordObject(owner, "Stage Player Synchronization");
            state.Transform.Open(state.PreviewHost);
            state.CameraScene.Refresh(state.PreviewHost);
            state.CameraScene.RequestSynchronization();
            return true;
        }



        #endregion

        #region Comparison

        /// <summary>Compares only dimensions owned by the Body preset; other native controller options stay authored.</summary>
        /// <param name="host">Selected scene player.</param>
        /// <returns>True when the existing controller represents the saved Body.</returns>
        private static bool BodyMatches(PlayerHost host)
        {
            // Body currently owns radius, height and the derived center.
            host.MasterPreset.TryGetBodySettings(out PlayerBodySettings settings, out _);
            CharacterController body = host.BodyController;
            return host.BodyBinding == PlayerBodyBinding.CharacterController && body != null && body.gameObject == host.gameObject
                && Mathf.Approximately(body.radius, settings.Radius) && Mathf.Approximately(body.height, settings.Height)
                && Near(body.center, settings.Center);
        }

        /// <summary>Checks action ownership, the initial map and the bridge references without inspecting hard-coded action names.</summary>
        /// <param name="host">Selected scene player.</param>
        /// <param name="actions">Validated selected asset, or null for an unassigned Input slot.</param>
        /// <returns>True when Input components match the saved configuration.</returns>
        private static bool InputMatches(PlayerHost host, InputActionAsset actions)
        {
            PlayerInput input = host.GetComponent<PlayerInput>();
            PlayerInputBridge bridge = host.GetComponent<PlayerInputBridge>();
            if (actions == null)
                return (input == null || !input.enabled) && (bridge == null || !bridge.enabled);
            if (input == null || bridge == null || !input.enabled || !bridge.enabled || input.actions != actions
                || input.notificationBehavior != PlayerNotifications.InvokeCSharpEvents
                || !ReferencesMatch(bridge, ("host", host), ("playerInput", input)))
                return false;
            // A map name and its ID are equivalent when they resolve to the same authored map.
            using SerializedObject preset = new SerializedObject(host.MasterPreset.InputPreset);
            InputActionReference movement = (InputActionReference)preset.FindProperty("movementAction").objectReferenceValue;
            return actions.FindActionMap(input.defaultActionMap, false) == movement.action.actionMap;
        }

        /// <summary>Checks the motor's applied dependencies; numeric movement values are read from its preset at activation.</summary>
        /// <param name="host">Selected scene player.</param>
        /// <returns>True when the optional motor is connected and enabled as configured.</returns>
        private static bool MovementMatches(PlayerHost host)
        {
            PlayerCharacterControllerMotor motor = host.GetComponent<PlayerCharacterControllerMotor>();
            if (host.MasterPreset.LocomotionPreset == null)
                return motor == null || !motor.enabled;
            return motor != null && motor.enabled && ReferencesMatch(motor, ("host", host), ("input", host.GetComponent<PlayerInputBridge>()));
        }



        /// <summary>Checks lens, pose, activation and the rig's references against the saved Camera preset.</summary>
        /// <param name="host">Selected scene player.</param>
        /// <returns>True when the applied camera is ready without further synchronization.</returns>
        private static bool CameraMatches(PlayerHost host)
        {
            PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
            if (host.MasterPreset.CameraPreset == null)
                return rig == null || !rig.enabled && (rig.View == null || !rig.View.enabled);
            if (rig == null || !rig.enabled || rig.View == null || !rig.View.enabled || rig.View.orthographic
                || !ReferencesMatch(rig, ("host", host), ("input", host.GetComponent<PlayerInput>()),
                    ("motor", host.GetComponent<PlayerCharacterControllerMotor>())))
                return false;
            host.MasterPreset.CameraPreset.TryGetSettings(out PlayerCameraSettings settings, out _);
            Vector3 focus = (rig.Target != null ? rig.Target : host.transform).TransformPoint(settings.TargetOffset);
            PlayerCameraRig.CalculatePose(settings, focus, host.transform.eulerAngles.y, settings.InitialAngles,
                out Vector3 position, out Quaternion rotation);
            AudioListener listener = rig.View.GetComponent<AudioListener>();
            return Near(rig.View.transform.position, position) && Quaternion.Angle(rig.View.transform.rotation, rotation) < 0.01f
                && Mathf.Approximately(rig.View.fieldOfView, settings.FieldOfView)
                && Mathf.Approximately(rig.View.nearClipPlane, settings.NearClip)
                && Mathf.Approximately(rig.View.farClipPlane, settings.FarClip) && (listener == null || listener.enabled);
        }

        /// <summary>Checks known serialized references without runtime reflection or scene searches.</summary>
        /// <param name="component">Existing owned component.</param>
        /// <param name="expected">Field names and the objects required by this player.</param>
        /// <returns>True when every reference matches its expected object.</returns>
        private static bool ReferencesMatch(Component component, params (string Name, Object Value)[] expected)
        {
            using SerializedObject serialized = new SerializedObject(component);
            foreach ((string Name, Object Value) reference in expected)
                if (serialized.FindProperty(reference.Name).objectReferenceValue != reference.Value)
                    return false;
            return true;
        }

        /// <summary>Uses a small positional tolerance to avoid prompts caused only by serialization roundoff.</summary>
        /// <param name="left">Applied scene vector.</param>
        /// <param name="right">Preset-derived vector.</param>
        /// <returns>True when the vectors differ by less than a tenth of a millimetre.</returns>
        private static bool Near(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude < 0.00000001f;
        }

        /// <summary>Detects managed additions and property overrides while preserving unrelated scene customization.</summary>
        /// <param name="host">Player with a source prefab.</param>
        /// <returns>True when confirmed configuration still needs to be stored in that prefab.</returns>
        private static bool HasManagedOverrides(PlayerHost host)
        {
            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(host.gameObject))
                if (PlayerPrefabProperties.HasOverrides(added.instanceComponent, host))
                    return true;
            foreach (ObjectOverride changed in PrefabUtility.GetObjectOverrides(host.gameObject, false))
                if (changed.instanceObject is Component component && PlayerPrefabProperties.HasOverrides(component, host))
                    return true;
            return false;
        }

        #endregion

        #endregion
    }
}
