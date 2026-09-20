using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains scene placement fields and creates a configured player from an applied master.</summary>
    [Serializable]
    internal sealed class PlayerStudioPlacement
    {
        #region Serialized State

        [Header("Placement")]
        [Tooltip("Saved scene receiving the player. It is opened additively when not already loaded.")]
        [SerializeField]
        private SceneAsset sceneAsset;

        [Tooltip("Name of the new root GameObject.")]
        [SerializeField]
        private string playerName = "Player";

        [Tooltip("World position of the new player's feet.")]
        [SerializeField]
        private Vector3 position;

        [Tooltip("World rotation around the vertical axis, in degrees.")]
        [SerializeField]
        private float yaw;

        [Tooltip("Use the new player view by disabling other active cameras and listeners in the destination scene, with Undo.")]
        [SerializeField]
        private bool usePlayerView = true;

        #endregion

        #region Labels

        private static readonly GUIContent sceneLabel = new GUIContent("Scene", "Saved scene receiving the player; other open scenes remain loaded.");
        private static readonly GUIContent nameLabel = new GUIContent("Player Name", "Name for the new player root.");
        private static readonly GUIContent positionLabel = new GUIContent("Position", "World position of the player's feet, in metres.");
        private static readonly GUIContent yawLabel = new GUIContent("Yaw", "Rotation around world Y in degrees; the body stays upright.");
        private static readonly GUIContent placeLabel = new GUIContent("Place Player", "Create the configured body, input, movement, visual and camera. Supports Undo; save the scene normally.");

        #endregion

        #region Methods

        #region Controls

        /// <summary>Draws placement in the preview panel without affecting preset drafts.</summary>
        /// <param name="master">Applied master chosen in the workspace, or null in direct Body mode.</param>
        /// <param name="createdHost">Receives the new player only after a successful explicit creation.</param>
        /// <param name="warning">Receives the reason an attempted placement failed.</param>
        /// <returns>True when the placement button was pressed, even if validation refused creation.</returns>
        public bool Draw(PlayerMasterPreset master, out PlayerHost createdHost, out string warning)
        {
            // Opening this section changes only presentation.
            createdHost = null;
            warning = string.Empty;

            sceneAsset = (SceneAsset)EditorGUILayout.ObjectField(sceneLabel, sceneAsset, typeof(SceneAsset), false);
            playerName = EditorGUILayout.TextField(nameLabel, playerName);
            position = EditorGUILayout.Vector3Field(positionLabel, position);
            yaw = EditorGUILayout.FloatField(yawLabel, yaw);
            if (master != null && master.CameraPreset != null)
                usePlayerView = EditorGUILayout.Toggle(new GUIContent("Use Player View",
                    "Disable other active cameras and audio listeners in the destination scene with Undo."), usePlayerView);

            // A Body alone does not supply the master reference required by PlayerHost.
            if (master == null)
                EditorGUILayout.LabelField("Choose a master to place its player.", EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(master == null))
            {
                if (!GUILayout.Button(placeLabel))
                    return false;

                TryPlace(master, out createdHost, out warning);
                return true;
            }
        }

        /// <summary>Uses the selected player's saved scene as a convenient placement destination.</summary>
        /// <param name="path">Scene asset path, or empty for an unsaved scene.</param>
        public void UseScene(string path)
        {
            // An unsaved scene cannot be represented by a SceneAsset field.
            if (!string.IsNullOrEmpty(path))
                sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        #endregion

        #region Creation

        /// <summary>Creates an upright native player in the requested scene without saving it or changing other scenes.</summary>
        /// <param name="master">Persistent master with a valid applied Body.</param>
        /// <param name="createdHost">Receives the configured scene component on success.</param>
        /// <param name="warning">Receives invalid input or a native binding incompatibility.</param>
        /// <returns>True when the complete player was created and registered with Undo.</returns>
        public bool TryPlace(PlayerMasterPreset master, out PlayerHost createdHost, out string warning)
        {
            // Validate entered values without correcting the name, position or rotation.
            createdHost = null;
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode || sceneAsset == null || master == null || !EditorUtility.IsPersistent(master))
            {
                warning = "Placement requires Edit mode, a saved scene and a saved master.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerName) || playerName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || !float.IsFinite(position.x) || !float.IsFinite(position.y)
                || !float.IsFinite(position.z) || !float.IsFinite(yaw))
            {
                warning = "Enter a player name and finite position and yaw values.";
                return false;
            }

            if (!PlayerCreationUtility.TryValidate(master, out _, out warning))
                return false;

            // Opening additively never replaces another open scene or its unsaved work.
            GameObject player = null;
            try
            {
                string masterPath = AssetDatabase.GetAssetPath(master);
                string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    master = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>(masterPath);
                }

                // Build every configured module before recording the complete creation snapshot.
                player = PlayerCreationUtility.Create(master, scene, playerName);
                createdHost = player.GetComponent<PlayerHost>();
                player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                if (PrefabUtility.IsPartOfPrefabInstance(player))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                PlayerCameraRig rig = player.GetComponent<PlayerCameraRig>();
                if (rig != null && master.CameraPreset.TryGetSettings(out PlayerCameraSettings camera, out _))
                {
                    rig.ApplyConfiguration(camera);
                    if (PrefabUtility.IsPartOfPrefabInstance(rig.View))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rig.View.transform);
                }
                {
                    string folder = PlayerDefaultAssets.ContentRoot + "/Players";
                    if (!AssetDatabase.IsValidFolder(folder))
                        AssetDatabase.CreateFolder(PlayerDefaultAssets.ContentRoot, "Players");
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + playerName + ".prefab");
                    PrefabUtility.SaveAsPrefabAssetAndConnect(player, path, InteractionMode.UserAction);
                }
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.RegisterCreatedObjectUndo(player, "Place Player");
                if (usePlayerView && master.CameraPreset != null)
                    PlayerPlacementView.Activate(player, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = player;
                Undo.CollapseUndoOperations(undoGroup);
                Undo.IncrementCurrentGroup();
                SceneView.RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                // A failed creation must not leave a partially configured root behind.
                if (player != null)
                    UnityEngine.Object.DestroyImmediate(player);

                createdHost = null;
                warning = "Player was not placed: " + exception.Message;
                return false;
            }
        }

        #endregion

        #endregion
    }
}
