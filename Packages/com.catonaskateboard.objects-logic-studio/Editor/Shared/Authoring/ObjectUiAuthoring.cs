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

        /// <summary>Stretches an existing rectangle across its parent's complete content area.</summary>
        /// <param name="rect">Authored UI branch whose layout should follow the shared Canvas.</param>
        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        #endregion

        #endregion
    }
}
