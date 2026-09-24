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
        [NonSerialized]
        private GameObject cameraPrefab;
        [NonSerialized]
        private Camera[] cameraChoices = Array.Empty<Camera>();
        [NonSerialized]
        private GUIContent[] cameraNames = Array.Empty<GUIContent>();

        #endregion

        #region Properties

        /// <summary>Whether scene setup needs the common Apply action.</summary>
        internal bool HasChanges => CameraId != OriginalCamera || PlayerId != OriginalPlayer || PlayerTag != OriginalTag;

        #endregion

        #region Methods

        #region Recovery

        /// <summary>Restores references and discovers an existing observer without resetting an unfinished proposal.</summary>
        internal void Refresh()
        {
            cameraPrefab = null;
            // Global IDs recover saved scene objects; cached references also support unsaved objects in this Editor session.
            observer = ObserverId.Length > 0 ? Recover(ObserverId, observer) : null;
            camera = CameraId.Length > 0 ? Recover(CameraId, camera) : null;
            player = PlayerId.Length > 0 ? Recover(PlayerId, player) : null;
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
            if (player == null && !EditorUtility.IsPersistent(observer) && Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, PlayerTag) >= 0)
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

        /// <summary>Recovers an ID without allowing a destroyed Unity wrapper to hide the live cached reference.</summary>
        /// <typeparam name="T">Expected Unity reference type.</typeparam>
        /// <param name="identity">Saved scene or asset identity.</param>
        /// <param name="cached">Reference retained for an unsaved object.</param>
        /// <returns>The resolved object or a still-live cached object.</returns>
        private static T Recover<T>(string identity, T cached) where T : UnityEngine.Object
        {
            // Unity's destroyed-object semantics require an explicit null check.
            T resolved = Resolve<T>(identity);
            return resolved != null ? resolved : cached != null ? cached : null;
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
            // A prefab picker makes asset-contained cameras accessible without a scene instance.
            DrawPrefab(owner);
            // Selecting an existing observer is navigation; a pending proposal must be resolved first.
            using (new EditorGUI.DisabledScope(HasChanges))
            {
                HoverObserver selected = (HoverObserver)EditorGUILayout.ObjectField(new GUIContent("Observer", "Existing scene or prefab component that supplies camera and player context."), observer, typeof(HoverObserver), true);
                if (selected != observer && (selected == null || EditorUtility.IsPersistent(selected) || !EditorSceneManager.IsPreviewScene(selected.gameObject.scene)))
                {
                    observer = selected;
                    ObserverId = Capture(observer);
                    if (observer != null)
                        ReadApplied();
                }
            }
            EditorGUI.BeginChangeCheck();
            Camera view = (Camera)EditorGUILayout.ObjectField(new GUIContent("Camera", "Gameplay camera in the same scene or prefab as the player; no MainCamera tag is required."), camera, typeof(Camera), true);
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

        /// <summary>Selects a player prefab and exposes every camera contained in that asset.</summary>
        /// <param name="owner">Workspace retaining the selected asset and proposed connection.</param>
        private void DrawPrefab(ObjectWorkspace owner)
        {
            // Selection remains independent of the interaction prefab edited in the other tabs.
            GameObject current = player != null && EditorUtility.IsPersistent(player)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(player)) : null;
            using (new EditorGUI.DisabledScope(HasChanges))
            {
                GameObject selected = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Player Prefab",
                    "Optional player prefab to configure directly. Leave empty to configure loaded scene objects."), current, typeof(GameObject), false);
                if (selected != current && (selected == null || ObjectAuthoringSave.TryValidate(selected, out _)))
                {
                    observer = selected != null ? selected.GetComponentInChildren<HoverObserver>(true) : null;
                    ObserverId = Capture(observer);
                    camera = null;
                    player = selected;
                    CameraId = OriginalCamera = OriginalPlayer = string.Empty;
                    PlayerId = Capture(player);
                    PlayerTag = OriginalTag = "Player";
                    ExistingPlayerTag = player != null ? player.tag : string.Empty;
                    if (observer != null)
                        ReadApplied();
                    else if (selected != null)
                    {
                        Camera[] cameras = selected.GetComponentsInChildren<Camera>(true);
                        if (cameras.Length == 1)
                        {
                            camera = cameras[0];
                            CameraId = Capture(camera);
                        }
                    }
                    current = selected;
                    owner.Persist();
                }
            }
            if (current == null)
                return;
            if (cameraPrefab != current)
            {
                cameraPrefab = current;
                cameraChoices = current.GetComponentsInChildren<Camera>(true);
                cameraNames = new GUIContent[cameraChoices.Length + 1];
                cameraNames[0] = new GUIContent("Select camera", "Choose an existing camera in this player prefab.");
                for (int index = 0; index < cameraChoices.Length; index++)
                    cameraNames[index + 1] = new GUIContent(AnimationUtility.CalculateTransformPath(cameraChoices[index].transform, current.transform),
                        "Camera inside the selected player prefab.");
            }
            EditorGUI.BeginChangeCheck();
            int requested = EditorGUILayout.Popup(new GUIContent("Prefab Camera", "Existing cameras in the selected asset."), Array.IndexOf(cameraChoices, camera) + 1, cameraNames);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Select observer prefab camera");
                camera = requested > 0 ? cameraChoices[requested - 1] : null;
                CameraId = Capture(camera);
                owner.Persist();
            }
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
            if (camera == null || player == null
                || !EditorUtility.IsPersistent(camera) && EditorSceneManager.IsPreviewScene(camera.gameObject.scene)
                || !EditorUtility.IsPersistent(player) && EditorSceneManager.IsPreviewScene(player.scene)
                || PlayerTag == "Untagged" || Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, PlayerTag) < 0)
                warning = "Choose a loaded gameplay camera, player root and a defined player tag.";
            else if (EditorUtility.IsPersistent(camera) != EditorUtility.IsPersistent(player)
                || EditorUtility.IsPersistent(player) && (AssetDatabase.GetAssetPath(camera) != AssetDatabase.GetAssetPath(player)
                    || observer != null && AssetDatabase.GetAssetPath(observer) != AssetDatabase.GetAssetPath(player)
                    || !ObjectAuthoringSave.TryValidate(player, out warning)))
                warning = "Choose camera, player and observer from the same writable prefab, or all from the same loaded scene.";
            else if (!EditorUtility.IsPersistent(player) && (camera.gameObject.scene != player.scene
                || observer != null && observer.gameObject.scene != camera.gameObject.scene))
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
            if (EditorUtility.IsPersistent(player))
                observer = ObjectObserverAssets.Apply(camera, player, PlayerTag, observer);
            else
            {
                observer = HoverAuthoring.SetupObserver(camera, player, PlayerTag, observer);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
                EditorSceneManager.MarkSceneDirty(player.scene);
            }
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
