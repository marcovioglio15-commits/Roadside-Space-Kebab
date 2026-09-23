using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable Drop body and surface settings independently of Throw.</summary>
    [CreateAssetMenu(fileName = "Drop Preset", menuName = "Objects Logic Studio/Drop Preset")]
    public sealed class DropPreset : SingleInteractionPreset
    {
        #region Fields

        [Header("Drop")]
        [Tooltip("Body and surface settings copied into an item's Drop draft on import.")]
        public ReleaseSettings Settings = new ReleaseSettings();

        #endregion

        #region Properties

        /// <summary>Only Drop cards can import this asset.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Drop;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the released body and contact settings before reuse.</summary>
        /// <param name="warning">Receives invalid or missing settings.</param>
        /// <returns>True when the complete Drop snapshot is valid.</returns>
        public override bool TryValidate(out string warning)
        {
            // Throw presets intentionally use a separate asset type and configuration.
            warning = "Drop preset settings are missing.";
            return Settings != null && Settings.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
