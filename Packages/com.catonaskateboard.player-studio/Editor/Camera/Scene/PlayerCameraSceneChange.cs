using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Prepares camera setup and lens updates before the shared preset transaction writes anything.</summary>
    internal sealed class PlayerCameraSceneChange
    {
        #region State

        private readonly List<Operation> operations = new List<Operation>();

        #endregion

        #region Types

        /// <summary>Retains one fully validated camera operation until synchronous Apply.</summary>
        private readonly struct Operation
        {
            public readonly PlayerHost Host;
            public readonly Camera View;
            public readonly Transform Target;
            public readonly PlayerCameraSettings Settings;
            public readonly bool Enabled;

            #region Methods

            /// <summary>Captures one scene camera proposal without changing its objects.</summary>
            /// <param name="host">Player receiving the setup.</param>
            /// <param name="view">Existing camera or null for a new child.</param>
            /// <param name="target">Optional focus anchor.</param>
            /// <param name="settings">Validated configuration snapshot.</param>
            /// <param name="enabled">Whether the master retains its camera module.</param>
            public Operation(PlayerHost host, Camera view, Transform target, PlayerCameraSettings settings, bool enabled)
            {
                // Immutable operation data cannot drift while preset values are being confirmed.
                Host = host;
                View = view;
                Target = target;
                Settings = settings;
                Enabled = enabled;
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Finds loaded players affected by a Camera preset, a slot change or an explicit scene setup.</summary>
        /// <param name="preset">Pending preset properties, or null for scene-only changes.</param>
        /// <param name="session">Selected camera setup proposal.</param>
        /// <param name="visual">Visual setup whose changed binding must be reconnected after Apply.</param>
        /// <param name="change">Receives the validated plan.</param>
        /// <param name="warning">Receives the first incompatible scene reference.</param>
        /// <returns>True when camera writes can safely join Apply.</returns>
        public static bool TryPrepare(PlayerPresetBatch preset, PlayerCameraSceneSession session, PlayerVisualSceneSession visual,
            out PlayerCameraSceneChange change, out string warning)
        {
            // Discovery happens once per confirmation, never on each Editor repaint.
            change = new PlayerCameraSceneChange();
            warning = string.Empty;
            bool sceneDraft = session != null && session.HasChanges;
            if (sceneDraft && (!session.TryValidate(out warning)
                || !change.TryAdd(session.Host, session.View, session.Target, preset, out warning)))
                return false;
            if (!sceneDraft && visual != null && visual.HasChanges && visual.Host != null)
            {
                PlayerCameraRig rig = visual.Host.GetComponent<PlayerCameraRig>();
                if (rig != null && !change.TryAdd(visual.Host, rig.View, rig.Target, preset, out warning))
                    return false;
            }
            foreach (PlayerHost host in Object.FindObjectsByType<PlayerHost>(FindObjectsInactive.Include))
            {
                if (host.MasterPreset == null || EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid()
                    || EditorSceneManager.IsPreviewScene(host.gameObject.scene) || (sceneDraft && host == session.Host))
                    continue;
                PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
                bool affected = preset.Affects(host.MasterPreset, "cameraPreset", host.MasterPreset.CameraPreset)
                    || rig != null && preset.Affects(host.MasterPreset, "visualPreset", host.MasterPreset.VisualPreset);
                if (!affected)
                    continue;
                if (!change.TryAdd(host, rig != null ? rig.View : null, rig != null ? rig.Target : null, preset, out warning))
                    return false;
            }
            return true;
        }

        /// <summary>Validates proposed references and active settings for a single loaded player.</summary>
        /// <param name="host">Player whose camera will be configured.</param>
        /// <param name="view">Optional existing camera.</param>
        /// <param name="target">Optional focus anchor.</param>
        /// <param name="preset">Pending asset included in the same Apply.</param>
        /// <param name="warning">Receives an incompatible reference or setting.</param>
        /// <returns>True when the operation was prepared or no Camera slot is assigned.</returns>
        private bool TryAdd(PlayerHost host, Camera view, Transform target, PlayerPresetBatch preset, out string warning)
        {
            // The camera must never move the player root, an asset transform or a target below itself.
            warning = string.Empty;
            if (host == null || host.MasterPreset == null || (view != null && (EditorUtility.IsPersistent(view)
                || host.transform.IsChildOf(view.transform) || (target != null && target.IsChildOf(view.transform))))
                || (target != null && EditorUtility.IsPersistent(target)))
            {
                warning = "Use a loaded camera that does not contain the player or its focus anchor.";
                return false;
            }
            // An explicit setup is added first; the other instances inherit its one prefab camera.
            PlayerHost sourceHost = PrefabUtility.GetCorrespondingObjectFromSource(host);
            foreach (Operation operation in operations)
                if (operation.Host == host || sourceHost != null
                    && PrefabUtility.GetCorrespondingObjectFromSource(operation.Host) == sourceHost)
                    return true;
            PlayerCameraPreset source = preset.Slot(host.MasterPreset, "cameraPreset", host.MasterPreset.CameraPreset);
            if (source == null)
            {
                operations.Add(new Operation(host, view, target, default, false));
                return true;
            }
            PlayerCameraSettings settings;
            SerializedObject proposed = preset.Find(source);
            if (proposed != null)
                settings = (PlayerCameraSettings)proposed.FindProperty("settings").boxedValue;
            else if (!source.TryGetSettings(out settings, out warning))
                return false;
            if (!settings.TryValidate(out warning))
                return false;
            operations.Add(new Operation(host, view, target, settings, true));
            return true;
        }

        #endregion

        #region Application

        /// <summary>Creates missing camera objects and applies validated settings inside the caller's Undo group.</summary>
        public void Apply()
        {
            // All objects are prepared in Editor; no runtime camera or UI construction is needed.
            foreach (Operation operation in operations)
            {
                PlayerCameraRig existingRig = operation.Host.GetComponent<PlayerCameraRig>();
                if (!operation.Enabled)
                {
                    SetEnabled(existingRig, false);
                    SetEnabled(operation.View, false);
                    if (operation.View != null)
                        SetEnabled(operation.View.GetComponent<AudioListener>(), false);
                    continue;
                }
                Camera view = operation.View;
                if (view == null)
                {
                    GameObject cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
                    SceneManager.MoveGameObjectToScene(cameraObject, operation.Host.gameObject.scene);
                    Undo.RegisterCreatedObjectUndo(cameraObject, "Create Player Camera");
                    Undo.SetTransformParent(cameraObject.transform, operation.Host.transform, "Parent Player Camera");
                    view = cameraObject.GetComponent<Camera>();
                }
                PlayerCameraRig rig = operation.Host.GetComponent<PlayerCameraRig>();
                if (rig == null)
                    rig = Undo.AddComponent<PlayerCameraRig>(operation.Host.gameObject);
                using (SerializedObject serialized = new SerializedObject(rig))
                {
                    serialized.FindProperty("host").objectReferenceValue = operation.Host;
                    serialized.FindProperty("view").objectReferenceValue = view;
                    serialized.FindProperty("target").objectReferenceValue = operation.Target;
                    serialized.FindProperty("input").objectReferenceValue = operation.Host.GetComponent<PlayerInput>();
                    serialized.FindProperty("visual").objectReferenceValue = operation.Host.GetComponent<PlayerVisualBinding>();
                    serialized.FindProperty("motor").objectReferenceValue = operation.Host.GetComponent<PlayerCharacterControllerMotor>();
                    serialized.ApplyModifiedProperties();
                }
                Undo.RegisterCompleteObjectUndo(new Object[] { view, view.transform }, "Apply Player Camera");
                SetEnabled(rig, true);
                SetEnabled(view, true);
                SetEnabled(view.GetComponent<AudioListener>(), true);
                rig.ApplyConfiguration(operation.Settings);
                if (PrefabUtility.IsPartOfPrefabInstance(view))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
                }
            }
        }

        /// <summary>Changes activation with Undo when a Camera slot is explicitly added or cleared.</summary>
        /// <param name="component">Existing owned camera component, if present.</param>
        /// <param name="enabled">Requested activation state.</param>
        private static void SetEnabled(Behaviour component, bool enabled)
        {
            // Clearing a slot leaves authored objects available for reuse and Undo.
            if (component == null || component.enabled == enabled)
                return;
            Undo.RecordObject(component, "Change Player Camera Activation");
            component.enabled = enabled;
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        #endregion

        #endregion
    }
}
