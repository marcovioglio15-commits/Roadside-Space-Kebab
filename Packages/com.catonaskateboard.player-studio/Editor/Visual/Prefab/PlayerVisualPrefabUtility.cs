using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Applies only the managed visual to the player prefab, preserving unrelated instance overrides.</summary>
    internal static class PlayerVisualPrefabUtility
    {
        #region Methods

        #region Validation

        /// <summary>Resolves the prefab that owns the player and checks write access before any session change.</summary>
        /// <param name="host">Scene player whose visual is being confirmed.</param>
        /// <param name="path">Receives its source prefab path, or empty for an ordinary scene object.</param>
        /// <param name="warning">Receives an unsupported context or write-access warning.</param>
        /// <returns>True when the visual can be written without applying unrelated overrides.</returns>
        public static bool TryGetPath(PlayerHost host, out string path, out string warning)
        {
            // A Prefab Stage is saved by its own authoring workflow; do not write behind its open contents.
            path = string.Empty;
            warning = string.Empty;
            if (PrefabStageUtility.GetCurrentPrefabStage()?.scene == host.gameObject.scene)
            {
                warning = "Apply Visual changes from a scene instance, then reopen the player prefab to inspect them.";
                return false;
            }
            PlayerHost source = PrefabUtility.GetCorrespondingObjectFromSource(host);
            if (source == null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(host))
                    warning = "Apply the Player Host component to its player prefab before configuring the Visual.";
                return warning.Length == 0;
            }

            path = AssetDatabase.GetAssetPath(source);
            if (!path.EndsWith(".prefab") || PrefabUtility.IsPartOfImmutablePrefab(source)
                || !AssetDatabase.IsOpenForEdit(source) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                warning = "The player prefab is not writable. Choose an editable player prefab before applying its Visual.";
            else if (source.MasterPreset != host.MasterPreset)
                warning = "The scene player's Master is an override. Use the master saved in its player prefab before applying Visual changes.";
            else if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
                warning = "Close the player Prefab Stage before applying Visual changes from its scene instance.";
            return warning.Length == 0;
        }

        #endregion

        #region Application

        /// <summary>Restores a deleted prefab visual before synchronizing an otherwise intact binding.</summary>
        /// <param name="binding">Binding whose model and root are both missing.</param>
        /// <param name="path">Player prefab retaining the original visual hierarchy.</param>
        public static void RestoreMissingChild(PlayerVisualBinding binding, string path)
        {
            // A scene-only added binding has no original hierarchy to restore.
            if (binding == null || path.Length == 0 || binding.VisualRoot != null || binding.Model != null)
                return;
            PlayerVisualBinding source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(binding, path);
            if (source == null || source.VisualRoot == null || source.Model == null)
                return;

            // Revert only the removed visual, preserving every other removal and property override.
            foreach (RemovedGameObject removed in PrefabUtility.GetRemovedGameObjects(binding.gameObject))
                if (removed.assetGameObject == source.VisualRoot.gameObject)
                {
                    PrefabUtility.RevertRemovedGameObject(binding.gameObject, removed.assetGameObject, InteractionMode.UserAction);
                    break;
                }

            // Resolve by exact prefab identity; names and child order never choose the restored target.
            using SerializedObject serialized = new SerializedObject(binding);
            foreach (Transform child in binding.GetComponentsInChildren<Transform>(true))
            {
                Transform original = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, path);
                if (original == source.VisualRoot)
                    serialized.FindProperty("visualRoot").objectReferenceValue = child;
                if (original == source.Model.transform)
                    serialized.FindProperty("model").objectReferenceValue = child.gameObject;
            }
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Commits a newly created child to the chosen player prefab while preserving nested prefab links.</summary>
        /// <param name="child">Visual hierarchy created during the confirmed operation.</param>
        /// <param name="path">Player prefab destination, or empty for a scene-only player.</param>
        public static void ApplyChild(GameObject child, string path)
        {
            // Apply only this addition, never the entire player's override list.
            if (path.Length == 0 || PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, path) != null)
                return;
            // An assigned camera or anchor may sit inside a newly authored group belonging to this player.
            while (child.transform.parent != null
                && PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child.transform.parent.gameObject, path) == null)
                child = child.transform.parent.gameObject;
            PrefabUtility.ApplyAddedGameObject(child, path, InteractionMode.UserAction);
        }

        /// <summary>Removes an owned object from the instance and its player prefab in the same Undo operation.</summary>
        /// <param name="host">Surviving instance used to route the removal.</param>
        /// <param name="child">Managed hierarchy to remove.</param>
        /// <param name="path">Destination player prefab path.</param>
        public static void RemoveChild(PlayerHost host, GameObject child, string path)
        {
            // Capture the exact asset object before destroying its instance counterpart.
            GameObject source = path.Length > 0 ? PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, path) : null;
            Undo.DestroyObjectImmediate(child);
            if (source != null)
                PrefabUtility.ApplyRemovedGameObject(host.gameObject, source, InteractionMode.UserAction);
        }

        /// <summary>Removes only the visual binding, leaving all other player components unchanged.</summary>
        /// <param name="host">Player retaining its other components.</param>
        /// <param name="binding">Binding being removed.</param>
        /// <param name="path">Destination player prefab path.</param>
        public static void RemoveBinding(PlayerHost host, PlayerVisualBinding binding, string path)
        {
            // A component added only to the instance has no source removal to apply.
            PlayerVisualBinding source = path.Length > 0 ? PrefabUtility.GetCorrespondingObjectFromSourceAtPath(binding, path) : null;
            Undo.DestroyObjectImmediate(binding);
            if (source != null)
                PrefabUtility.ApplyRemovedComponent(host.gameObject, source, InteractionMode.UserAction);
        }

        /// <summary>Saves the binding and offset to the player prefab without touching the nested model asset.</summary>
        /// <param name="binding">Fully configured binding after the scene operation.</param>
        /// <param name="path">Destination player prefab path.</param>
        public static void ApplyBinding(PlayerVisualBinding binding, string path)
        {
            // Property application targets the player prefab even when the model is itself a nested prefab.
            if (path.Length == 0)
                return;
            ApplyChild(binding.VisualRoot.gameObject, path);
            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(binding, path) == null)
                PrefabUtility.ApplyAddedComponent(binding, path, InteractionMode.UserAction);
            else
                PrefabUtility.ApplyObjectOverride(binding, path, InteractionMode.UserAction);
            ApplyPose(binding.VisualRoot, path);
            if (binding.OwnsContainer)
                ApplyPose(binding.Model.transform, path);
        }

        /// <summary>Applies only local pose properties, preserving every other object override.</summary>
        /// <param name="transform">Managed visual transform with its confirmed pose.</param>
        /// <param name="path">Player prefab receiving this local pose.</param>
        private static void ApplyPose(Transform transform, string path)
        {
            // The model source keeps its authored pose; only the enclosing player stores the offset.
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            using SerializedObject serialized = new SerializedObject(transform);
            PrefabUtility.ApplyPropertyOverride(serialized.FindProperty("m_LocalPosition"), path, InteractionMode.UserAction);
            PrefabUtility.ApplyPropertyOverride(serialized.FindProperty("m_LocalRotation"), path, InteractionMode.UserAction);
            PrefabUtility.ApplyPropertyOverride(serialized.FindProperty("m_LocalScale"), path, InteractionMode.UserAction);
        }

        #endregion

        #endregion
    }
}
