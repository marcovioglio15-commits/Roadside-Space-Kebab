using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one flag-filtered modification between contact interactions on different objects.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Contact Modification")]
    public sealed class ContactModificationDefinition : ScriptableObject
    {
        #region Fields

        [Header("Contact Modification")]
        [Tooltip("Reusable contact flags, timing, participant effects and temporary restrictions.")]
        public ContactModificationRule Settings = new ContactModificationRule();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks this reusable modification without requiring destination geometry.</summary>
        /// <param name="warning">Receives invalid settings without replacing authored values.</param>
        /// <returns>True when this modification is complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Configure this contact modification.";
            return Settings != null && Settings.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
