using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares prefab-stage, prefab-asset and ordinary scene save behaviour across interaction types.</summary>
    internal static class ObjectAuthoringSave
    {
        #region Methods

        #region Persistence

        /// <summary>Saves a prefab immediately or marks its gameplay scene for an explicit scene Save.</summary>
        /// <param name="target">Object whose applied interaction settings changed.</param>
        internal static void Save(GameObject target)
        {
            // A preview stage writes through its own root; scene instances retain ordinary overrides.
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            if (stage != null)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("The prefab stage could not be saved.");
            }
            else if (EditorUtility.IsPersistent(target))
                PrefabUtility.SavePrefabAsset(target.transform.root.gameObject);
            else
                EditorSceneManager.MarkSceneDirty(target.scene);
        }

        #endregion

        #endregion
    }
}
