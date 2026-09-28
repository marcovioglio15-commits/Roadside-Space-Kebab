using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Applies only the managed player hierarchy to the player prefab, preserving unrelated instance overrides.</summary>
    internal static class PlayerPrefabUtility
    {
        #region Methods

        #region Validation

        /// <summary>Resolves the prefab that owns the player and checks write access before any session change.</summary>
        /// <param name="host">Scene player whose player hierarchy is being confirmed.</param>
        /// <param name="path">Receives its source prefab path, or empty for an ordinary scene object.</param>
        /// <param name="warning">Receives an unsupported context or write-access warning.</param>
        /// <returns>True when the player hierarchy can be written without applying unrelated overrides.</returns>
        public static bool TryGetPath(PlayerHost host, out string path, out string warning)
        {
            // A Prefab Stage is saved by its own authoring workflow; do not write behind its open contents.
            path = string.Empty;
            warning = string.Empty;
            if (PrefabStageUtility.GetCurrentPrefabStage()?.scene == host.gameObject.scene)
            {
                warning = "Apply player changes from a scene instance, then reopen the player prefab to inspect them.";
                return false;
            }
            PlayerHost source = PrefabUtility.GetCorrespondingObjectFromSource(host);
            if (source == null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(host))
                    warning = "Apply the Player Host component to its player prefab before configuring the player.";
                return warning.Length == 0;
            }

            path = AssetDatabase.GetAssetPath(source);
            if (!path.EndsWith(".prefab") || PrefabUtility.IsPartOfImmutablePrefab(source)
                || !AssetDatabase.IsOpenForEdit(source) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                warning = "The player prefab is not writable. Choose an editable player prefab before applying its configuration.";
            else if (source.MasterPreset != host.MasterPreset)
                warning = "The scene player's Master is an override. Use the master saved in its player prefab before applying player changes.";
            else if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
                warning = "Close the player Prefab Stage before applying player changes from its scene instance.";
            return warning.Length == 0;
        }

        #endregion

        #region Application



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









        #endregion

        #endregion
    }
}
