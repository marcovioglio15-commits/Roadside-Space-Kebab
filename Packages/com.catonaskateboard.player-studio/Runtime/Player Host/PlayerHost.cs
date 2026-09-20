using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>
    /// Connects a scene object to its master and captures body settings when Play starts.
    /// Optionally configures an existing CharacterController without moving or creating scene objects.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player Studio/Player Host")]
    public sealed class PlayerHost : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Configuration")]
        [Tooltip("Master preset used to read the body settings when this player initializes.")]
        [SerializeField]
        private PlayerMasterPreset masterPreset;

        [Header("Integration")]
        [Tooltip("Configuration Only reads the preset. Character Controller applies geometry at initialization and when Player Studio confirms edits for this loaded scene instance.")]
        [SerializeField]
        private PlayerBodyBinding bodyBinding;

        [Tooltip("Existing CharacterController on this GameObject. Required only by the Character Controller binding; never created automatically.")]
        [SerializeField]
        private CharacterController bodyController;

        [Header("Debug")]
        [Tooltip("Draw the configured capsule in the Scene view when this player is selected and Gizmos are enabled.")]
        [SerializeField]
        private bool drawBodyGizmo = true;

        #endregion

        #region Runtime State

        private PlayerBodySettings bodySettings;
        private bool isInitialized;
        private string initializationWarning = "The player has not initialized yet.";

        #endregion

        #region Properties

        /// <summary>The applied master used to identify scene instances affected by a preset edit.</summary>
        public PlayerMasterPreset MasterPreset => masterPreset;

        /// <summary>Allows the editor drawer to respect the component's debug toggle.</summary>
        public bool DrawBodyGizmo => drawBodyGizmo;

        /// <summary>The explicit component binding selected for this host.</summary>
        public PlayerBodyBinding BodyBinding => bodyBinding;

        /// <summary>The assigned native capsule, retained when another binding is selected.</summary>
        public CharacterController BodyController => bodyController;

        #endregion

        #region Methods

        #region Unity Callbacks

        /// <summary>
        /// Captures one independent copy before this player is used during Play.
        /// </summary>
        private void Awake()
        {
            // Resolve once; later runtime reads use the stored settings.
            isInitialized = TryReadConfiguration(out bodySettings, out initializationWarning);

            // Configure only the existing component chosen for the validated native binding.
            if (isInitialized && bodyBinding == PlayerBodyBinding.CharacterController)
                PlayerCharacterControllerBody.ApplyValidated(bodyController, bodySettings);

            // Keep invalid configuration visible without rewriting the preset.
            if (!isInitialized)
                Debug.LogWarning(initializationWarning, this);
        }

        #endregion

        #region Configuration Access

        /// <summary>
        /// Reads current preset dimensions in Edit mode and the captured copy during Play.
        /// Callers must check the result before using the returned settings.
        /// </summary>
        /// <param name="settings">Receives the available dimensions; unusable when this method returns false.</param>
        /// <param name="warning">Receives the reason settings are unavailable, or an empty string on success.</param>
        /// <returns>True when valid settings are available in the current mode.</returns>
        public bool TryGetBodySettings(out PlayerBodySettings settings, out string warning)
        {
            // Edit mode previews changes immediately without storing runtime state.
            if (!Application.isPlaying)
                return TryReadConfiguration(out settings, out warning);

            // Runtime reads never revisit or modify the shared preset.
            settings = bodySettings;
            warning = initializationWarning;
            return isInitialized;
        }

        /// <summary>
        /// Resolves the active Body and checks the requirements of this object's selected binding.
        /// </summary>
        /// <param name="settings">Receives resolved dimensions; use them only when this method succeeds.</param>
        /// <param name="warning">Receives the first configuration or component issue, or an empty string on success.</param>
        /// <returns>True when the master, dimensions and selected body binding are valid.</returns>
        private bool TryReadConfiguration(out PlayerBodySettings settings, out string warning)
        {
            // Leave a safe output when a component-level requirement is missing.
            settings = default;

            // A player must use an explicitly selected master.
            if (masterPreset == null)
            {
                warning = "Assign a Player Master preset to this Player Host.";
                return false;
            }

            // Resolve the data before checking requirements of the selected representation.
            if (!masterPreset.TryGetBodySettings(out settings, out warning))
                return false;

            // Hidden component references remain stored but are used only by their selected binding.
            switch (bodyBinding)
            {
                case PlayerBodyBinding.ConfigurationOnly:
                    if (transform.lossyScale == Vector3.one)
                        return true;

                    warning = "Player Host requires world scale (1, 1, 1). Scale the visual child instead of the player or its parents.";
                    return false;
                case PlayerBodyBinding.CharacterController:
                    return PlayerCharacterControllerBody.TryValidate(bodyController, transform, settings, out warning);
                default:
                    warning = "Select a supported Body Binding on the Player Host.";
                    return false;
            }
        }

        #endregion

        #endregion
    }
}
