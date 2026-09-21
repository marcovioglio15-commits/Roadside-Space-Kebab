using System;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable text and layout values applied to existing prefab graphics at activation.</summary>
    [Serializable]
    public sealed class HoverStyle
    {
        #region Serialized Fields

        [Header("Text")]
        [Tooltip("Text displayed while this interaction is eligible.")]
        [SerializeField]
        [TextArea]
        private string content = "Inspect";

        [Tooltip("Optional font override. Empty keeps the font already authored on the label.")]
        [SerializeField]
        private Font font = null;

        [Tooltip("Font size at a 1080-pixel viewport height.")]
        [SerializeField]
        private int fontSize = 24;

        [Tooltip("Style supported by the selected font.")]
        [SerializeField]
        private FontStyle fontStyle = FontStyle.Normal;

        [Tooltip("Text tint and opacity.")]
        [SerializeField]
        private Color textColor = Color.white;

        [Tooltip("Alignment inside the label rectangle.")]
        [SerializeField]
        private TextAnchor alignment = TextAnchor.MiddleCenter;

        [Tooltip("Allow supported formatting tags in the text.")]
        [SerializeField]
        private bool richText = true;

        [Tooltip("Wrap text or allow it beyond the label width.")]
        [SerializeField]
        private HorizontalWrapMode horizontalOverflow = HorizontalWrapMode.Wrap;

        [Tooltip("Clip overflowing lines or allow them beyond the label height.")]
        [SerializeField]
        private VerticalWrapMode verticalOverflow = VerticalWrapMode.Overflow;

        [Header("Layout")]
        [Tooltip("Label dimensions at a 1080-pixel viewport height.")]
        [SerializeField]
        private Vector2 size = new Vector2(280f, 64f);

        [Tooltip("Negative values inset the stretched text within the label.")]
        [SerializeField]
        private Vector2 textSizeOffset = new Vector2(-24f, -12f);

        [Tooltip("Text displacement inside the label in reference pixels.")]
        [SerializeField]
        private Vector2 textOffset = Vector2.zero;

        [Tooltip("Overlay order relative to other canvases.")]
        [SerializeField]
        private int sortingOrder = 20;

        [Header("Background")]
        [Tooltip("Show the existing background Image.")]
        [SerializeField]
        private bool showBackground = true;

        [Tooltip("Background tint and opacity.")]
        [SerializeField]
        private Color backgroundColor = new Color(0.04f, 0.06f, 0.08f, 0.88f);

        [Tooltip("Optional sprite; empty uses a solid rectangle.")]
        [SerializeField]
        private Sprite backgroundSprite = null;

        [Tooltip("Simple stretches the sprite; Sliced uses its border.")]
        [SerializeField]
        private Image.Type backgroundType = Image.Type.Simple;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects unusable active values without changing the preset.</summary>
        /// <param name="warning">Receives the first configuration issue.</param>
        /// <returns>True when the existing graphics can use this style.</returns>
        public bool TryValidate(out string warning)
        {
            // Background-only options remain irrelevant when the background is disabled.
            warning = string.Empty;
            if (fontSize <= 0 || !Finite(size) || size.x <= 0f || size.y <= 0f
                || !Finite(textOffset) || !Finite(textSizeOffset)
                || size.x + textSizeOffset.x <= 0f || size.y + textSizeOffset.y <= 0f)
                warning = "Use positive font and label sizes, finite offsets and a text inset smaller than the label.";
            else if (!Finite(textColor) || showBackground && !Finite(backgroundColor))
                warning = "Text and enabled background colors must contain finite values.";
            else if (showBackground && backgroundSprite != null
                && backgroundType != Image.Type.Simple && backgroundType != Image.Type.Sliced)
                warning = "Choose Simple or Sliced for the hover background sprite.";
            return warning.Length == 0;
        }

        /// <summary>Checks offset coordinates before the layout consumes them.</summary>
        /// <param name="value">Authored rectangle dimensions or offset.</param>
        /// <returns>True when both coordinates are finite.</returns>
        private static bool Finite(Vector2 value)
        {
            // Numeric validation never normalizes the entered values.
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        /// <summary>Checks all tint channels without replacing HDR or transparency values.</summary>
        /// <param name="value">Authored graphic color.</param>
        /// <returns>True when all color channels are finite.</returns>
        private static bool Finite(Color value)
        {
            // Unity may accept out-of-range colors; only non-finite values are rejected here.
            return float.IsFinite(value.r) && float.IsFinite(value.g) && float.IsFinite(value.b) && float.IsFinite(value.a);
        }

        #endregion

        #region Presentation

        /// <summary>Applies validated values once to an already authored label.</summary>
        /// <param name="label">Existing UI owned by the interaction; callers validate its references first.</param>
        public void Apply(HoverLabel label)
        {
            // A preset changes component values, never the UI hierarchy.
            Text text = label.Text;
            text.text = content;
            if (font != null)
                text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = textColor;
            text.alignment = alignment;
            text.supportRichText = richText;
            text.horizontalOverflow = horizontalOverflow;
            text.verticalOverflow = verticalOverflow;
            label.Panel.sizeDelta = size;
            text.rectTransform.sizeDelta = textSizeOffset;
            text.rectTransform.anchoredPosition = textOffset;
            label.Canvas.sortingOrder = sortingOrder;
            if (label.Background == null)
                return;
            label.Background.enabled = showBackground;
            if (!showBackground)
                return;
            label.Background.color = backgroundColor;
            label.Background.sprite = backgroundSprite;
            label.Background.type = backgroundSprite != null ? backgroundType : Image.Type.Simple;
        }

        #endregion

        #endregion
    }
}
