using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Prepares a temporary playable scene and prefab from the workspace without touching its player.</summary>
    internal static class PlayerQuickPlayScene
    {
        #region Methods

        /// <summary>Copies the saved test environment and inserts the complete proposed player.</summary>
        /// <param name="state">Source workspace with optional scene-specific poses and references.</param>
        /// <param name="folder">Owned temporary directory for this run.</param>
        /// <returns>The temporary scene to use as Unity's Play start scene.</returns>
        internal static SceneAsset Build(PlayerStudioState state, string folder)
        {
            // Play uses a copy, so testing never saves over the editable obstacle course.
            PlayerTestScene.Ensure();
            Scene authored = SceneManager.GetSceneByPath(PlayerTestScene.Path);
            if (authored.IsValid() && authored.isDirty)
                throw new InvalidOperationException("Save the Player Test scene before testing its latest obstacle changes.");
            string path = folder + "/Test.unity";
            if (!AssetDatabase.CopyAsset(PlayerTestScene.Path, path))
                throw new IOException("Unity could not copy the test scene.");
            PlayerMasterPreset master = PlayerQuickPlayConfiguration.Create(state, folder);
            Scene scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                // Exactly one marker provides an explicit spawn; names are never used to identify the player.
                PlayerTestSpawn spawn = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (PlayerTestSpawn marker in root.GetComponentsInChildren<PlayerTestSpawn>(true))
                    {
                        if (spawn != null)
                            throw new InvalidOperationException("The test scene must contain exactly one Player Test Spawn.");
                        spawn = marker;
                    }
                if (spawn == null)
                    throw new InvalidOperationException("Add one Player Test Spawn to the editable test scene.");
                if (master.CameraPreset == null)
                    throw new InvalidOperationException("Assign a Camera preset before Quick Play so the test has a player view.");
                PrepareVisual(state, master, scene, folder);
                AssetDatabase.SaveAssetIfDirty(master);
                GameObject player = PlayerCreationUtility.Create(master, scene, "Test Player");
                Position(state, player.transform, spawn.transform);
                ConfigureCamera(state, player.GetComponent<PlayerCameraRig>(), master);
                // Preview scenes cannot be saved; the isolated prefab is inserted when the copied environment loads.
                if (PrefabUtility.SaveAsPrefabAsset(player, folder + "/Player.prefab") == null)
                    throw new IOException("Unity could not save the temporary test player.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        /// <summary>Transfers adopted geometry and released management without modifying the original model.</summary>
        /// <param name="state">Workspace containing the visual proposal.</param>
        /// <param name="master">Temporary configuration that may receive a geometry snapshot.</param>
        /// <param name="scene">Temporary scene used to build the snapshot.</param>
        /// <param name="folder">Owned folder receiving an optional static model prefab.</param>
        private static void PrepareVisual(PlayerStudioState state, PlayerMasterPreset master, Scene scene, string folder)
        {
            // Prefab-backed managed models already have all their proposed offsets in the copied preset.
            PlayerVisualSceneSession visual = state.VisualScene;
            if (visual.Host == null || master.VisualPreset == null)
                return;
            master.VisualPreset.TryGetSettings(out PlayerVisualSettings settings, out _);
            bool released = visual.HasChanges && !visual.Managed;
            if (!released && settings.Prefab != null)
                return;
            if (!released && visual.Binding != null && visual.Binding.SourcePrefab != null
                && visual.Existing == visual.Binding.Model)
                return;
            GameObject source = released ? visual.Binding != null ? visual.Binding.Model : null : visual.Existing;
            if (source == null)
            {
                if (released)
                    PlayerCreationUtility.SetReferences(master, ("visualPreset", null));
                return;
            }
            if (!PlayerVisualModelValidation.TryValidate(source, out string warning))
                throw new InvalidOperationException(warning);
            GameObject copy = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(copy, scene);
            try
            {
                Matrix4x4 pose = released ? visual.Host.transform.worldToLocalMatrix * source.transform.localToWorldMatrix
                    : visual.Binding != null ? visual.Binding.BaseMatrix : visual.AdoptedMatrix;
                copy.transform.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
                copy.transform.localScale = pose.lossyScale;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, folder + "/Visual.prefab");
                PlayerCreationUtility.SetReferences(master.VisualPreset, ("prefab", prefab));
                if (released)
                {
                    using SerializedObject properties = new SerializedObject(master.VisualPreset);
                    PlayerVisualDraft draft = PlayerVisualDraft.Read(properties);
                    draft.SetOffset(Vector3.zero, Vector3.zero, 1f);
                    draft.Write(properties);
                    properties.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.SaveAssetIfDirty(master.VisualPreset);
                AssetDatabase.SaveAssetIfDirty(master);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        /// <summary>Uses the test spawn as the origin while retaining proposed orientation, scale and displacement.</summary>
        /// <param name="state">Workspace with an optional selected scene player.</param>
        /// <param name="player">New temporary player root.</param>
        /// <param name="spawn">Editable test starting pose.</param>
        private static void Position(PlayerStudioState state, Transform player, Transform spawn)
        {
            Vector3 displacement = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one;
            if (state.PreviewHost != null)
            {
                // The pose baseline and proposed Body were validated together before creating the test copy.
                displacement = state.Transform.WorldPosition - state.PreviewHost.transform.position;
                rotation = state.Transform.WorldRotation;
                scale = state.Transform.WorldMatrix.lossyScale;
            }
            player.SetPositionAndRotation(spawn.position + spawn.rotation * displacement, spawn.rotation * rotation);
            player.localScale = scale;
            PlayerHost host = player.GetComponent<PlayerHost>();
            host.MasterPreset.TryGetBodySettings(out PlayerBodySettings body, out _);
            if (!PlayerCharacterControllerBody.TryConfigure(host.BodyController, host.transform, body, out string bodyWarning))
                throw new InvalidOperationException(bodyWarning);
        }

        /// <summary>Copies camera component options and maps an authored focus anchor into the isolated player.</summary>
        /// <param name="state">Workspace supplying optional scene camera references.</param>
        /// <param name="rig">New test rig.</param>
        /// <param name="master">Temporary camera settings.</param>
        private static void ConfigureCamera(PlayerStudioState state, PlayerCameraRig rig, PlayerMasterPreset master)
        {
            if (state.CameraScene.View != null)
                EditorUtility.CopySerialized(state.CameraScene.View, rig.View);
            Transform source = state.CameraScene.Target;
            if (source != null && state.PreviewHost != null)
            {
                if (!source.IsChildOf(state.PreviewHost.transform))
                    throw new InvalidOperationException("The test focus anchor must belong to the selected player.");
                Transform target = new GameObject("Focus Anchor").transform;
                target.SetParent(rig.transform, false);
                target.localPosition = state.PreviewHost.transform.InverseTransformPoint(source.position);
                target.localRotation = Quaternion.Inverse(state.PreviewHost.transform.rotation) * source.rotation;
                target.localScale = (state.PreviewHost.transform.worldToLocalMatrix * source.localToWorldMatrix).lossyScale;
                PlayerCreationUtility.SetReferences(rig, ("target", target));
            }
            master.CameraPreset.TryGetSettings(out PlayerCameraSettings camera, out _);
            rig.View.enabled = true;
            rig.ApplyConfiguration(camera);
        }

        #endregion
    }
}
