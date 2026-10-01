using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains order source identities across prefab-stage closure and reopening.</summary>
    [Serializable]
    internal sealed class OrderDraft
    {
        #region Fields

        [Tooltip("Detached board ID and order text. Source references are resolved from stable prefab identities.")]
        public OrderSettings Settings = new OrderSettings();
        [Tooltip("Stable component identity for each selected consumption interaction.")]
        public long[] Sources = Array.Empty<long>();

        #endregion
        #region Methods
        #region Snapshots

        /// <summary>Copies order text while removing temporary stage references from the draft.</summary>
        /// <param name="settings">Applied local order configuration.</param>
        /// <returns>A detached proposal with stable component identities.</returns>
        internal static OrderDraft Capture(OrderSettings settings)
        {
            // Native prefab saves remap references; window drafts retain file IDs instead.
            OrderDraft draft = new OrderDraft { Settings = ObjectWorkspace.Copy(settings), Sources = new long[settings.Entries.Length] };
            for (int index = 0; index < settings.Entries.Length; index++)
            {
                draft.Sources[index] = settings.Entries[index].Source != null ? ObjectWorkspaceTarget.FileId(settings.Entries[index].Source) : 0;
                draft.Settings.Entries[index].Source = null;
            }
            return draft;
        }

        /// <summary>Resolves the same exact consuming components before validation and Apply.</summary>
        /// <param name="owner">Current prefab object owning the order interaction.</param>
        /// <returns>A fresh configuration containing current component references.</returns>
        internal OrderSettings Resolve(GameObject owner)
        {
            // Removed components remain missing rather than silently matching a duplicate name.
            OrderSettings result = ObjectWorkspace.Copy(Settings);
            ObjectContactModifier[] candidates = owner.GetComponents<ObjectContactModifier>();
            for (int index = 0; index < result.Entries.Length; index++)
                foreach (ObjectContactModifier candidate in candidates)
                    if (index < Sources.Length && Sources[index] != 0 && ObjectWorkspaceTarget.FileId(candidate) == Sources[index])
                    {
                        result.Entries[index].Source = candidate;
                        break;
                    }
            return result;
        }

        #endregion
        #endregion
    }
}
