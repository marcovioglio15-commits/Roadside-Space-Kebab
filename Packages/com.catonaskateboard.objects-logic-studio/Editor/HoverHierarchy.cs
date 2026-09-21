using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stores prefab-local transform routes independently of temporary prefab-stage instance IDs.</summary>
    internal static class HoverHierarchy
    {
        #region Methods

        #region Routes

        /// <summary>Captures an unambiguous sibling-index route below the owning object.</summary>
        /// <param name="root">Owning prefab or interaction transform.</param>
        /// <param name="target">Descendant to locate; null means no anchor.</param>
        /// <returns>An index route, an empty route for the root, or a dash for no valid descendant.</returns>
        internal static string Path(Transform root, Transform target)
        {
            // Names can repeat within a prefab; sibling indices retain the actual chosen branch.
            if (target == null || !target.IsChildOf(root))
                return "-";
            List<int> indices = new List<int>();
            for (Transform current = target; current != root; current = current.parent)
                indices.Add(current.GetSiblingIndex());
            indices.Reverse();
            return string.Join("/", indices);
        }

        /// <summary>Resolves a retained route without creating or moving any transforms.</summary>
        /// <param name="root">Current owner of the hierarchy.</param>
        /// <param name="path">Route captured by Path.</param>
        /// <returns>The matching transform, or null when the route no longer exists.</returns>
        internal static Transform Resolve(Transform root, string path)
        {
            // Missing routes stay missing instead of selecting a similarly named object.
            if (root == null || path == "-")
                return null;
            if (string.IsNullOrEmpty(path))
                return root;
            foreach (string part in path.Split('/'))
            {
                if (!int.TryParse(part, out int index) || index < 0 || index >= root.childCount)
                    return null;
                root = root.GetChild(index);
            }
            return root;
        }

        /// <summary>Captures hierarchy structure so a pending anchor cannot silently bind to another branch.</summary>
        /// <param name="root">Interaction hierarchy to inspect at session boundaries.</param>
        /// <returns>A structural signature independent of world pose and prefab-stage instance IDs.</returns>
        internal static string Signature(Transform root)
        {
            // This is evaluated on selection and Apply, never by the runtime hover loop.
            System.Text.StringBuilder signature = new System.Text.StringBuilder();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                signature.Append(Path(root, child)).Append(':').Append(child.name.Length).Append(':').Append(child.name).Append(';');
            return signature.ToString();
        }

        #endregion

        #endregion
    }
}
