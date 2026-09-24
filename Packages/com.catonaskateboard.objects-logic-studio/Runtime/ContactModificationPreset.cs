using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable contact timing, effects and temporary restrictions.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Modify By Contact Preset")]
    public sealed class ContactModificationPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Modify By Contact")]
        [Tooltip("Settings copied into a contact interaction when Import is used.")]
        public ContactModificationSettings Settings = new ContactModificationSettings();

        #endregion

        #region Properties

        /// <summary>Contact modification accepts this preset.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ModifyByContact;

        #endregion
    }
}
