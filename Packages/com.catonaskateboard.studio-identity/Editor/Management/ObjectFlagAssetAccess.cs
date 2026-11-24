using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Visits persistent assets and scene components without replacing the current scene setup.</summary>
    internal static class ObjectFlagAssetAccess
    {
        #region Methods
        #region Loading

        /// <summary>Inspects one asset and saves only explicitly modified owners.</summary>
        /// <param name="path">Project asset or scene path.</param>
        /// <param name="visit">Returns whether an owner was changed.</param>
        /// <param name="write">Allows persistent writes.</param>
        internal static void Visit(string path, Func<UnityEngine.Object, bool> visit, bool write = false)
        {
            // Preview scenes are closed in finally, including failed scans.
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened)
                    scene = write ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    bool changed = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        changed |= VisitHierarchy(root, visit);
                    if (write && changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (opened && !EditorSceneManager.SaveScene(scene))
                            throw new InvalidOperationException("Could not save scene: " + path);
                    }
                }
                finally
                {
                    if (opened)
                        if (write)
                            EditorSceneManager.CloseScene(scene, true);
                        else
                            EditorSceneManager.ClosePreviewScene(scene);
                }
                return;
            }

            // Prefab components include inactive children and repeated interaction types.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                if (VisitHierarchy(prefab, visit) && write)
                {
                    PrefabUtility.SavePrefabAsset(prefab, out bool saved);
                    if (!saved)
                        throw new InvalidOperationException("Could not save prefab: " + path);
                }
                return;
            }
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && asset is not MonoScript && visit(asset) && write)
                    Save(asset);
        }

        /// <summary>Verifies that Unity actually saved a dirty asset before a proposal is discarded.</summary>
        /// <param name="asset">Persistent asset or subasset receiving changes.</param>
        internal static void Save(UnityEngine.Object asset)
        {
            AssetDatabase.SaveAssetIfDirty(asset);
            if (EditorUtility.IsDirty(asset))
                throw new System.IO.IOException("Unity could not save: " + AssetDatabase.GetAssetPath(asset));
        }

        /// <summary>Visits serialized components without including their flag definition assets.</summary>
        /// <param name="root">Loaded prefab or scene root.</param>
        /// <param name="visit">Receives each valid component.</param>
        /// <returns>True when any component changed.</returns>
        private static bool VisitHierarchy(GameObject root, Func<UnityEngine.Object, bool> visit)
        {
            bool changed = false;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null && component is not Transform)
                    changed |= visit(component);
            return changed;
        }

        /// <summary>Builds a stable identity shared by scans and later edits.</summary>
        /// <param name="owner">Loaded asset or scene component.</param>
        /// <returns>The local file and prefab-instance identifiers.</returns>
        internal static string Id(UnityEngine.Object owner)
        {
            // Nested instances can share source IDs; their prefab IDs distinguish the actual owners.
            GlobalObjectId identity = GlobalObjectId.GetGlobalObjectIdSlow(owner);
            return identity.targetObjectId != 0 ? identity.targetObjectId + ":" + identity.targetPrefabId
                : "live:" + EntityId.ToULong(owner.GetEntityId());
        }

        /// <summary>Checks both version-control access and filesystem write protection.</summary>
        /// <param name="path">Project-relative asset path.</param>
        /// <returns>True when the asset can be changed.</returns>
        internal static bool Writable(string path)
        {
            return AssetDatabase.IsOpenForEdit(path, StatusQueryOptions.ForceUpdate)
                && System.IO.File.Exists(path)
                && (System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReadOnly) == 0;
        }

        #endregion
        #endregion
    }
}
