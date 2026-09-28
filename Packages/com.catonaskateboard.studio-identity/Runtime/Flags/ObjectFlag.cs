using UnityEngine;

namespace CatOnASkateboard.StudioIdentity
{
    /// <summary>Defines one extensible enum value whose asset identity survives display-name and group changes.</summary>
    [CreateAssetMenu(fileName = "Object Flag", menuName = "Studio Identity/Object Flag")]
    public sealed class ObjectFlag : ScriptableObject
    {
        #region Fields

        [Header("Object Flag")]
        [Tooltip("Unique name shown in object identity selectors. Renaming preserves all asset references.")]
        public string DisplayName = "New Flag";
        [Tooltip("Optional group used to filter selectors, for example Ingredients or Characters.")]
        public string Group = string.Empty;
        [Tooltip("Short explanation shown when choosing this flag.")]
        [TextArea(1, 3)]
        public string Description = string.Empty;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks editable labels without rewriting them during validation.</summary>
        /// <param name="warning">Receives an incomplete flag name.</param>
        /// <returns>True when the flag can be shown and selected.</returns>
        public bool TryValidate(out string warning)
        {
            // Asset references carry identity; the label only needs a meaningful value.
            warning = string.IsNullOrWhiteSpace(DisplayName) ? "Give this object flag a name." : string.Empty;
            return warning.Length == 0;
        }

        /// <summary>Reports invalid authoring without changing serialized values.</summary>
        private void OnValidate()
        {
            // Native Undo retains the exact text entered in the Inspector.
            if (!TryValidate(out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
