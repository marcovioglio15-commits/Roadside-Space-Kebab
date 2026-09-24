using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors a reusable prefab-local dialogue overlay before gameplay begins.</summary>
    internal static class DialogueAuthoring
    {
        #region Methods

        #region Construction

        /// <summary>Connects an existing local HUD or creates one complete overlay with native UI components.</summary>
        /// <param name="dialogue">Prefab-stage interaction missing its HUD.</param>
        internal static void CreateHud(ObjectDialogue dialogue)
        {
            // Prefer sharing this object's valid authored overlay among its dialogue components.
            if (dialogue.Hud != null)
                return;
            DialogueHud hud = null;
            foreach (DialogueHud candidate in dialogue.GetComponentsInChildren<DialogueHud>(true))
                if (candidate.GetComponentInParent<ObjectItem>() == dialogue.GetComponent<ObjectItem>() && candidate.IsValid(dialogue.transform))
                {
                    hud = candidate;
                    break;
                }
            if (hud == null)
                hud = Build(dialogue.transform);
            using (SerializedObject data = new SerializedObject(dialogue))
            {
                data.FindProperty("hud").objectReferenceValue = hud;
                data.ApplyModifiedProperties();
            }
        }

        /// <summary>Builds an editable bottom-screen panel that never intercepts player input.</summary>
        /// <param name="owner">Prefab branch that owns the overlay.</param>
        /// <returns>The complete HUD with all presentation references assigned.</returns>
        private static DialogueHud Build(Transform owner)
        {
            // Screen-space layout is authored once and scales against a reference resolution.
            RectTransform root = ObjectUiAuthoring.CreateRect("Dialogue HUD", owner);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            canvas.enabled = false;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
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
                data.FindProperty("body").objectReferenceValue = body;
                data.FindProperty("speaker").objectReferenceValue = speaker;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create dialogue HUD");
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
