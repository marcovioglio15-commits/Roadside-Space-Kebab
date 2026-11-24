using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Keeps edited prefab flag fields synchronized with an open native prefab stage.</summary>
    internal static class ObjectFlagPrefabStage
    {
        #region Methods
        #region Resolution

        /// <summary>Finds the exact stage component without falling back to a similarly named hierarchy branch.</summary>
        /// <param name="source">Persistent component owning the scanned selection.</param>
        /// <param name="path">Containing prefab path.</param>
        /// <returns>The stage counterpart, or null when that prefab is closed.</returns>
        internal static Component Resolve(UnityEngine.Object source, string path)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (source is not Component || stage == null || stage.assetPath != path)
                return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out long fileId))
                foreach (Component candidate in stage.prefabContentsRoot.GetComponentsInChildren<Component>(true))
                {
                    if (candidate == null || candidate.GetType() != source.GetType())
                        continue;
                    GlobalObjectId identity = GlobalObjectId.GetGlobalObjectIdSlow(candidate);
                    if (unchecked((long)(identity.targetObjectId ^ identity.targetPrefabId)) == fileId)
                        return candidate;
                }
            throw new InvalidOperationException("The flag owner was removed or replaced in the open prefab stage: " + path);
        }

        #endregion
        #region Synchronization

        /// <summary>Rejects conflicting native stage edits before any file is written.</summary>
        /// <param name="field">Scanned selection being committed.</param>
        /// <param name="source">Current persistent field.</param>
        internal static void Validate(ObjectFlagField field, SerializedProperty source)
        {
            Component owner = Resolve(source.serializedObject.targetObject, field.AssetPath);
            if (owner == null)
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(field.PropertyPath);
            if (property == null || property.contentHash != source.contentHash)
                throw new InvalidOperationException("This flag selection has pending changes in Prefab Mode: " + field.Location);
        }

        /// <summary>Copies only the committed flag field into its stage counterpart, preserving other pending fields.</summary>
        /// <param name="field">Selection just written to the persistent prefab.</param>
        /// <param name="source">Persistent owner after the edit.</param>
        /// <param name="values">Committed flag membership.</param>
        internal static void Write(ObjectFlagField field, UnityEngine.Object source, ObjectFlag[] values)
        {
            Component owner = Resolve(source, field.AssetPath);
            if (owner == null)
                return;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(owner.gameObject);
            bool dirty = stage.scene.isDirty;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(field.PropertyPath);
            if (property == null)
                throw new InvalidOperationException("The stage flag field no longer exists: " + field.Location);
            ObjectFlagStorage.Write(property, field.Portable, values);
            if (data.ApplyModifiedPropertiesWithoutUndo())
                if (dirty)
                    EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
                else
                    stage.ClearDirtiness();
        }

        #endregion
        #endregion
    }
}
