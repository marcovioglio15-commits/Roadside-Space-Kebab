using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains prefab-only selection across stage reopening, reimports and Editor restarts.</summary>
    [Serializable]
    internal sealed class ObjectWorkspaceTarget
    {
        #region Fields

        [Header("Identity")]
        [Tooltip("Stable GUID of the prefab asset containing the selected branch.")]
        public string PrefabGuid = string.Empty;
        [Tooltip("Sibling-index route from the prefab root to the selected object.")]
        public string ObjectPath = string.Empty;
        [Tooltip("Serialized object ID used to recover the same branch after hierarchy reordering.")]
        public long ObjectFileId;
        [Tooltip("Hierarchy signature protecting a new unsaved branch from ambiguous route changes.")]
        public string Hierarchy = string.Empty;
        [Tooltip("Selected hover component index on the chosen object.")]
        public int InteractionIndex;
        [Tooltip("Name captured to reject an unrelated branch after hierarchy changes.")]
        public string ObjectName = string.Empty;

        #endregion

        #region Properties

        /// <summary>Whether the selected asset is the prefab currently open in the native workspace.</summary>
        internal bool IsOpen => PrefabStageUtility.GetCurrentPrefabStage() is PrefabStage stage
            && stage.assetPath == AssetDatabase.GUIDToAssetPath(PrefabGuid);

        #endregion

        #region Methods

        #region Selection

        /// <summary>Records one target independently of the native prefab editing stage.</summary>
        /// <param name="target">Validated prefab-stage or asset object; null clears selection.</param>
        /// <param name="index">Selected hover component index.</param>
        internal void Capture(GameObject target, int index)
        {
            // Gameplay scene objects cannot become interaction authoring targets.
            PrefabGuid = ObjectPath = Hierarchy = string.Empty;
            ObjectFileId = 0;
            InteractionIndex = index;
            ObjectName = target != null ? target.name : string.Empty;
            if (target == null || !ObjectAuthoringSave.TryValidate(target, out _))
                return;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            GameObject root = stage != null ? stage.prefabContentsRoot : target.transform.root.gameObject;
            PrefabGuid = AssetDatabase.AssetPathToGUID(stage != null ? stage.assetPath : AssetDatabase.GetAssetPath(root));
            ObjectPath = HoverHierarchy.Path(root.transform, target.transform);
            Hierarchy = HoverHierarchy.Signature(root.transform);
            ObjectFileId = FileId(target);
        }

        /// <summary>Resolves the selected prefab branch without opening or modifying gameplay scenes.</summary>
        /// <returns>The same object, or null when its recorded branch is unavailable.</returns>
        internal GameObject Resolve()
        {
            // Asset references resolve through GUIDs, preserving selection after file moves.
            if (PrefabGuid.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(PrefabGuid);
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                GameObject root = stage != null && stage.assetPath == path ? stage.prefabContentsRoot
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    return null;
                if (ObjectFileId != 0)
                {
                    foreach (Transform branch in root.GetComponentsInChildren<Transform>(true))
                        if (FileId(branch.gameObject) == ObjectFileId)
                            return branch.gameObject;
                    return null;
                }
                Transform candidate = HoverHierarchy.Signature(root.transform) == Hierarchy
                    ? HoverHierarchy.Resolve(root.transform, ObjectPath) : null;
                return candidate != null && candidate.name == ObjectName ? candidate.gameObject : null;
            }

            return null;
        }

        /// <summary>Promotes a newly saved branch to its serialized prefab identity.</summary>
        internal void RefreshIdentity()
        {
            // A missing saved ID never redirects to another similarly named object.
            if (ObjectFileId == 0 && Resolve() is GameObject target)
                ObjectFileId = FileId(target);
        }

        /// <summary>Reads local prefab identity in either persistent asset or native stage form.</summary>
        /// <param name="target">Object belonging to the selected prefab.</param>
        /// <returns>The saved local file ID, or zero for a new unsaved object.</returns>
        private static long FileId(GameObject target)
        {
            // Stage instances expose source and instance IDs separately; combine them to match the asset-local ID.
            if (EditorUtility.IsPersistent(target))
                return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string _, out long identifier) ? identifier : 0;
            GlobalObjectId identity = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return unchecked((long)(identity.targetObjectId ^ identity.targetPrefabId));
        }

        #endregion

        #endregion
    }
}
