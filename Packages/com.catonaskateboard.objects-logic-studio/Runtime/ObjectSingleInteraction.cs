using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies the mutually exclusive single-action feature cards.</summary>
    public enum SingleInteractionKind { Grab, Drop, Throw }

    /// <summary>Registers one button-triggered object feature with the shared scene observer.</summary>
    public abstract class ObjectSingleInteraction : ObjectInteraction
    {
        #region Serialized Fields

        [Header("Settings Preset")]
        [Tooltip("Reusable settings asset selected in Objects Logic Studio. Update and Apply write this feature's current settings to it.")]
        [SerializeField]
        private SingleInteractionPreset settingsPreset;

        [Header("Input")]
        [Tooltip("Button action resolved by its stable ID in the Observer player's PlayerInput. Its map must already be enabled.")]
        [SerializeField]
        private InputActionReference action;

        #endregion

        #region Properties

        /// <summary>Reusable preset assigned by the editor; runtime behavior uses the local settings snapshot.</summary>
        public SingleInteractionPreset SettingsPreset => settingsPreset;

        /// <summary>Imported button reference selected through the tool's map/action menu.</summary>
        public InputActionReference Action => action;
        /// <summary>Feature represented by this component.</summary>
        public abstract SingleInteractionKind Kind { get; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Registers this feature once when its component becomes available.</summary>
        protected virtual void OnEnable()
        {
            // An observer binds input later, after PlayerInput has initialized its private action copy.
            SingleInteractionRegistry.Register(this);
        }

        /// <summary>Removes this feature before disabled objects can consume input.</summary>
        protected virtual void OnDisable()
        {
            // The registry revision invalidates cached bindings at the next observer update.
            SingleInteractionRegistry.Unregister(this);
        }

        #endregion

        #region Validation

        /// <summary>Checks the assigned action and all feature-specific configuration.</summary>
        /// <param name="warning">Receives the first missing reference or invalid setting.</param>
        /// <returns>True when this feature can be used at runtime.</returns>
        public bool TryValidate(out string warning)
        {
            // Asset actions are only identifiers; runtime input always comes from PlayerInput.
            warning = "Assign an imported Button action from the player's Input Actions asset.";
            return action != null && action.action != null && action.action.type == InputActionType.Button
                && TryValidateSettings(out warning);
        }

        /// <summary>Checks the settings and authored components required by a concrete feature.</summary>
        /// <param name="warning">Receives a feature-specific issue.</param>
        /// <returns>True when the feature's configuration is complete.</returns>
        protected abstract bool TryValidateSettings(out string warning);

        #endregion

        #endregion
    }
}
