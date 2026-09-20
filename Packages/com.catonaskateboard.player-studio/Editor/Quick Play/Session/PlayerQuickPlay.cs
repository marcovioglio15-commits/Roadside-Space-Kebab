using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Runs an isolated test scene through Unity Play mode and restores the previous start-scene choice.</summary>
    [InitializeOnLoad]
    internal static class PlayerQuickPlay
    {
        #region State

        private const string manifestPath = "Library/PlayerStudioQuickPlay.json";
        private const string sessions = PlayerTestScene.Folder + "/Sessions";
        private static bool preparing;
        private static bool active = File.Exists(manifestPath);
        private static string scenePath = string.Empty;

        /// <summary>Durable ownership record used after reload or an interrupted Editor session.</summary>
        [Serializable]
        private sealed class RunRecord
        {
            [Tooltip("Unique directory owned only by this temporary test run.")]
            public string Folder;
            [Tooltip("Previously configured Play start scene, restored after the test.")]
            public string PreviousScene;
            [Tooltip("Previous Game view entry behavior, restored when the test finishes.")]
            public bool PreviousFocused;
        }

        /// <summary>Whether this Editor owns an unfinished Quick Play run.</summary>
        internal static bool IsActive => active;

        /// <summary>Exact scene owned by this test, used to reject unrelated runtime cameras.</summary>
        internal static string ScenePath => scenePath;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Reattaches after domain reload and cleans a finished or interrupted run when Edit mode is available.</summary>
        static PlayerQuickPlay()
        {
            // Restore only the owned scene identity; input and rendering are attached by the workspace.
            if (active)
                try
                {
                    scenePath = ReadRecord().Folder + "/Test.unity";
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Player Studio Quick Play recovery: " + exception.Message);
                }
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SceneManager.sceneLoaded += SpawnPlayer;
            EditorApplication.delayCall += Recover;
        }

        /// <summary>Starts with a validated copy of every current draft, without committing the workspace.</summary>
        /// <param name="state">Workspace whose proposals are used for the test.</param>
        /// <param name="warning">Receives a setup or validation failure.</param>
        /// <returns>True once Unity has accepted the Play request.</returns>
        internal static bool TryStart(PlayerStudioState state, out string warning)
        {
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode || IsActive || preparing)
            {
                warning = "Finish the current Play session before starting another test.";
                return false;
            }
            preparing = true;
            try
            {
                if (PlayerDefaultAssets.Ensure() == null)
                    throw new InvalidOperationException("Wait for the default content import to finish.");
                if (!AssetDatabase.IsValidFolder(sessions))
                    AssetDatabase.CreateFolder(PlayerTestScene.Folder, "Sessions");
                string id = Guid.NewGuid().ToString("N");
                RunRecord record = new RunRecord
                {
                    Folder = sessions + "/" + id,
                    PreviousScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                    PreviousFocused = PlayModeWindow.GetPlayModeFocused()
                };
                // Persist ownership before allocating assets, so interrupted preparation is recoverable.
                File.WriteAllText(manifestPath, JsonUtility.ToJson(record));
                active = true;
                AssetDatabase.CreateFolder(sessions, id);
                EditorSceneManager.playModeStartScene = PlayerQuickPlayScene.Build(state, record.Folder);
                scenePath = record.Folder + "/Test.unity";
                PlayModeWindow.SetPlayModeFocused(true);
                EditorApplication.EnterPlaymode();
                return true;
            }
            catch (Exception exception)
            {
                warning = "Quick Play could not start: " + exception.Message;
                preparing = false;
                Recover();
                return false;
            }
            finally
            {
                preparing = false;
            }
        }

        /// <summary>Stops only a test owned by Player Studio, using the same return path as Unity's Stop button.</summary>
        internal static void Stop()
        {
            if (IsActive && EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        /// <summary>Defers cleanup until scene and window restoration have finished.</summary>
        /// <param name="change">Native Play transition.</param>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += Recover;
        }

        /// <summary>Inserts the prepared player only into the environment owned by this Quick Play run.</summary>
        /// <param name="scene">Scene Unity has just loaded during Play.</param>
        /// <param name="mode">Native scene-loading mode.</param>
        private static void SpawnPlayer(Scene scene, LoadSceneMode mode)
        {
            // An ordinary Play session or a scene loaded by gameplay never matches this owned path.
            if (!active || !Application.isPlaying || scene.path != scenePath)
                return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Only the temporary environment's other players and views are deactivated.
                foreach (PlayerHost host in root.GetComponentsInChildren<PlayerHost>(true))
                    host.gameObject.SetActive(false);
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.GetDirectoryName(scenePath) + "/Player.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("Quick Play could not load its prepared player prefab.");
                Stop();
                return;
            }
            // This Editor-only test uses an already configured prefab; no production initialization path changes.
            GameObject player = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(player, scene);
        }

        /// <summary>Restores the user's Play setting and removes only the recorded test directory.</summary>
        private static void Recover()
        {
            if (!IsActive || preparing || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            try
            {
                RunRecord record = ReadRecord();
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(record.PreviousScene)
                    ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(record.PreviousScene);
                PlayModeWindow.SetPlayModeFocused(record.PreviousFocused);
                if (AssetDatabase.IsValidFolder(record.Folder) && !AssetDatabase.DeleteAsset(record.Folder))
                    throw new IOException("Unity could not remove the completed Quick Play assets.");
                File.Delete(manifestPath);
                active = false;
                scenePath = string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player Studio Quick Play cleanup: " + exception.Message);
            }
        }

        /// <summary>Validates the durable ownership boundary before locating or deleting test assets.</summary>
        /// <returns>The record whose folder belongs to this tool's temporary session directory.</returns>
        private static RunRecord ReadRecord()
        {
            // A malformed or manually edited record cannot redirect cleanup to another asset directory.
            RunRecord record = JsonUtility.FromJson<RunRecord>(File.ReadAllText(manifestPath));
            if (record == null || string.IsNullOrEmpty(record.Folder)
                || !record.Folder.StartsWith(sessions + "/", StringComparison.Ordinal)
                || !Guid.TryParseExact(record.Folder.Substring(sessions.Length + 1), "N", out _))
                throw new IOException("The Quick Play ownership record is invalid; no assets were deleted.");
            return record;
        }

        #endregion

        #endregion
    }
}
