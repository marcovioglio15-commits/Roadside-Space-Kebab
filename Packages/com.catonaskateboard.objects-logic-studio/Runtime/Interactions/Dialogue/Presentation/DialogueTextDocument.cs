using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Contains text-token state for one source character, independent of glyph layout.</summary>
    internal struct DialogueGlyphStyle
    {
        internal bool Shake;
        internal bool Wave;
        internal bool Slam;
        internal bool Rainbow;
        internal bool Colored;
        internal Color Color;
        internal float Speed;
        internal float Pause;
    }

    /// <summary>Retains a parsed page and per-source-character styling without changing the entry preset.</summary>
    internal sealed class DialogueTextDocument
    {
        #region State

        internal readonly string Text;
        internal readonly DialogueGlyphStyle[] Styles;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures one parsed page for layout and reveal timing.</summary>
        /// <param name="text">Text with effect tokens removed.</param>
        /// <param name="styles">One style per UTF-16 source position.</param>
        internal DialogueTextDocument(string text, DialogueGlyphStyle[] styles)
        {
            Text = text;
            Styles = styles;
        }

        #endregion

        #endregion
    }
}
