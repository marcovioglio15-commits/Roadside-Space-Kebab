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
                AssetDatabase.SaveAssetIfDirty(master);
                GameObject player = CreatePlayer(state, master, scene);
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

        /// <summary>Copies the selected hierarchy so tool targets and authored children remain available.</summary>
        /// <param name="state">Workspace with an optional scene player.</param>
        /// <param name="master">Isolated test configuration.</param>
        /// <param name="scene">Temporary preview scene.</param>
        /// <returns>A configured player whose internal references belong to the copy.</returns>
        private static GameObject CreatePlayer(PlayerStudioState state, PlayerMasterPreset master, Scene scene)
        {
            // A master alone creates only configured components; geometry belongs to the selected hierarchy.
            if (state.PreviewHost == null)
                return PlayerCreationUtility.Create(master, scene, "Test Player");
            GameObject copy = UnityEngine.Object.Instantiate(state.PreviewHost.gameObject);
            copy.name = "Test Player";
            SceneManager.MoveGameObjectToScene(copy, scene);
            PlayerHost host = copy.GetComponent<PlayerHost>();
            PlayerCreationUtility.SetReferences(host, ("masterPreset", master));
            PlayerRootConfiguration.Apply(host);
            PlayerCameraRig rig = copy.GetComponent<PlayerCameraRig>();
            if (rig == null)
                rig = copy.AddComponent<PlayerCameraRig>();
            Camera view = rig.View;
            if (view == null || !view.transform.IsChildOf(copy.transform))
            {
                GameObject child = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
                child.transform.SetParent(copy.transform, false);
                view = child.GetComponent<Camera>();
            }
            PlayerCreationUtility.SetReferences(rig, ("host", host), ("view", view),
                ("input", copy.GetComponent<UnityEngine.InputSystem.PlayerInput>()),
                ("motor", copy.GetComponent<PlayerCharacterControllerMotor>()));
            rig.enabled = true;
            return copy;
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
            // Remap explicit scene choices into the copied hierarchy without creating substitute anchors.
            foreach ((string Name, Transform Source) role in new[]
                { ("target", state.CameraScene.Target), ("model", state.CameraScene.Model) })
            {
                Transform selected = null;
                if (role.Source != null && state.PreviewHost != null)
                {
                    if (!role.Source.IsChildOf(state.PreviewHost.transform))
                        throw new InvalidOperationException("Camera targets must belong to the selected player for Quick Play.");
                    selected = PlayerHierarchy.Resolve(rig.transform, AnimationUtility.CalculateTransformPath(role.Source, state.PreviewHost.transform));
                    if (selected == null)
                        throw new InvalidOperationException("Use unique hierarchy names for the camera targets before Quick Play.");
                }
                PlayerCreationUtility.SetReferences(rig, (role.Name, selected));
            }
            master.CameraPreset.TryGetSettings(out PlayerCameraSettings camera, out _);
            rig.View.enabled = true;
            rig.ApplyConfiguration(camera);
        }

        #endregion
    }
}
