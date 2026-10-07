using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits one reusable modification independently of the interaction that references it.</summary>
    public sealed class ContactModificationWindow : StudioAssetWindow
    {
        #region Properties

        /// <summary>Shared definition type accepted by this window.</summary>
        protected override System.Type AssetType => typeof(ContactModificationDefinition);

        #endregion
        #region Methods
        #region Window

        /// <summary>Opens a definition in its own Apply/Discard session.</summary>
        /// <param name="asset">Shared modification to edit.</param>
        public static void Open(ContactModificationDefinition asset)
        {
            GetWindow<ContactModificationWindow>("Contact Modification").Select(asset);
        }

        /// <summary>Checks the complete detached proposal before saving the shared asset.</summary>
        /// <param name="proposal">Detached definition containing pending changes.</param>
        /// <param name="issue">Receives invalid flags, timing or effects.</param>
        /// <returns>True when the modification is complete.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((ContactModificationDefinition)proposal).TryValidate(out issue);
        }

        #endregion
        #endregion
    }
}
