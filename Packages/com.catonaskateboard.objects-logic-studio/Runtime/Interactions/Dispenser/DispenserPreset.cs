using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable Dispenser settings independently of the object's input binding.</summary>
    [CreateAssetMenu(fileName = "Dispenser Preset", menuName = "Objects Logic Studio/Dispenser Preset")]
    public sealed class DispenserPreset : SingleInteractionPreset
    {
        #region Fields

        [Header("Dispenser")]
        [Tooltip("Configuration copied by Import and written by Update or Apply.")]
        public DispenserSettings Settings = new DispenserSettings();

        #endregion

        #region Properties

        /// <summary>Limits this asset to the matching interaction card.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Dispenser;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates the snapshot before replacing an object's settings.</summary>
        /// <param name="warning">Receives the first configuration issue.</param>
        /// <returns>True when the snapshot is complete.</returns>
        public override bool TryValidate(out string warning)
        {
            // Missing serialized settings cannot overwrite an existing configuration.
            warning = "Dispenser preset settings are missing.";
            return Settings != null && Settings.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
