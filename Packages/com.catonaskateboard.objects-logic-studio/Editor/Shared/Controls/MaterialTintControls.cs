using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares color-property multiplication controls between contact modifications and sauce exposure.</summary>
    internal static class MaterialTintControls
    {
        #region Methods
        #region Controls
        /// <summary>Shows renderer targeting only while material tint is enabled.</summary>
        /// <param name="effects">Serialized contact-compatible effect settings.</param>
        /// <param name="other">Whether the receiving hierarchy needs an explicit source.</param>
        /// <param name="source">Known hierarchy, or null to select one when needed.</param>
        /// <param name="selectSource">Whether this group owns the source picker rather than its enclosing effects section.</param>
        internal static void Draw(SerializedProperty effects, bool other, GameObject source = null, bool selectSource = true)
        {
            HoverControls.Field(effects, "Tint");
            if (!effects.FindPropertyRelative("Tint").boolValue)
                return;
            if (selectSource)
                source = HierarchyPathMenu.Source(effects, other, source);
            HoverControls.Field(effects, "Multiplier");
            HoverControls.Field(effects, "ColorProperty");
            HierarchyPathMenu.Draw(effects.FindPropertyRelative("RendererPath"), source, false, true);
            HoverControls.Field(effects, "MaterialSlot");
        }
        #endregion
        #endregion
    }
}
