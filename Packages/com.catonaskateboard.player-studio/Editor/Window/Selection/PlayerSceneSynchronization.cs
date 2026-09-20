using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Compares preset-owned scene values once when opening a player and stages their reapplication.</summary>
    internal static class PlayerSceneSynchronization
    {
        #region Methods

        #region Proposal

        /// <summary>Stages all differing modules without changing a preset, component or prefab.</summary>
        /// <param name="state">Clean workspace whose selected player supplies the comparison context.</param>
        /// <param name="owner">Window that owns the proposal's Undo record.</param>
        /// <param name="differences">Receives the names of the modules requiring confirmation.</param>
        /// <param name="warning">Receives invalid preset data or an unsupported player context.</param>
        /// <returns>True when a synchronization proposal was created.</returns>
        internal static bool TryStage(PlayerStudioState state, Object owner, out string differences, out string warning)
        {
            // Opening another view never replaces work that was already pending.
            differences = warning = string.Empty;
            PlayerHost host = state.PreviewHost;
            if (EditorApplication.isPlayingOrWillChangePlaymode || state.HasChanges || host == null
                || host.MasterPreset == null || host.MasterPreset != state.Selection.Master)
                return false;
            if (!PlayerCreationUtility.TryValidate(host.MasterPreset, out InputActionAsset actions, out warning)
                || !PlayerVisualPrefabUtility.TryGetPath(host, out string path, out warning))
                return false;

            List<string> changed = new List<string>();
            if (!BodyMatches(host))
                changed.Add("Body");
            if (!InputMatches(host, actions))
                changed.Add("Input");
            if (!MovementMatches(host))
                changed.Add("Locomotion");
            if (!VisualMatches(host))
                changed.Add("Visual");
            if (!CameraMatches(host))
                changed.Add("Camera");
            if (path.Length == 0 || HasManagedOverrides(host))
                changed.Add("Player prefab");
            if (changed.Count == 0)
                return false;

            // These requests reuse the same validated Apply path as ordinary preset and Transform edits.
            Undo.RecordObject(owner, "Stage Player Synchronization");
            state.VisualScene.Refresh(host);
            state.VisualScene.SetDraft(host.MasterPreset.VisualPreset != null, state.VisualScene.Existing);
            state.VisualScene.RequestSynchronization();
            state.CameraScene.Refresh(host);
            state.CameraScene.RequestSynchronization();
            differences = string.Join(", ", changed);
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
        /// <param name="actions">Validated dedicated asset, or null for an unassigned Input slot.</param>
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

        /// <summary>Compares model identity and its full composed local pose, including authored scale.</summary>
        /// <param name="host">Selected scene player.</param>
        /// <returns>True when no visual creation, removal or pose correction is needed.</returns>
        private static bool VisualMatches(PlayerHost host)
        {
            PlayerVisualBinding binding = host.GetComponent<PlayerVisualBinding>();
            PlayerVisualPreset preset = host.MasterPreset.VisualPreset;
            if (preset == null)
                return binding == null;
            preset.TryGetSettings(out PlayerVisualSettings settings, out _);
            if (binding == null)
                return settings.Prefab == null;
            if (binding.Host != host || binding.VisualRoot == null || binding.Model == null
                || binding.SourcePrefab != settings.Prefab || binding.VisualRoot.parent != host.transform)
                return false;
            Matrix4x4 expected = Matrix4x4.TRS(settings.Position, settings.Rotation, Vector3.one * settings.Scale) * binding.BaseMatrix;
            Matrix4x4 actual = Matrix4x4.TRS(binding.VisualRoot.localPosition, binding.VisualRoot.localRotation, binding.VisualRoot.localScale);
            // Matrix comparison includes position, rotation and non-unit authored model scale.
            for (int index = 0; index < 16; index++)
                if (Mathf.Abs(expected[index] - actual[index]) > 0.0001f)
                    return false;
            if (binding.OwnsContainer && settings.Prefab != null)
                return Near(binding.Model.transform.localPosition, settings.Prefab.transform.localPosition)
                    && Quaternion.Angle(binding.Model.transform.localRotation, settings.Prefab.transform.localRotation) < 0.01f
                    && Near(binding.Model.transform.localScale, settings.Prefab.transform.localScale);
            return true;
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
                    ("visual", host.GetComponent<PlayerVisualBinding>()), ("motor", host.GetComponent<PlayerCharacterControllerMotor>())))
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
                if (IsManaged(added.instanceComponent, host))
                    return true;
            foreach (ObjectOverride changed in PrefabUtility.GetObjectOverrides(host.gameObject, false))
                if (changed.instanceObject is Component component && IsManaged(component, host))
                    return true;
            return false;
        }

        /// <summary>Identifies only components controlled by Player Studio on this root or its camera.</summary>
        /// <param name="component">Potential overridden component.</param>
        /// <param name="host">Player defining the owned hierarchy.</param>
        /// <returns>True for a managed component; the root placement Transform is excluded.</returns>
        private static bool IsManaged(Component component, PlayerHost host)
        {
            if (component == null)
                return false;
            PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
            return component.gameObject == host.gameObject && (component is PlayerHost || component is CharacterController
                || component is PlayerInput || component is PlayerInputBridge || component is PlayerCharacterControllerMotor
                || component is PlayerVisualBinding || component is PlayerCameraRig)
                || rig != null && rig.View != null && component.gameObject == rig.View.gameObject;
        }

        #endregion

        #endregion
    }
}
