using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies contact, dialogue, outline, unlock and assembly feature cards.</summary>
    public enum ExtendedInteractionKind { ModifyByContact, Dialogue, Outline, Unlock, AssemblyStation, AssemblyProduct, SpawnManagement, Slice }

    /// <summary>Shares validation and item ownership for interactions with persistent progress.</summary>
    [RequireComponent(typeof(ObjectItem))]
    public abstract class ObjectExtendedInteraction : ObjectInteraction
    {
        #region Serialized Fields

        [Header("Settings Preset")]
        [Tooltip("Reusable settings asset selected in Objects Logic Studio. Update and Apply write this feature's current settings to it.")]
        [SerializeField]
        private ExtendedInteractionPreset settingsPreset;

        [Header("Debug")]
        [Tooltip("Draw the interaction's range or contact bounds when the object is selected.")]
        [SerializeField]
        private bool drawGizmos = true;

        #endregion

        #region Properties

        /// <summary>Reusable preset assigned by the editor; runtime behavior uses the local settings snapshot.</summary>
        public ExtendedInteractionPreset SettingsPreset => settingsPreset;

        /// <summary>Feature represented by this component.</summary>
        public abstract ExtendedInteractionKind Kind { get; }
        /// <summary>Whether editor contact and distance guides are requested.</summary>
        public bool DrawGizmos => drawGizmos;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks authored dependencies before activation or an editor Apply.</summary>
        /// <param name="warning">Receives the first actionable configuration issue.</param>
        /// <returns>True when this feature can run without creating missing dependencies.</returns>
        public abstract bool TryValidate(out string warning);

        #endregion

        #endregion
    }
}
