using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares basic editor-only layout construction between authored object overlays.</summary>
    internal static class ObjectUiAuthoring
    {
        #region Methods

        #region Layout

        /// <summary>Creates a centered rectangle under an existing prefab hierarchy.</summary>
        /// <param name="name">Hierarchy name assigned to the new object.</param>
        /// <param name="parent">Existing parent in the prefab stage.</param>
        /// <returns>The new layout rectangle.</returns>
        internal static RectTransform CreateRect(string name, Transform parent)
        {
            // The caller registers the complete hierarchy with Undo after assigning its dependencies.
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        #endregion

        #endregion
    }
}
