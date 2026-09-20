using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>
    /// Selects the presets used by a player through its active slots.
    /// Multiple masters can refer to the same Body preset.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMaster", menuName = "Player Studio/Master Preset")]
    public sealed class PlayerMasterPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Active Presets")]
        [Tooltip("Body preset used by this master. Assign an asset to define the player's capsule dimensions.")]
        [SerializeField]
        private PlayerBodyPreset bodyPreset;

        [Tooltip("Optional Input mapping used by an explicitly connected Player Input Bridge. Leave empty for a player without local input.")]
        [SerializeField]
        private PlayerInputPreset inputPreset;

        [Tooltip("Optional movement configuration captured by a connected motor at initialization. Body-only players can leave this empty.")]
        [SerializeField]
        private PlayerLocomotionPreset locomotionPreset;

        [Tooltip("Optional visual source and local offset shared by players using this master.")]
        [SerializeField]
        private PlayerVisualPreset visualPreset;

        [Tooltip("Optional camera, follow and view-relative movement configuration.")]
        [SerializeField]
        private PlayerCameraPreset cameraPreset;

        #endregion

        #region Properties

        /// <summary>The active Body asset; tools can inspect its identity without changing the slot.</summary>
        public PlayerBodyPreset BodyPreset => bodyPreset;

        /// <summary>The optional action mapping; Body-only players do not require input.</summary>
        public PlayerInputPreset InputPreset => inputPreset;

        /// <summary>The optional movement configuration; an existing motor explicitly chooses to consume it.</summary>
        public PlayerLocomotionPreset LocomotionPreset => locomotionPreset;

        /// <summary>The optional visual configuration; assigning it does not create a scene instance.</summary>
        public PlayerVisualPreset VisualPreset => visualPreset;

        /// <summary>The optional view configuration consumed by an explicitly connected camera rig.</summary>
        public PlayerCameraPreset CameraPreset => cameraPreset;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>
        /// Resolves the active Body slot and forwards its validation result to the caller.
        /// </summary>
        /// <param name="settings">Receives the active body's dimensions, or the default value on failure.</param>
        /// <param name="warning">Receives a missing-reference or dimension warning when resolution fails.</param>
        /// <returns>True when an assigned Body preset provides valid dimensions.</returns>
        public bool TryGetBodySettings(out PlayerBodySettings settings, out string warning)
        {
            // Stop at a missing slot instead of creating an implicit default asset.
            if (bodyPreset == null)
            {
                settings = default;
                warning = "Assign a Body preset to this Player Master preset.";
                return false;
            }

            // Keep the dimension rules in the Body preset, where the values belong.
            return bodyPreset.TryGetSettings(out settings, out warning);
        }

        #endregion

        #region Unity Callbacks

        /// <summary>
        /// Reports an incomplete or invalid Body slot after an Inspector change.
        /// </summary>
        private void OnValidate()
        {
            // Reuse resolution so Inspector checks and player initialization agree.
            if (!TryGetBodySettings(out _, out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
