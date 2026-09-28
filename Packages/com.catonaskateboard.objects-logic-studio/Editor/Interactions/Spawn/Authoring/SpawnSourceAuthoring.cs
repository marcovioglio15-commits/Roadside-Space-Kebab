using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares stable cross-prefab completion links inside the workspace's existing Undo transaction.</summary>
    internal sealed class SpawnSourceAuthoring
    {
        #region State

        private readonly HashSet<GameObject> assets = new HashSet<GameObject>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks source assets before any prefab or preset is written.</summary>
        /// <param name="settings">Proposed spawn settings.</param>
        /// <param name="warning">Receives a missing stable identity or unwritable source.</param>
        /// <returns>True when source bindings can be saved as part of Apply.</returns>
        internal static bool Validate(SpawnManagementSettings settings, out string warning)
        {
            // Source references always point to saved prefab components, never temporary stage objects.
            if (!settings.TryValidate(out warning))
                return false;
            if (!AssetDatabase.IsOpenForEdit(settings.SourcePrefab))
                warning = "The source prefab is not writable; its completion links cannot be prepared.";
            foreach (SpawnCondition condition in settings.Conditions)
                if (!EditorUtility.IsPersistent(condition.Source) || ObjectWorkspaceTarget.FileId(condition.Source) == 0)
                    warning = "Save the source prefab and select its existing interaction again.";
            return warning.Length == 0;
        }

        /// <summary>Copies settings with deterministic identities while preserving all selected prefab references.</summary>
        /// <param name="settings">Detached source settings.</param>
        /// <returns>A separate snapshot containing the identities to write to the rule and source prefab.</returns>
        internal static SpawnManagementSettings Resolve(SpawnManagementSettings settings)
        {
            // Identities are metadata, not editable names; renaming a source interaction cannot break its link.
            SpawnManagementSettings result = ObjectWorkspace.Copy(settings);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(result.SourcePrefab));
            foreach (SpawnCondition condition in result.Conditions)
                condition.SourceId = guid + ":" + ObjectWorkspaceTarget.FileId(condition.Source);
            return result;
        }

        #endregion

        #region Transaction

        /// <summary>Adds missing source bindings with Undo and tracks external assets requiring a save.</summary>
        /// <param name="settings">Applied rule containing resolved prefab component identities.</param>
        internal void Prepare(SpawnManagementSettings settings)
        {
            // An already open source stage receives edits directly so its later save cannot erase this binding.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (SpawnCondition condition in settings.Conditions)
            {
                ObjectInteraction source = condition.Source;
                GameObject root = settings.SourcePrefab;
                bool staged = stage != null && stage.assetPath == AssetDatabase.GetAssetPath(root);
                if (staged)
                {
                    root = stage.prefabContentsRoot;
                    long identity = ObjectWorkspaceTarget.FileId(source);
                    source = null;
                    foreach (ObjectInteraction candidate in root.GetComponentsInChildren<ObjectInteraction>(true))
                        if (ObjectWorkspaceTarget.FileId(candidate) == identity)
                        {
                            source = candidate;
                            break;
                        }
                    if (source == null)
                        throw new System.InvalidOperationException("The selected source interaction was removed from the open prefab. Save it and select an existing source before Apply.");
                }
                using SerializedObject data = new SerializedObject(source);
                SerializedProperty bindings = data.FindProperty("completionBindings");
                SerializedProperty binding = null;
                for (int index = 0; index < bindings.arraySize; index++)
                    if (bindings.GetArrayElementAtIndex(index).FindPropertyRelative("Identity").stringValue == condition.SourceId)
                    {
                        binding = bindings.GetArrayElementAtIndex(index);
                        break;
                    }
                if (binding != null && binding.FindPropertyRelative("Root").objectReferenceValue == root.transform)
                    continue;
                if (binding == null)
                {
                    bindings.arraySize++;
                    binding = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
                }
                binding.FindPropertyRelative("Identity").stringValue = condition.SourceId;
                binding.FindPropertyRelative("Root").objectReferenceValue = root.transform;
                data.ApplyModifiedProperties();
                EditorUtility.SetDirty(source);
                if (!staged)
                    assets.Add(root);
            }
        }

        /// <summary>Persists touched external source prefabs after commit or after Undo has restored a failed transaction.</summary>
        internal void Save()
        {
            // The main workspace saves its own open prefab; only external source assets are handled here.
            foreach (GameObject asset in assets)
                PrefabUtility.SavePrefabAsset(asset);
        }

        #endregion

        #endregion
    }
}
