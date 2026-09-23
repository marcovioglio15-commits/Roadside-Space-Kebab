using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Validates prefab-only authoring and saves applied edits to the containing prefab asset.</summary>
    internal static class ObjectAuthoringSave
    {
        #region Methods

        #region Persistence

        /// <summary>Checks that an object belongs to a writable prefab asset or native prefab stage.</summary>
        /// <param name="target">Proposed interaction owner.</param>
        /// <param name="warning">Receives an unsupported or read-only selection.</param>
        /// <returns>True when edits can be written directly to a prefab.</returns>
        internal static bool TryValidate(GameObject target, out string warning)
        {
            // Scene instances are excluded even when they have an editable source prefab.
            warning = "Select a writable prefab asset or an object inside its Prefab workspace.";
            if (target == null)
                return false;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            string path = stage != null ? stage.assetPath : EditorUtility.IsPersistent(target) ? AssetDatabase.GetAssetPath(target) : string.Empty;
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsOpenForEdit(path))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Saves the containing prefab immediately and reports a failed disk write.</summary>
        /// <param name="target">Object whose applied interaction settings changed.</param>
        internal static void Save(GameObject target)
        {
            // Repeat validation at the write boundary so stale menu actions cannot edit gameplay scenes.
            if (!TryValidate(target, out string warning))
                throw new InvalidOperationException(warning);
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            if (stage != null)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("The prefab stage could not be saved.");
            }
            else
            {
                PrefabUtility.SavePrefabAsset(target.transform.root.gameObject, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("The prefab asset could not be saved.");
            }
        }

        #endregion

        #endregion
    }
}
