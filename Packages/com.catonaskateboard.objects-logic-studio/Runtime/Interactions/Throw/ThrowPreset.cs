using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable Throw body, surface and launch settings as one dedicated asset.</summary>
    [CreateAssetMenu(fileName = "Throw Preset", menuName = "Objects Logic Studio/Throw Preset")]
    public sealed class ThrowPreset : SingleInteractionPreset
    {
        #region Fields

        [Header("Throw")]
        [Tooltip("Body and surface settings copied into an item's Throw draft on import.")]
        public ReleaseSettings Settings = new ReleaseSettings();
        [Tooltip("Launch direction, strength and spin copied with this Throw preset.")]
        public ThrowSettings Trajectory = new ThrowSettings();

        #endregion

        #region Properties

        /// <summary>Only Throw cards can import this asset.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Throw;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks both release physics and trajectory before reuse.</summary>
        /// <param name="warning">Receives invalid or missing settings.</param>
        /// <returns>True when the complete Throw snapshot is valid.</returns>
        public override bool TryValidate(out string warning)
        {
            // Both blocks travel together so imported throws retain their mass and launch behavior.
            warning = "Throw preset physics or trajectory is missing.";
            return Settings != null && Trajectory != null && Settings.TryValidate(out warning) && Trajectory.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
