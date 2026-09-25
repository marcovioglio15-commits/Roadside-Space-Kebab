using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Connects observer references inside one prefab without mixing asset and scene instances.</summary>
    internal static class ObjectObserverAssets
    {
        #region Methods

        #region Persistence

        /// <summary>Updates a selected player prefab using its open stage or an isolated prefab-content scope.</summary>
        /// <param name="camera">Camera component in the selected prefab asset.</param>
        /// <param name="player">Tagged player root in that same asset.</param>
        /// <param name="tag">Project tag assigned to the player.</param>
        /// <param name="existing">Optional existing observer in the same asset.</param>
        /// <param name="createHud">Prepare the observer's one shared dialogue overlay when requested.</param>
        /// <returns>The saved observer component in the persistent prefab asset.</returns>
        internal static HoverObserver Apply(Camera camera, GameObject player, string tag, HoverObserver existing, bool createHud = false)
        {
            // An already open stage remains authoritative, including its unsaved collider and hierarchy edits.
            string path = AssetDatabase.GetAssetPath(player);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool opened = stage != null && stage.assetPath == path;
            GameObject root = opened ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            try
            {
                Camera view = Resolve<Camera>(root, camera);
                GameObject target = Resolve<GameObject>(root, player);
                HoverObserver observer = existing != null ? Resolve<HoverObserver>(root, existing) : null;
                if (view == null || target == null || existing != null && observer == null)
                    throw new InvalidOperationException("The observer prefab hierarchy changed. Discard and select its current camera and player.");
                if (opened)
                    observer = HoverAuthoring.SetupObserver(view, target, tag, observer);
                else
                {
                    // Isolated contents are temporary, so no Undo entry may retain references to their destroyed scene.
                    target.tag = tag;
                    if (observer == null)
                        observer = view.GetComponent<HoverObserver>();
                    if (observer == null)
                        observer = view.gameObject.AddComponent<HoverObserver>();
                    using (SerializedObject data = new SerializedObject(observer))
                    {
                        data.FindProperty("view").objectReferenceValue = view;
                        data.FindProperty("playerTag").stringValue = tag;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                if (createHud)
                    DialogueAuthoring.CreateHud(observer, opened);
                if (opened)
                    ObjectAuthoringSave.Save(root);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved)
                        throw new InvalidOperationException("The observer prefab could not be saved.");
                }
                // Reconnect by stable camera identity, which also disambiguates duplicate child names.
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Camera savedView = Resolve<Camera>(asset, camera);
                if (existing != null)
                    return Resolve<HoverObserver>(asset, existing);
                return savedView != null ? savedView.GetComponent<HoverObserver>() : null;
            }
            finally
            {
                if (!opened)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Maps an asset reference to the same component or object inside loaded prefab contents.</summary>
        /// <typeparam name="T">Expected Unity reference type.</typeparam>
        /// <param name="root">Loaded prefab root.</param>
        /// <param name="source">Persistent asset object.</param>
        /// <returns>The matching local reference, or null when removed.</returns>
        private static T Resolve<T>(GameObject root, UnityEngine.Object source) where T : UnityEngine.Object
        {
            // Stable local IDs also distinguish components and children with identical names.
            long identity = ObjectWorkspaceTarget.FileId(source);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (typeof(T) == typeof(GameObject) && ObjectWorkspaceTarget.FileId(child.gameObject) == identity)
                    return child.gameObject as T;
                foreach (Component component in child.GetComponents<Component>())
                    if (component is T result && ObjectWorkspaceTarget.FileId(result) == identity)
                        return result;
            }
            return null;
        }

        #endregion

        #endregion
    }
}
