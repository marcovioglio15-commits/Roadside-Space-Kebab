using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors a reusable assembly progress overlay in a player prefab or scene before Play.</summary>
    internal static class AssemblyHudAuthoring
    {
        #region Methods

        #region Construction

        /// <summary>Builds an assembly branch under the caller's existing scene Canvas.</summary>
        /// <param name="parent">Existing shared UI root.</param>
        /// <param name="canvas">Canvas used by every gameplay panel.</param>
        /// <returns>The authored charge panel with complete rendering dependencies.</returns>
        internal static AssemblyChargeHud Build(Transform parent, Canvas canvas)
        {
            RectTransform root = ObjectUiAuthoring.CreateRect("Assembly HUD", parent);
            ObjectUiAuthoring.Stretch(root);
            RectTransform panel = ObjectUiAuthoring.CreateRect("Progress", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, -80f);
            panel.sizeDelta = new Vector2(64f, 64f);
            RectTransform circle = ObjectUiAuthoring.CreateRect("Ring", panel);
            circle.anchorMin = Vector2.zero;
            circle.anchorMax = Vector2.one;
            circle.offsetMin = circle.offsetMax = Vector2.zero;
            circle.gameObject.AddComponent<CanvasRenderer>();
            AssemblyChargeRing ring = circle.gameObject.AddComponent<AssemblyChargeRing>();
            ring.raycastTarget = false;
            RectTransform center = ObjectUiAuthoring.CreateRect("Icon", panel);
            center.anchorMin = new Vector2(0.28f, 0.28f);
            center.anchorMax = new Vector2(0.72f, 0.72f);
            center.offsetMin = center.offsetMax = Vector2.zero;
            Image icon = center.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            // All references are serialized now; runtime only changes colors, fill and canvas visibility.
            AssemblyChargeHud hud = root.gameObject.AddComponent<AssemblyChargeHud>();
            using (SerializedObject data = new SerializedObject(hud))
            {
                data.FindProperty("canvas").objectReferenceValue = canvas;
                data.FindProperty("panel").objectReferenceValue = panel;
                data.FindProperty("ring").objectReferenceValue = ring;
                data.FindProperty("icon").objectReferenceValue = icon;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            panel.gameObject.SetActive(false);
            return hud;
        }

        #endregion

        #endregion
    }
}
