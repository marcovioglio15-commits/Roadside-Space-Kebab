using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies a reusable settings snapshot for one interaction, independent of an item's bindings.</summary>
    public abstract class SingleInteractionPreset : ScriptableObject
    {
        #region Properties

        /// <summary>Interaction type accepted by this preset.</summary>
        public abstract SingleInteractionKind Kind { get; }

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the stored snapshot before importing or exporting settings.</summary>
        /// <param name="warning">Receives the first invalid value.</param>
        /// <returns>True when the settings can be reused.</returns>
        public abstract bool TryValidate(out string warning);

        /// <summary>Reports invalid direct asset edits without correcting the saved values.</summary>
        protected void OnValidate()
        {
            // Item bindings and collider requirements are checked separately when applying an import.
            if (!TryValidate(out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
