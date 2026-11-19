using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reuses authored paragraphs so combo members can have different fonts without creating text objects.</summary>
    [DisallowMultipleComponent]
    public sealed class OrderBoardText : MonoBehaviour
    {
        #region Fields

        [Header("Authored Paragraphs")]
        [Tooltip("Existing TMP children for variant paragraphs and optional base-order typography, prepared before Play.")]
        [SerializeField]
        private TMP_Text[] paragraphs = Array.Empty<TMP_Text>();
        private TMP_Text original;
        private OrderTextStyle[] paragraphBaselines = Array.Empty<OrderTextStyle>();
        private Vector2[] paragraphSizes = Array.Empty<Vector2>();
        private Vector2[] paragraphPositions = Array.Empty<Vector2>();
        private bool originalEnabled;
        private readonly List<OrderTextParagraph> layout = new List<OrderTextParagraph>();

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Captures the original slot before the board starts assigning customers.</summary>
        /// <param name="slot">Existing board slot retaining the original layout and plain-text appearance.</param>
        internal void Initialize(TMP_Text slot)
        {
            // Reset retained managed state as well as graphics when scene reload is disabled.
            Restore();
            original = slot;
            originalEnabled = slot.enabled;
            paragraphBaselines = new OrderTextStyle[paragraphs.Length];
            paragraphSizes = new Vector2[paragraphs.Length];
            paragraphPositions = new Vector2[paragraphs.Length];
            // Each paragraph keeps the exact TMP appearance authored on its own scene object.
            for (int index = 0; index < paragraphs.Length; index++)
                if (paragraphs[index] != null)
                {
                    paragraphBaselines[index] = OrderTextStyle.Capture(paragraphs[index]);
                    paragraphSizes[index] = paragraphs[index].rectTransform.sizeDelta;
                    paragraphPositions[index] = paragraphs[index].rectTransform.anchoredPosition;
                }
            HideParagraphs();
        }

        /// <summary>Restores original component visibility when the board or its scene is released.</summary>
        internal void Restore()
        {
            Clear();
            if (original != null)
                original.enabled = originalEnabled;
            original = null;
            paragraphBaselines = Array.Empty<OrderTextStyle>();
            paragraphSizes = Array.Empty<Vector2>();
            paragraphPositions = Array.Empty<Vector2>();
        }

        /// <summary>Disables only preauthored paragraph children, retaining them for the next ticket.</summary>
        private void HideParagraphs()
        {
            foreach (TMP_Text text in paragraphs)
                if (text != null && text.gameObject.activeSelf)
                    text.gameObject.SetActive(false);
        }

        #endregion
        #region Drawing

        /// <summary>Maps base and variant fragments onto existing paragraphs, advancing at every semicolon.</summary>
        /// <param name="candidate">Base, single variant or combo currently occupying this slot.</param>
        /// <param name="shared">Typography from the catalog selected for this customer's spawn.</param>
        internal void Draw(OrderCandidate candidate, OrderTextStyle shared)
        {
            // Restore all existing paragraph objects before drawing a replacement ticket.
            Clear();
            if (original == null)
                return;
            OrderTextStyle style = OrderTextStyle.Resolve(candidate.Entry.TextStyle, shared);
            string content = candidate.MainText;
            bool includeBase = !string.IsNullOrWhiteSpace(content);
            bool separateBase = includeBase && (style != null || content.IndexOf(';') >= 0);
            // Build the whole layout before revealing any paragraph, including combo-specific styles.
            layout.Clear();
            if (separateBase)
                OrderTextParagraphs.Append(layout, content, style);
            if (candidate.Combo != null)
                foreach (OrderRecipeVariant variant in candidate.Combo)
                    OrderTextParagraphs.Append(layout, variant.Text, OrderTextStyle.Resolve(variant.TextStyle, shared));
            else if (candidate.Variant != null)
                OrderTextParagraphs.Append(layout, candidate.Variant.Text, OrderTextStyle.Resolve(candidate.Variant.TextStyle, shared));
            if (layout.Count > paragraphs.Length || Array.FindIndex(paragraphs, 0, layout.Count, text => text == null) >= 0)
            {
                Debug.LogWarning("Prepare enough Order Board paragraphs before Play for every base and variant text fragment separated by ';'.", this);
                return;
            }
            // Unstyled text without separators keeps the original slot and its existing layout.
            if (separateBase)
                original.enabled = false;
            float offset = includeBase && !separateBase ? original.preferredHeight : 0f;
            for (int index = 0; index < layout.Count; index++)
                Present(index, layout[index].Text, layout[index].Style, ref offset);
        }

        /// <summary>Restores the plain slot before clearing or replacing a ticket.</summary>
        internal void Clear()
        {
            HideParagraphs();
            // Restore each reused child from its own scene-authored TMP settings, not from the parent slot.
            for (int index = 0; index < paragraphs.Length; index++)
            {
                // Reused children return to their own scene-authored appearance and dimensions.
                if (paragraphs[index] == null)
                    continue;
                if (index < paragraphBaselines.Length && paragraphBaselines[index] != null)
                    paragraphBaselines[index].Apply(paragraphs[index]);
                if (index < paragraphSizes.Length)
                    paragraphs[index].rectTransform.sizeDelta = paragraphSizes[index];
                if (index < paragraphPositions.Length)
                    paragraphs[index].rectTransform.anchoredPosition = paragraphPositions[index];
            }
            if (original != null)
                original.enabled = originalEnabled;
        }

        /// <summary>Measures one styled paragraph only when its ticket changes.</summary>
        /// <param name="index">Authored paragraph index.</param>
        /// <param name="content">Independent base or variant content.</param>
        /// <param name="style">Optional local appearance override.</param>
        /// <param name="offset">Accumulated vertical layout in board-local units.</param>
        private void Present(int index, string content, OrderTextStyle style, ref float offset)
        {
            // Empty variants retain their recipe meaning without occupying visual space.
            if (string.IsNullOrWhiteSpace(content))
                return;
            TMP_Text text = paragraphs[index];
            style?.Apply(text);
            float indent = style is { Enabled: true } ? style.Indent : 0f;
            offset += style is { Enabled: true } ? style.SpaceBefore : 0f;
            if (indent > 0f)
                text.rectTransform.sizeDelta = new Vector2(paragraphSizes[index].x - indent, paragraphSizes[index].y);
            if (indent > 0f || offset > 0f)
                text.rectTransform.anchoredPosition = new Vector2(paragraphPositions[index].x + indent, paragraphPositions[index].y - offset);
            text.text = content;
            text.gameObject.SetActive(true);
            offset += text.preferredHeight + (style is { Enabled: true } ? style.SpaceAfter : 0f);
        }

        #endregion
        #endregion
    }
}
