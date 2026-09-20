using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares validation between module drafts, Inspectors and player creation.</summary>
    internal static class PlayerModuleValidation
    {
        #region Methods

        #region Validation

        /// <summary>Validates supported module data without modifying or normalizing it.</summary>
        /// <param name="asset">Persistent asset or temporary Editor proposal.</param>
        /// <param name="warning">Receives the first configuration incompatibility.</param>
        /// <returns>True when the module supplies a usable configuration.</returns>
        public static bool TryValidate(ScriptableObject asset, out string warning)
        {
            // Type-specific rules remain in their runtime configuration contracts.
            warning = string.Empty;
            switch (asset)
            {
                case PlayerCameraPreset camera:
                    return camera.TryGetSettings(out _, out warning);
                case PlayerInputPreset input:
                    return input.TryGetMovementId(out _, out warning) && input.TryGetJumpId(out _, out warning)
                        && input.TryGetLookIds(out _, out _, out _, out warning);
                default:
                    warning = "Assign an Input or Camera preset before editing this module.";
                    return false;
            }
        }

        #endregion

        #endregion
    }
}
