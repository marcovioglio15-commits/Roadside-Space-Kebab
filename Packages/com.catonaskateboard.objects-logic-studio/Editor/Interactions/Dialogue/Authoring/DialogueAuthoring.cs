using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors one shared dialogue overlay for an observer before gameplay begins.</summary>
    internal static class DialogueAuthoring
    {
        #region Methods

        #region Construction

        /// <summary>Builds an editable dialogue branch under the existing shared Canvas.</summary>
        /// <param name="owner">Scene UI root receiving the branch.</param>
        /// <param name="canvas">Canvas used by all gameplay panels.</param>
        /// <returns>The authored dialogue controller with its existing graphics assigned.</returns>
        internal static DialogueHud Build(Transform owner, Canvas canvas)
        {
            RectTransform root = ObjectUiAuthoring.CreateRect("Dialogue HUD", owner);
            ObjectUiAuthoring.Stretch(root);
            RectTransform panel = ObjectUiAuthoring.CreateRect("Panel", root);
            panel.anchorMin = new Vector2(0.15f, 0.05f);
            panel.anchorMax = new Vector2(0.85f, 0.25f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.06f, 0.94f);
            background.raycastTarget = false;

            // Explicit pages keep their authored line breaks; the tool never splits text automatically.
            Text speaker = CreateText("Speaker", panel, new Vector2(24f, -52f), new Vector2(-24f, -16f), 26);
            speaker.rectTransform.anchorMin = new Vector2(0f, 1f);
            speaker.rectTransform.offsetMin = new Vector2(24f, -52f);
            speaker.rectTransform.offsetMax = new Vector2(-24f, -16f);
            speaker.fontStyle = FontStyle.Bold;
            Text body = CreateText("Page", panel, new Vector2(24f, 18f), new Vector2(-24f, -58f), 25);
            DialogueHud hud = root.gameObject.AddComponent<DialogueHud>();
            using (SerializedObject data = new SerializedObject(hud))
            {
                data.FindProperty("canvas").objectReferenceValue = canvas;
                data.FindProperty("panel").objectReferenceValue = panel;
                data.FindProperty("body").objectReferenceValue = body;
                data.FindProperty("speaker").objectReferenceValue = speaker;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            panel.gameObject.SetActive(false);
            return hud;
        }

        /// <summary>Creates one existing text graphic with editable font, margins and colors.</summary>
        /// <param name="name">Hierarchy name for the text graphic.</param>
        /// <param name="parent">Panel containing the graphic.</param>
        /// <param name="minimum">Lower-left inset.</param>
        /// <param name="maximum">Upper-right inset.</param>
        /// <param name="size">Initial font size at the reference resolution.</param>
        /// <returns>The authored text component.</returns>
        private static Text CreateText(string name, Transform parent, Vector2 minimum, Vector2 maximum, int size)
        {
            // Graphics use no raycaster or event handlers, leaving movement and interaction maps untouched.
            RectTransform rect = ObjectUiAuthoring.CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        #endregion

        #endregion
    }
}
