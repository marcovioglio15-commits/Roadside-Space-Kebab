using System;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps page edits independent of the containing tree's draft.</summary>
    public sealed class DialogueEntryWindow : StudioAssetWindow
    {
        #region Properties

        protected override Type AssetType => typeof(DialogueEntry);

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens the shared text preset without committing its parent tree.</summary>
        /// <param name="entry">Page sequence to edit.</param>
        public static void Open(DialogueEntry entry)
        {
            GetWindow<DialogueEntryWindow>("Dialogue Entry").Select(entry);
        }

        /// <summary>Rejects empty text while preserving the editable proposal.</summary>
        /// <param name="proposal">Detached entry.</param>
        /// <param name="issue">Receives missing page text.</param>
        /// <returns>True when every page is complete.</returns>
        protected override bool Validate(UnityEngine.ScriptableObject proposal, out string issue)
        {
            return ((DialogueEntry)proposal).TryValidate(out issue);
        }

        #endregion

        #endregion
    }
}
