using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one paragraph's text and optional typography until a board slot is refreshed.</summary>
    internal readonly struct OrderTextParagraph
    {
        #region Fields

        internal readonly string Text;
        internal readonly OrderTextStyle Style;

        #endregion
        #region Methods
        #region Construction

        /// <summary>Pairs one semicolon-delimited fragment with its base or variant appearance.</summary>
        /// <param name="text">Paragraph text with escaped line breaks resolved.</param>
        /// <param name="style">Optional appearance inherited by this fragment.</param>
        internal OrderTextParagraph(string text, OrderTextStyle style)
        {
            // Paragraphs keep their own TMP baseline when no override applies.
            Text = text;
            Style = style;
        }

        #endregion
        #endregion
    }

    /// <summary>Shares paragraph splitting and plain-text fallback between board presentation paths.</summary>
    internal static class OrderTextParagraphs
    {
        #region Methods
        #region Formatting

        /// <summary>Appends one prepared paragraph per semicolon-separated fragment without creating UI.</summary>
        /// <param name="destination">Reusable layout belonging to a board slot.</param>
        /// <param name="content">Base or variant text. Consecutive delimiters retain their paragraph positions.</param>
        /// <param name="style">Optional typography shared by these fragments.</param>
        internal static void Append(List<OrderTextParagraph> destination, string content, OrderTextStyle style)
        {
            // Empty variants retain their recipe meaning without claiming paragraph objects.
            if (string.IsNullOrWhiteSpace(content))
                return;
            foreach (string paragraph in content.Split(';'))
                destination.Add(new OrderTextParagraph(PlainText(paragraph), style));
        }

        /// <summary>Resolves text breaks when a board has no prepared paragraph presenter.</summary>
        /// <param name="content">Order text or a previously split fragment.</param>
        /// <returns>Display text without literal semicolon delimiters or escaped newlines.</returns>
        internal static string PlainText(string content)
        {
            // A missing presenter still preserves the requested breaks in the original TMP slot.
            return (content ?? string.Empty).Replace("\\n", "\n").Replace(';', '\n');
        }

        #endregion
        #endregion
    }
}
