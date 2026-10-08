using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one ordered page sequence and its eligibility across independent dialogue interactions.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Entry Preset")]
    public sealed class DialogueEntry : ScriptableObject
    {
        #region Fields

        [Header("Dialogue Entry")]
        [Tooltip("Name displayed for this reusable dialogue sequence.")]
        public string Name = "Dialogue";
        [Tooltip("Relative chance when an interaction uses Weighted Random. Zero excludes this entry from weighted selection.")]
        public float Weight = 1f;
        [Tooltip("Optional filter used only by Consumption dialogues, combined with the interaction's own filter.")]
        public DialogueConsumptionFilter Consumption = new DialogueConsumptionFilter();
        [Tooltip("Pages displayed in this order. Each advance input moves to the next page, or closes the last one.")]
        public DialogueLine[] Lines = { new DialogueLine() };

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks this shared entry using the consuming interaction's active branches when known.</summary>
        /// <param name="warning">Receives invalid text, conditions or selection weight.</param>
        /// <param name="consumption">Whether this interaction uses consumption eligibility.</param>
        /// <param name="weighted">Whether this interaction uses weighted selection.</param>
        /// <param name="orders">Optional local order catalog.</param>
        /// <param name="bound">Whether the destination object's filter mode is known.</param>
        /// <returns>True when every active setting and page is complete.</returns>
        public bool TryValidate(out string warning, bool consumption = true, bool weighted = true, OrderSettings orders = null, bool bound = false)
        {
            warning = "Each dialogue entry needs at least one complete text page.";
            if (Lines == null || Lines.Length == 0)
                return false;
            foreach (DialogueLine line in Lines)
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                    return false;
            warning = "Dialogue weights must be finite and non-negative.";
            if (weighted && (!float.IsFinite(Weight) || Weight < 0f))
                return false;
            warning = "Configure the entry's consumption filter.";
            if (consumption && (Consumption == null || !Consumption.TryValidate(orders, out warning, !bound)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
