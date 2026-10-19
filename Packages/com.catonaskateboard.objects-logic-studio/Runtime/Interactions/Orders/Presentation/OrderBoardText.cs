using System;
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
        [Tooltip("Existing TMP children for the base order and its variant paragraphs, prepared before Play.")]
        [SerializeField]
        private TMP_Text[] paragraphs = Array.Empty<TMP_Text>();
        private TMP_Text original;
        private OrderTextStyle baseline;
        private bool originalEnabled;

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
            baseline = OrderTextStyle.Capture(slot);
            HideParagraphs();
        }

        /// <summary>Restores original component visibility when the board or its scene is released.</summary>
        internal void Restore()
        {
            HideParagraphs();
            if (original != null)
                original.enabled = originalEnabled;
            original = null;
            baseline = null;
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

        /// <summary>Draws each selected variant with its own appearance, preserving uncustomized orders exactly.</summary>
        /// <param name="candidate">Base, single variant or combo currently occupying this slot.</param>
        /// <param name="completed">Whether every displayed paragraph must be crossed out.</param>
        internal void Draw(OrderCandidate candidate, bool completed)
        {
            // The original text remains the fast path for catalogs with no paragraph overrides.
            Clear();
            bool styled = candidate.Entry.TextStyle is { Enabled: true };
            if (candidate.Combo != null)
                foreach (OrderRecipeVariant variant in candidate.Combo)
                    styled |= variant.TextStyle is { Enabled: true };
            else
                styled |= candidate.Variant?.TextStyle is { Enabled: true };
            if (!styled || original == null || baseline == null)
                return;
            bool includeBase = candidate.Combo != null || candidate.Variant == null
                || candidate.Variant.AppendToBaseText || string.IsNullOrWhiteSpace(candidate.Variant.Text);
            int count = (includeBase ? 1 : 0) + (candidate.Combo?.Length ?? (candidate.Variant != null ? 1 : 0));
            if (count > paragraphs.Length)
            {
                Debug.LogWarning("Prepare more Order Board text paragraphs before Play for this variant combo.", this);
                return;
            }
            original.enabled = false;
            float offset = 0f;
            int index = 0;
            if (includeBase)
                Present(index++, candidate.Entry.Text, candidate.Entry.TextStyle, completed, ref offset);
            if (candidate.Combo != null)
                foreach (OrderRecipeVariant variant in candidate.Combo)
                    Present(index++, variant.Text, variant.TextStyle, completed, ref offset);
            else if (candidate.Variant != null)
                Present(index, candidate.Variant.Text, candidate.Variant.TextStyle, completed, ref offset);
        }

        /// <summary>Restores the plain slot before clearing or replacing a ticket.</summary>
        internal void Clear()
        {
            HideParagraphs();
            if (original != null)
                original.enabled = originalEnabled;
        }

        /// <summary>Measures one styled paragraph only when its ticket changes.</summary>
        /// <param name="index">Authored paragraph index.</param>
        /// <param name="content">Independent base or variant content.</param>
        /// <param name="style">Optional local appearance override.</param>
        /// <param name="completed">Whether this paragraph is fulfilled.</param>
        /// <param name="offset">Accumulated vertical layout in board-local units.</param>
        private void Present(int index, string content, OrderTextStyle style, bool completed, ref float offset)
        {
            // Empty variants retain their recipe meaning without occupying visual space.
            if (string.IsNullOrWhiteSpace(content))
                return;
            TMP_Text text = paragraphs[index];
            baseline.Apply(text);
            style?.Apply(text);
            float indent = style is { Enabled: true } ? style.Indent : 0f;
            offset += style is { Enabled: true } ? style.SpaceBefore : 0f;
            text.rectTransform.sizeDelta = new Vector2(original.rectTransform.rect.width - indent, original.rectTransform.rect.height);
            text.rectTransform.anchoredPosition = new Vector2(indent, -offset);
            text.text = content.Replace("\\n", "\n");
            text.fontStyle = completed ? text.fontStyle | FontStyles.Strikethrough : text.fontStyle & ~FontStyles.Strikethrough;
            text.gameObject.SetActive(true);
            offset += text.preferredHeight + (style is { Enabled: true } ? style.SpaceAfter : 0f);
        }

        #endregion
        #endregion
    }
}
