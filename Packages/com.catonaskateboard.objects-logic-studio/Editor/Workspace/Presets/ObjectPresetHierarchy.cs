using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Resolves named hierarchy slots while rejecting duplicate sibling names.</summary>
    internal static class ObjectPresetHierarchy
    {
        #region Methods

        #region Paths

        /// <summary>Finds a relative path without picking an arbitrary duplicate.</summary>
        /// <param name="root">Configuration root.</param>
        /// <param name="path">Slash-separated child names, or empty for the root.</param>
        /// <returns>The unique matching transform or null.</returns>
        internal static Transform Find(Transform root, string path)
        {
            if (root == null || path == null)
                return null;
            if (path.Length == 0)
                return root;
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform selected = null;
                foreach (Transform child in current)
                    if (string.Equals(child.name, segment, StringComparison.Ordinal))
                    {
                        if (selected != null)
                            return null;
                        selected = child;
                    }
                if (selected == null)
                    return null;
                current = selected;
            }
            return current;
        }

        #endregion

        #endregion
    }
}
