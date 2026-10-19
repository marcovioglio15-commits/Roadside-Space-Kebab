using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Routes project and scene selections to the existing prefab editing transaction.</summary>
    internal static class ObjectStudioShortcuts
    {
        #region Methods
        #region Navigation

        /// <summary>Opens the selected prefab at its corresponding asset branch, including scene instances.</summary>
        [MenuItem("Tools/Objects Logic Studio Selected Prefab %l")]
        private static void OpenSelected()
        {
            // Resolve before opening the window, whose selection callbacks may change the active object.
            GameObject target = ResolveSelection();
            if (target != null)
                ObjectsLogicStudioWindow.Open(target);
        }

        /// <summary>Enables the command only for editable prefab selections outside Play.</summary>
        /// <returns>True when selection can resolve to a saved prefab or its open stage.</returns>
        [MenuItem("Tools/Objects Logic Studio Selected Prefab %l", true)]
        private static bool CanOpenSelected()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && ResolveSelection() != null;
        }

        /// <summary>Preserves the selected branch instead of redirecting nested instances to an unrelated root.</summary>
        /// <returns>Corresponding editable prefab object, or null for ordinary scene objects and models.</returns>
        private static GameObject ResolveSelection()
        {
            // A prefab stage is already an authoring target; scene instances resolve through their nearest source.
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return null;
            if (PrefabStageUtility.GetPrefabStage(selected) != null)
                return selected;
            GameObject source = EditorUtility.IsPersistent(selected) ? selected : PrefabUtility.GetCorrespondingObjectFromSource(selected);
            return source != null && AssetDatabase.GetAssetPath(source).EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                ? source : null;
        }

        #endregion
        #endregion
    }
}
