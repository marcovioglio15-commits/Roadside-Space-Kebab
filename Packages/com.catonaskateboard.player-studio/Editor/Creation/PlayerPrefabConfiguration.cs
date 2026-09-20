using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Validates player ownership before Apply and saves managed components to each player's prefab.</summary>
    internal sealed class PlayerPrefabConfiguration
    {
        #region State

        private readonly List<PlayerHost> hosts = new List<PlayerHost>();

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Collects the loaded players affected by the confirmed asset or explicit setup.</summary>
        /// <param name="preset">Pending asset values; an empty batch represents scene setup only.</param>
        /// <param name="visual">Optional explicit visual setup.</param>
        /// <param name="camera">Optional explicit camera setup.</param>
        /// <param name="plan">Receives the validated set of players.</param>
        /// <param name="warning">Receives an ownership or configuration problem before any writes.</param>
        /// <returns>True when each affected player can save a complete configuration.</returns>
        internal static bool TryPrepare(PlayerPresetBatch preset, PlayerVisualSceneSession visual,
            PlayerCameraSceneSession camera, out PlayerPrefabConfiguration plan, out string warning)
        {
            // Discovery is limited to confirmation, not repaint or runtime updates.
            plan = new PlayerPrefabConfiguration();
            warning = string.Empty;
            foreach (PlayerHost host in UnityEngine.Object.FindObjectsByType<PlayerHost>(FindObjectsInactive.Include))
            {
                if (EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid()
                    || EditorSceneManager.IsPreviewScene(host.gameObject.scene) || host.MasterPreset == null)
                    continue;
                PlayerMasterPreset master = host.MasterPreset;
                bool selected = visual != null && visual.HasChanges && visual.Host == host
                    || camera != null && camera.HasChanges && camera.Host == host;
                if (!selected && preset.Find(master) == null && !preset.Affects(master, "bodyPreset", master.BodyPreset)
                    && !preset.Affects(master, "inputPreset", master.InputPreset) && !preset.Affects(master, "locomotionPreset", master.LocomotionPreset)
                    && !preset.Affects(master, "visualPreset", master.VisualPreset) && !preset.Affects(master, "cameraPreset", master.CameraPreset))
                    continue;
                if (!PlayerVisualPrefabUtility.TryGetPath(host, out _, out warning)
                    || !preset.TryValidate(master, out warning))
                    return false;
                PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
                Camera view = camera != null && camera.HasChanges && camera.Host == host ? camera.View : rig != null ? rig.View : null;
                Transform target = camera != null && camera.HasChanges && camera.Host == host ? camera.Target : rig != null ? rig.Target : null;
                if (view != null && !view.transform.IsChildOf(host.transform)
                    || target != null && !target.IsChildOf(host.transform))
                {
                    warning = "Camera and focus anchor must belong to the player hierarchy so they can be saved in its prefab.";
                    return false;
                }
                plan.hosts.Add(host);
            }
            plan.RetainPrefabOwners(visual != null && visual.HasChanges ? visual.Host
                : camera != null && camera.HasChanges ? camera.Host : null);
            return true;
        }

        /// <summary>Chooses one instance per prefab so propagation cannot invalidate additions on another instance.</summary>
        /// <param name="preferred">Explicitly edited instance, whose proposed references take precedence.</param>
        private void RetainPrefabOwners(PlayerHost preferred)
        {
            // Validate every instance first, then let Unity propagate the confirmed prefab to its other instances.
            if (preferred != null && hosts.Remove(preferred))
                hosts.Insert(0, preferred);
            HashSet<PlayerHost> sources = new HashSet<PlayerHost>();
            for (int index = 0; index < hosts.Count; index++)
            {
                PlayerHost source = PrefabUtility.GetCorrespondingObjectFromSource(hosts[index]);
                if (source != null && !sources.Add(source))
                    hosts.RemoveAt(index--);
            }
        }

        #endregion

        #region Application

        /// <summary>Captures affected prefab files before visual or camera operations can persist changes.</summary>
        /// <param name="snapshot">File recovery record for the current confirmation.</param>
        internal void CaptureFiles(PlayerPresetSaveSnapshot snapshot)
        {
            // Prefab ownership and write access have already passed preparation.
            foreach (PlayerHost host in hosts)
                if (PlayerVisualPrefabUtility.TryGetPath(host, out string path, out _))
                    snapshot.Capture(path);
        }

        /// <summary>Configures root modules before Visual and Camera bind to their dependencies.</summary>
        internal void Configure()
        {
            // Called inside the common Undo group, after preset values have been confirmed.
            foreach (PlayerHost host in hosts)
                PlayerRootConfiguration.Apply(host);
        }

        /// <summary>Saves managed additions and properties without applying unrelated instance overrides.</summary>
        /// <param name="snapshot">Recovery record that also tracks newly created prefab paths.</param>
        internal void Save(PlayerPresetSaveSnapshot snapshot)
        {
            // A previously scene-only player receives a unique prefab only during explicit Apply.
            foreach (PlayerHost host in hosts)
                Save(host, snapshot);
        }

        /// <summary>Persists one configured player, including camera children and root modules.</summary>
        /// <param name="host">Scene player whose managed objects have already been configured.</param>
        /// <param name="snapshot">Recovery record for this synchronous batch.</param>
        private static void Save(PlayerHost host, PlayerPresetSaveSnapshot snapshot)
        {
            // A new player prefab retains the complete authored hierarchy, including unrelated children.
            if (!PlayerVisualPrefabUtility.TryGetPath(host, out string path, out string warning))
                throw new InvalidOperationException(warning);
            if (path.Length == 0)
            {
                string folder = PlayerDefaultAssets.ContentRoot + "/Players";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(PlayerDefaultAssets.ContentRoot, "Players");
                path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + host.name + ".prefab");
                snapshot.Capture(path);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(host.gameObject, path, InteractionMode.UserAction) == null)
                    throw new InvalidOperationException("Unity could not save the player prefab.");
                return;
            }

            // Apply dependencies first so serialized references resolve to their prefab counterparts.
            PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
            if (rig != null && rig.Target != null)
                PlayerVisualPrefabUtility.ApplyChild(rig.Target.gameObject, path);
            if (rig != null && rig.View != null)
            {
                PlayerVisualPrefabUtility.ApplyChild(rig.View.gameObject, path);
                // The camera child is fully owned by Player Studio, including its URP data component.
                foreach (Component component in rig.View.GetComponents<Component>())
                    ApplyComponent(component, path);
            }
            foreach (Component component in host.GetComponents<Component>())
                if (component is PlayerInput || component is PlayerInputBridge || component is PlayerCharacterControllerMotor
                    || component is PlayerCameraRig || component is CharacterController)
                    if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, path) == null)
                        PrefabUtility.ApplyAddedComponent(component, path, InteractionMode.UserAction);
            ApplyComponent(host.BodyController, path);
            ApplyComponent(host.GetComponent<PlayerInput>(), path);
            ApplyComponent(host.GetComponent<PlayerInputBridge>(), path);
            ApplyComponent(host.GetComponent<PlayerCharacterControllerMotor>(), path);
            ApplyComponent(rig, path);
            ApplyComponent(host, path);
        }

        /// <summary>Applies only one owned component, preserving other components and root placement overrides.</summary>
        /// <param name="component">Managed component, or null when its module is absent.</param>
        /// <param name="path">Owning player prefab, never a nested visual prefab.</param>
        private static void ApplyComponent(Component component, string path)
        {
            // Camera Transform is owned; the player root Transform intentionally remains scene placement.
            if (component == null)
                return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, path) == null)
                PrefabUtility.ApplyAddedComponent(component, path, InteractionMode.UserAction);
            else
                PrefabUtility.ApplyObjectOverride(component, path, InteractionMode.UserAction);
        }

        #endregion

        #endregion
    }
}
