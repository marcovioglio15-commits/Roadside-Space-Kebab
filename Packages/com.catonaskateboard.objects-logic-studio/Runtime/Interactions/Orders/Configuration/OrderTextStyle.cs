using System;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Styles one order paragraph independently of its parent order and other combo members.</summary>
    [Serializable]
    public sealed class OrderTextStyle
    {
        #region Fields

        [Header("Board Text")]
        [Tooltip("Use this paragraph's typography and layout. Disabled keeps the authored board appearance.")]
        public bool Enabled;
        [Tooltip("Optional TMP font asset. Empty keeps the board's font.")]
        public TMP_FontAsset Font;
        [Tooltip("Optional material preset for the selected font, including outline, shadow and face effects.")]
        public Material Material;
        [Tooltip("Text color and opacity, independent of other order paragraphs.")]
        public Color Color = Color.white;
        [Tooltip("Bold, italic, underline, casing and other supported TMP font styles.")]
        public FontStyles Style;
        [Tooltip("Font weight when the selected TMP font provides the corresponding weight face.")]
        public FontWeight Weight = FontWeight.Regular;
        [Tooltip("Font size in TMP points. A world-space board commonly uses much smaller values than screen UI.")]
        [Range(0.1f, 120f)]
        public float Size = 3f;
        [Tooltip("Fit this paragraph within its authored text rectangle using the size limits below.")]
        public bool AutoSize;
        [Tooltip("Smallest allowed font size when automatic sizing is enabled.")]
        [Range(0.1f, 120f)]
        public float MinimumSize = 1f;
        [Tooltip("Largest allowed font size when automatic sizing is enabled.")]
        [Range(0.1f, 120f)]
        public float MaximumSize = 3f;
        [Tooltip("Horizontal and vertical alignment within this paragraph's rectangle.")]
        public TextAlignmentOptions Alignment = TextAlignmentOptions.TopLeft;
        [Tooltip("Wrap overflowing words or keep the text on its authored lines.")]
        public TextWrappingModes Wrapping = TextWrappingModes.Normal;
        [Tooltip("Overflow behavior after wrapping and automatic sizing.")]
        public TextOverflowModes Overflow = TextOverflowModes.Overflow;
        [Tooltip("Interpret supported TMP formatting tags within this paragraph.")]
        public bool RichText = true;
        [Header("Spacing")]
        [Tooltip("Additional spacing between characters, in TMP font units.")]
        [Range(-20f, 40f)]
        public float CharacterSpacing;
        [Tooltip("Additional spacing between words, in TMP font units.")]
        [Range(-20f, 40f)]
        public float WordSpacing;
        [Tooltip("Additional spacing between wrapped lines, in TMP font units.")]
        [Range(-20f, 100f)]
        public float LineSpacing;
        [Tooltip("Additional spacing after explicit paragraph breaks, in TMP font units.")]
        [Range(0f, 100f)]
        public float ParagraphSpacing;
        [Tooltip("Left indentation in the board slot's local units.")]
        [Range(0f, 10f)]
        public float Indent;
        [Tooltip("Empty space before this paragraph in board-local units.")]
        [Range(0f, 5f)]
        public float SpaceBefore;
        [Tooltip("Empty space after this paragraph in board-local units.")]
        [Range(0f, 5f)]
        public float SpaceAfter;
        [Tooltip("Left, top, right and bottom text margins in the text rectangle's local units.")]
        public Vector4 Margins;
        [Header("Vertex Gradient")]
        [Tooltip("Multiply text color by separate colors at the four glyph corners.")]
        public bool UseGradient;
        [Tooltip("Top-left glyph color when Vertex Gradient is enabled.")]
        public Color TopLeft = Color.white;
        [Tooltip("Top-right glyph color when Vertex Gradient is enabled.")]
        public Color TopRight = Color.white;
        [Tooltip("Bottom-left glyph color when Vertex Gradient is enabled.")]
        public Color BottomLeft = Color.white;
        [Tooltip("Bottom-right glyph color when Vertex Gradient is enabled.")]
        public Color BottomRight = Color.white;

        #endregion
        #region Methods
        #region Presentation

        /// <summary>Applies an enabled paragraph override to existing TMP graphics.</summary>
        /// <param name="text">Preauthored paragraph, already restored to the board's baseline.</param>
        internal void Apply(TMP_Text text)
        {
            // Font materials are shared assets; changing typography never clones UI objects.
            if (!Enabled)
                return;
            if (Font != null)
                text.font = Font;
            if (Material != null)
                text.fontSharedMaterial = Material;
            text.color = Color;
            text.fontStyle = Style;
            text.fontWeight = Weight;
            text.fontSize = Size;
            text.enableAutoSizing = AutoSize;
            text.fontSizeMin = MinimumSize;
            text.fontSizeMax = MaximumSize;
            text.alignment = Alignment;
            text.textWrappingMode = Wrapping;
            text.overflowMode = Overflow;
            text.richText = RichText;
            text.characterSpacing = CharacterSpacing;
            text.wordSpacing = WordSpacing;
            text.lineSpacing = LineSpacing;
            text.paragraphSpacing = ParagraphSpacing;
            text.margin = Margins;
            text.enableVertexGradient = UseGradient;
            text.colorGradient = new VertexGradient(TopLeft, TopRight, BottomLeft, BottomRight);
        }

        /// <summary>Captures the authored board typography before any customer can replace it.</summary>
        /// <param name="text">Original slot supplying defaults for uncustomized paragraphs.</param>
        /// <returns>Independent snapshot restored before each paragraph is drawn.</returns>
        internal static OrderTextStyle Capture(TMP_Text text)
        {
            // Capture once at board activation, never during an individual render frame.
            return new OrderTextStyle
            {
                Enabled = true, Font = text.font, Material = text.fontSharedMaterial, Color = text.color,
                Style = text.fontStyle, Weight = text.fontWeight, Size = text.fontSize, AutoSize = text.enableAutoSizing,
                MinimumSize = text.fontSizeMin, MaximumSize = text.fontSizeMax, Alignment = text.alignment,
                Wrapping = text.textWrappingMode, Overflow = text.overflowMode, RichText = text.richText,
                CharacterSpacing = text.characterSpacing, WordSpacing = text.wordSpacing,
                LineSpacing = text.lineSpacing, ParagraphSpacing = text.paragraphSpacing, Margins = text.margin,
                UseGradient = text.enableVertexGradient, TopLeft = text.colorGradient.topLeft,
                TopRight = text.colorGradient.topRight, BottomLeft = text.colorGradient.bottomLeft,
                BottomRight = text.colorGradient.bottomRight
            };
        }

        #endregion
        #region Validation

        /// <summary>Checks active typography without snapping invalid numbers or changing shared materials.</summary>
        /// <param name="warning">Receives the first unusable paragraph setting.</param>
        /// <returns>True for disabled or fully usable overrides.</returns>
        public bool TryValidate(out string warning)
        {
            // A material override must belong to an explicit font's atlas when that font is assigned.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (!InteractionValues.Positive(Size) || AutoSize && (!InteractionValues.Positive(MinimumSize)
                || !InteractionValues.Positive(MaximumSize) || MaximumSize < MinimumSize)
                || !float.IsFinite(CharacterSpacing) || !float.IsFinite(WordSpacing) || !float.IsFinite(LineSpacing)
                || !SpawnFlowPlan.Duration(ParagraphSpacing) || !SpawnFlowPlan.Duration(Indent)
                || !SpawnFlowPlan.Duration(SpaceBefore) || !SpawnFlowPlan.Duration(SpaceAfter)
                || !float.IsFinite(Margins.x) || !float.IsFinite(Margins.y) || !float.IsFinite(Margins.z) || !float.IsFinite(Margins.w))
                warning = "Use finite text spacing, positive font sizes and ordered automatic size limits.";
            else if (Material != null && Font != null && Material.mainTexture != Font.atlasTexture)
                warning = "Choose a material preset using the selected font's atlas texture.";
            else if (Overflow == TextOverflowModes.Linked)
                warning = "Linked overflow needs a separate text chain. Choose an overflow mode for this independent order paragraph.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
