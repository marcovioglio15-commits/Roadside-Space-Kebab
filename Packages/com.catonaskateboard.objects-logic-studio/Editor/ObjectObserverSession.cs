using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Recovers the scene's real observer and retains any pending camera/player setup separately.</summary>
    [Serializable]
    internal sealed class ObjectObserverSession
    {
        #region Fields

        [Header("Scene Observer")]
        [Tooltip("Stable identity of the observer selected in the loaded gameplay scene.")]
        public string ObserverId = string.Empty;
        [Tooltip("Stable identity of the proposed gameplay camera.")]
        public string CameraId = string.Empty;
        [Tooltip("Stable identity of the proposed tagged player root.")]
        public string PlayerId = string.Empty;
        [Tooltip("Player tag proposed for the observer and selected player root.")]
        public string PlayerTag = "Player";
        [Tooltip("Applied camera identity captured when the scene setup was opened.")]
        public string OriginalCamera = string.Empty;
        [Tooltip("Applied player identity captured when the scene setup was opened.")]
        public string OriginalPlayer = string.Empty;
        [Tooltip("Applied player tag captured when the scene setup was opened.")]
        public string OriginalTag = "Player";
        [Tooltip("Existing player tag before the proposed setup is committed.")]
        public string ExistingPlayerTag = string.Empty;

        #endregion

        #region State

        [NonSerialized]
        private HoverObserver observer;
        [NonSerialized]
        private Camera camera;
        [NonSerialized]
        private GameObject player;

        #endregion

        #region Properties

        /// <summary>Whether scene setup needs the common Apply action.</summary>
        internal bool HasChanges => CameraId != OriginalCamera || PlayerId != OriginalPlayer || PlayerTag != OriginalTag;

        #endregion

        #region Methods

        #region Recovery

        /// <summary>Reopens saved scene references when restoring the tool after an Editor restart.</summary>
        internal void RestoreScenes()
        {
            // Only scenes explicitly referenced by the retained connection are eligible for recovery.
            foreach (string value in new[] { ObserverId, CameraId, PlayerId })
                if (GlobalObjectId.TryParse(value, out GlobalObjectId identity))
                {
                    string path = AssetDatabase.GUIDToAssetPath(identity.assetGUID.ToString());
                    if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        && !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path).isLoaded)
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
        }

        /// <summary>Restores references and discovers an existing observer without resetting an unfinished proposal.</summary>
        internal void Refresh()
        {
            // Global IDs recover saved scene objects; cached references also support unsaved objects in this Editor session.
            observer = ObserverId.Length > 0 ? Resolve<HoverObserver>(ObserverId) ?? observer : null;
            camera = CameraId.Length > 0 ? Resolve<Camera>(CameraId) ?? camera : null;
            player = PlayerId.Length > 0 ? Resolve<GameObject>(PlayerId) ?? player : null;
            if (HasChanges)
                return;
            if (observer == null && ObserverId.Length > 0)
                return;
            if (observer == null)
            {
                // Automatic discovery is safe only when the gameplay scene has one unambiguous observer.
                HoverObserver found = null;
                foreach (HoverObserver candidate in UnityEngine.Object.FindObjectsByType<HoverObserver>(FindObjectsInactive.Include))
                    if (!EditorUtility.IsPersistent(candidate) && !EditorSceneManager.IsPreviewScene(candidate.gameObject.scene))
                    {
                        if (found != null)
                            return;
                        found = candidate;
                    }
                observer = found;
            }
            if (observer != null)
                ReadApplied();
        }

        /// <summary>Reads the actual scene component and resolves its tagged player for presentation.</summary>
        private void ReadApplied()
        {
            // The tool shows the saved connection rather than an empty setup form on each reopening.
            ObserverId = Capture(observer);
            camera = observer.ConfiguredView;
            PlayerTag = observer.PlayerTag;
            player = null;
            if (camera != null)
                for (Transform parent = camera.transform; parent != null; parent = parent.parent)
                    if (parent.tag == PlayerTag)
                    {
                        player = parent.gameObject;
                        break;
                    }
            if (player == null && Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, PlayerTag) >= 0)
            {
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(PlayerTag);
                if (candidates.Length == 1)
                    player = candidates[0];
            }
            CameraId = OriginalCamera = Capture(camera);
            PlayerId = OriginalPlayer = Capture(player);
            OriginalTag = PlayerTag;
            ExistingPlayerTag = player != null ? player.tag : string.Empty;
        }

        /// <summary>Resolves a stored Unity identity without selecting similar objects by name.</summary>
        /// <typeparam name="T">Expected Unity object type.</typeparam>
        /// <param name="identity">Previously captured global identity.</param>
        /// <returns>The same loaded object or null.</returns>
        private static T Resolve<T>(string identity) where T : UnityEngine.Object
        {
            // Scene discovery remains limited to explicit refresh boundaries.
            return GlobalObjectId.TryParse(identity, out GlobalObjectId parsed)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T : null;
        }

        /// <summary>Captures stable identity text for a camera, player or observer.</summary>
        /// <param name="value">Selected scene reference.</param>
        /// <returns>A global ID or an empty string for no selection.</returns>
        private static string Capture(UnityEngine.Object value)
        {
            // Saved scene GUIDs survive an Editor restart.
            return value != null ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : string.Empty;
        }

        #endregion

        #region Controls

        /// <summary>Edits a scene connection in the workspace without modifying scene objects yet.</summary>
        /// <param name="owner">Workspace receiving Undo records.</param>
        internal void Draw(ObjectWorkspace owner)
        {
            // Selecting an existing observer is navigation; a pending proposal must be resolved first.
            using (new EditorGUI.DisabledScope(HasChanges))
            {
                HoverObserver selected = (HoverObserver)EditorGUILayout.ObjectField(new GUIContent("Observer", "Existing scene component that supplies the camera and player to all object hovers."), observer, typeof(HoverObserver), true);
                if (selected != observer && (selected == null || !EditorUtility.IsPersistent(selected)
                    && !EditorSceneManager.IsPreviewScene(selected.gameObject.scene)))
                {
                    observer = selected;
                    ObserverId = Capture(observer);
                    if (observer != null)
                        ReadApplied();
                }
            }
            EditorGUI.BeginChangeCheck();
            Camera view = (Camera)EditorGUILayout.ObjectField(new GUIContent("Camera", "Existing gameplay camera; Apply connects it without requiring the MainCamera tag."), camera, typeof(Camera), true);
            GameObject root = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Player Root", "Existing player root; Apply assigns the chosen tag with Undo support."), player, typeof(GameObject), true);
            string tag = EditorGUILayout.TagField(new GUIContent("Player Tag", "Tag used to identify the player during gameplay."), PlayerTag);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Edit hover observer setup");
                camera = view;
                if (player != root)
                    ExistingPlayerTag = root != null ? root.tag : string.Empty;
                player = root;
                CameraId = Capture(camera);
                PlayerId = Capture(player);
                PlayerTag = tag;
                owner.Persist();
            }
            EditorGUILayout.LabelField(observer != null ? "Connected · changes are committed with Apply." : "Choose a camera and player, then Apply once.", EditorStyles.miniLabel);
        }

        #endregion

        #region Confirmation

        /// <summary>Checks pending references and outside changes before any preset or scene write.</summary>
        /// <param name="warning">Receives the first blocking setup issue.</param>
        /// <returns>True when no setup is pending or all proposed references are valid.</returns>
        internal bool TryValidate(out string warning)
        {
            // Reject prefab assets and preview scenes before attempting to add an observer.
            warning = string.Empty;
            if (!HasChanges)
                return true;
            if (camera == null || player == null || EditorUtility.IsPersistent(camera) || EditorUtility.IsPersistent(player)
                || EditorSceneManager.IsPreviewScene(camera.gameObject.scene) || EditorSceneManager.IsPreviewScene(player.scene)
                || PlayerTag == "Untagged" || Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, PlayerTag) < 0)
                warning = "Choose a loaded gameplay camera, player root and a defined player tag.";
            else if (camera.gameObject.scene != player.scene || observer != null && observer.gameObject.scene != camera.gameObject.scene)
                warning = "Choose an observer, camera and player in the same gameplay scene so their references can be saved.";
            else if (ObserverId.Length > 0 && observer == null)
                warning = "The original scene observer is unavailable. Restore its scene or Discard before reconnecting.";
            else if (observer != null && (Capture(observer.ConfiguredView) != OriginalCamera || observer.PlayerTag != OriginalTag)
                || player.tag != ExistingPlayerTag)
                warning = "Scene observer or player tag changed elsewhere. Discard to reload it.";
            return warning.Length == 0;
        }

        /// <summary>Commits a validated scene connection inside the workspace's Undo transaction.</summary>
        internal void Apply()
        {
            // Reuse the same authored component instead of requiring setup whenever the tool reopens.
            if (!HasChanges)
                return;
            observer = HoverAuthoring.SetupObserver(camera, player, PlayerTag, observer);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.MarkSceneDirty(player.scene);
            ReadApplied();
        }

        /// <summary>Reloads the applied scene setup without touching its objects.</summary>
        internal void Discard()
        {
            // Existing observers remain authoritative; an unfinished first setup returns to its original selection.
            CameraId = OriginalCamera;
            PlayerId = OriginalPlayer;
            PlayerTag = OriginalTag;
            camera = Resolve<Camera>(CameraId);
            player = Resolve<GameObject>(PlayerId);
            Refresh();
        }

        #endregion

        #endregion
    }
}
