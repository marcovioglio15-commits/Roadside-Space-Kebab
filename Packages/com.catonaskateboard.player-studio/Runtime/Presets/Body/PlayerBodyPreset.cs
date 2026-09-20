using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>
    /// Stores reusable capsule dimensions selected by a Player Master preset.
    /// Validation reports invalid values without replacing them.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerBody", menuName = "Player Studio/Body Preset")]
    public sealed class PlayerBodyPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Capsule Dimensions")]
        [Tooltip("Capsule radius in metres. Must be finite and greater than zero.")]
        [SerializeField]
        private float radius = 0.35f;

        [Tooltip("Total height in metres, including the rounded ends. Must be finite and at least twice the radius.")]
        [SerializeField]
        private float height = 1.8f;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>
        /// Checks the dimensions before a player or an editor preview reads them.
        /// A failed result leaves the asset untouched.
        /// </summary>
        /// <param name="settings">Receives valid dimensions, or the default value on failure.</param>
        /// <param name="warning">Receives the reason for failure, or an empty string on success.</param>
        /// <returns>True when both dimensions describe a valid capsule.</returns>
        public bool TryGetSettings(out PlayerBodySettings settings, out string warning)
        {
            // Presets and drafts use the same rules without exposing writable fields.
            return PlayerBodySettings.TryCreate(radius, height, out settings, out warning);
        }

        #endregion

        #region Unity Callbacks

        /// <summary>
        /// Reports invalid Inspector edits without correcting the entered dimensions.
        /// </summary>
        private void OnValidate()
        {
            // Keep validation local; no assets or scene objects are changed here.
            if (!TryGetSettings(out _, out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
