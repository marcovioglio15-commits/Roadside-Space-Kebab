using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable Grab targeting, carry and contact settings.</summary>
    [CreateAssetMenu(fileName = "Grab Preset", menuName = "Objects Logic Studio/Grab Preset")]
    public sealed class GrabPreset : SingleInteractionPreset
    {
        #region Fields

        [Header("Grab")]
        [Tooltip("Settings copied into an item's Grab draft when importing this preset.")]
        public GrabSettings Settings = new GrabSettings();

        #endregion

        #region Properties

        /// <summary>Only Grab cards can import this asset.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Grab;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks targeting and carry settings before reuse.</summary>
        /// <param name="warning">Receives invalid or missing settings.</param>
        /// <returns>True when the complete Grab snapshot is valid.</returns>
        public override bool TryValidate(out string warning)
        {
            // A missing serialized block cannot replace a working item configuration.
            warning = "Grab preset settings are missing.";
            return Settings != null && Settings.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
