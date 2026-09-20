using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores camera and view-relative presentation options independently of scene references.</summary>
    [CreateAssetMenu(fileName = "PlayerCamera", menuName = "Player Studio/Camera Preset")]
    public sealed class PlayerCameraPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Camera")]
        [Tooltip("View, follow, lens and presentation configuration. Scene references belong to the Camera Rig.")]
        [SerializeField]
        private PlayerCameraSettings settings = PlayerCameraSettings.Default;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>Returns a validated copy for initialization or Editor confirmation.</summary>
        /// <param name="value">Receives the saved configuration.</param>
        /// <param name="warning">Receives invalid active settings without modifying the asset.</param>
        /// <returns>True when the configured behavior is usable.</returns>
        public bool TryGetSettings(out PlayerCameraSettings value, out string warning)
        {
            // Runtime consumers retain this copy instead of rereading the asset every frame.
            value = settings;
            return settings.TryValidate(out warning);
        }

        #endregion

        #endregion
    }
}
