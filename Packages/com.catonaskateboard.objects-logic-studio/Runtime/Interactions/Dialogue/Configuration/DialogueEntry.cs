using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares ordered pages independently of triggers, branching and random selection.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Entry Preset")]
    public sealed class DialogueEntry : ScriptableObject
    {
        #region Fields

        [Header("Dialogue Entry")]
        [Tooltip("Name displayed for this reusable page sequence.")]
        public string Name = "Dialogue";
        [Tooltip("Pages displayed in order. Closing the final page follows this row's connection.")]
        public DialogueLine[] Lines = { new DialogueLine() };
        [Header("Text Effects")]
        [Tooltip("Letter timing and the strength of optional word-effect tokens.")]
        public DialogueTextSettings TextEffects = new DialogueTextSettings();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable content without imposing any trigger or order condition.</summary>
        /// <param name="warning">Receives an empty sequence or text page.</param>
        /// <returns>True when every page contains text.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Each dialogue entry needs at least one complete text page.";
            if (Lines == null || Lines.Length == 0)
                return false;
            if (TextEffects == null || !TextEffects.TryValidate(out warning))
                return false;
            foreach (DialogueLine line in Lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    warning = "Each dialogue entry needs at least one complete text page.";
                    return false;
                }
                if (!DialogueTextParser.TryParse(line.Text, TextEffects, out DialogueTextDocument document, out warning))
                    return false;
                if (string.IsNullOrWhiteSpace(document.Text))
                {
                    warning = "Text tokens need visible text between them.";
                    return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
