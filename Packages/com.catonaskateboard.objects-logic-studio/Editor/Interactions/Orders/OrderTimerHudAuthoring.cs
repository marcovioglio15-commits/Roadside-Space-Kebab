using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares the stopwatch within the existing gameplay Canvas before Play.</summary>
    internal static class OrderTimerHudAuthoring
    {
        #region Methods
        #region Authoring

        /// <summary>Builds one editable timer panel, reusing an already authored controller if present.</summary>
        /// <param name="root">Shared scene Canvas transform.</param>
        /// <param name="canvas">Existing gameplay rendering surface.</param>
        /// <returns>The saved stopwatch controller with its required references assigned.</returns>
        internal static OrderTimerHud Build(Transform root, Canvas canvas)
        {
            // This editor-only path creates no additional Canvas and can safely be invoked again.
            OrderTimerHud existing = root.GetComponentInChildren<OrderTimerHud>(true);
            if (existing != null)
                return existing;
            RectTransform host = ObjectUiAuthoring.CreateRect("Order Timer HUD", root);
            ObjectUiAuthoring.Stretch(host);
            OrderTimerHud hud = host.gameObject.AddComponent<OrderTimerHud>();
            RectTransform panel = ObjectUiAuthoring.CreateRect("Stopwatch", host);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-40f, -40f);
            panel.sizeDelta = new Vector2(280f, 48f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.05f, 0.8f);
            background.raycastTarget = false;
            RectTransform label = ObjectUiAuthoring.CreateRect("Elapsed Time", panel);
            ObjectUiAuthoring.Stretch(label);
            label.offsetMin = new Vector2(12f, 4f);
            label.offsetMax = new Vector2(-12f, -4f);
            Text text = label.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleRight;
            text.text = "Order 00:00";
            text.raycastTarget = false;
            using SerializedObject data = new SerializedObject(hud);
            data.FindProperty("canvas").objectReferenceValue = canvas;
            data.FindProperty("panel").objectReferenceValue = panel;
            data.FindProperty("text").objectReferenceValue = text;
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return hud;
        }

        #endregion
        #endregion
    }
}
