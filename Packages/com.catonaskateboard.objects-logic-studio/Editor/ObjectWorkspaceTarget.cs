using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Identifies the selected prefab branch or saved scene object across workspace and Editor restarts.</summary>
    [Serializable]
    internal sealed class ObjectWorkspaceTarget
    {
        #region Fields

        [Header("Identity")]
        [Tooltip("Stable source prefab GUID; empty when the selected object belongs to a gameplay scene.")]
        public string PrefabGuid = string.Empty;
        [Tooltip("Sibling-index route from the prefab root to the selected object.")]
        public string ObjectPath = string.Empty;
        [Tooltip("Global identity of a selected scene object.")]
        public string SceneObject = string.Empty;
        [Tooltip("Saved scene GUID used to reopen the same scene additively on workspace recovery.")]
        public string SceneGuid = string.Empty;
        [Tooltip("Selected hover component index on the chosen object.")]
        public int InteractionIndex;
        [Tooltip("Name captured to reject an unrelated branch after hierarchy changes.")]
        public string ObjectName = string.Empty;

        #endregion

        #region State

        [NonSerialized]
        private GameObject sceneObject;

        #endregion

        #region Methods

        #region Selection

        /// <summary>Records one target independently of the native prefab editing stage.</summary>
        /// <param name="target">Selected prefab-stage, persistent prefab or scene object.</param>
        /// <param name="index">Selected hover component index.</param>
        internal void Capture(GameObject target, int index)
        {
            // Clear the previous route before deciding which identity system owns this object.
            PrefabGuid = SceneObject = SceneGuid = ObjectPath = string.Empty;
            sceneObject = null;
            InteractionIndex = index;
            ObjectName = target != null ? target.name : string.Empty;
            if (target == null)
                return;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            if (stage != null || EditorUtility.IsPersistent(target))
            {
                GameObject root = stage != null ? stage.prefabContentsRoot : target.transform.root.gameObject;
                PrefabGuid = AssetDatabase.AssetPathToGUID(stage != null ? stage.assetPath : AssetDatabase.GetAssetPath(root));
                ObjectPath = HoverHierarchy.Path(root.transform, target.transform);
                return;
            }
            SceneObject = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            SceneGuid = AssetDatabase.AssetPathToGUID(target.scene.path);
            sceneObject = target;
        }

        /// <summary>Finds the same object in the active stage, prefab asset or loaded scene.</summary>
        /// <param name="loadScene">Whether recovery may reopen the referenced saved scene additively.</param>
        /// <returns>The same object, or null when its recorded branch is unavailable.</returns>
        internal GameObject Resolve(bool loadScene = false)
        {
            // Asset references resolve through GUIDs, preserving selection after file moves.
            if (PrefabGuid.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(PrefabGuid);
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                GameObject root = stage != null && stage.assetPath == path ? stage.prefabContentsRoot
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Transform branch = root != null ? HoverHierarchy.Resolve(root.transform, ObjectPath) : null;
                return branch != null && branch.name == ObjectName ? branch.gameObject : null;
            }

            // Saved scenes can be restored without replacing or saving the user's other loaded scenes.
            string scenePath = AssetDatabase.GUIDToAssetPath(SceneGuid);
            if (loadScene && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath).isLoaded)
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject resolved = GlobalObjectId.TryParse(SceneObject, out GlobalObjectId identity)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity) as GameObject : null;
            if (resolved != null)
                sceneObject = resolved;
            else if (sceneObject != null && SceneGuid.Length > 0
                && GlobalObjectId.GetGlobalObjectIdSlow(sceneObject).ToString() != SceneObject)
                sceneObject = null;
            return sceneObject;
        }

        /// <summary>Updates the durable identity after a previously unsaved scene receives its asset path.</summary>
        internal void RefreshIdentity()
        {
            // An unsaved scene remains editable in this process; scene Save makes its reference durable.
            if (PrefabGuid.Length == 0 && Resolve() != null)
                Capture(sceneObject, InteractionIndex);
        }

        #endregion

        #endregion
    }
}
