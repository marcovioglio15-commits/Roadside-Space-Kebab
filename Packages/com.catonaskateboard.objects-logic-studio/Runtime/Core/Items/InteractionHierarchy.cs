using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks stored branch activation without activating isolated prefab or preset previews.</summary>
    public static class InteractionHierarchy
    {
        #region Methods
        #region Activation

        /// <summary>Checks descendant switches while ignoring the supplied root's external activation.</summary>
        /// <param name="branch">Branch whose local activation is inspected.</param>
        /// <param name="root">Owner boundary, which may be inactive during validation.</param>
        /// <returns>True when every descendant is locally active and belongs to the root.</returns>
        public static bool ActiveWithin(Transform branch, Transform root)
        {
            // Preset validation and assembly staging intentionally keep their owner inactive.
            while (branch != null && branch != root)
            {
                if (!branch.gameObject.activeSelf)
                    return false;
                branch = branch.parent;
            }
            return root != null && branch == root;
        }

        #endregion
        #endregion
    }
}
