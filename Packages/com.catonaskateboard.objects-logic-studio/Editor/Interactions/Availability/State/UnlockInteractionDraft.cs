using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains prefab component identities independently of temporary native stage objects.</summary>
    [Serializable]
    internal sealed class UnlockInteractionDraft
    {
        #region Fields

        [Header("Unlock Proposal")]
        [Tooltip("Detached condition values; component references are represented by their stable prefab file IDs.")]
        public InteractionUnlockSettings Settings = new InteractionUnlockSettings();
        [Tooltip("Stable identity of the existing interaction to lock.")]
        public long TargetId;
        [Tooltip("Stable identity of the incoming interaction when Replace is selected.")]
        public long ReplacementId;
        [Tooltip("Stable source component identity for each condition.")]
        public long[] SourceIds = Array.Empty<long>();

        #endregion

        #region Methods

        #region Snapshots

        /// <summary>Captures local references as stable IDs before the native prefab stage closes.</summary>
        /// <param name="source">Applied unlock configuration.</param>
        /// <returns>A detached proposal containing no temporary stage references.</returns>
        internal static UnlockInteractionDraft Capture(InteractionUnlockSettings source)
        {
            // Unity remaps real references on prefab save; drafts must survive while that stage is closed.
            UnlockInteractionDraft draft = new UnlockInteractionDraft
            {
                Settings = ObjectWorkspace.Copy(source),
                TargetId = source.Target != null ? ObjectWorkspaceTarget.FileId(source.Target) : 0,
                ReplacementId = source.Replacement != null ? ObjectWorkspaceTarget.FileId(source.Replacement) : 0,
                SourceIds = new long[source.Conditions.Length]
            };
            draft.Settings.Target = null;
            draft.Settings.Replacement = null;
            for (int index = 0; index < source.Conditions.Length; index++)
            {
                draft.SourceIds[index] = source.Conditions[index].Source != null ? ObjectWorkspaceTarget.FileId(source.Conditions[index].Source) : 0;
                draft.Settings.Conditions[index].Source = null;
            }
            return draft;
        }

        /// <summary>Resolves retained identities against the current prefab before validation or Apply.</summary>
        /// <param name="root">Current native prefab root.</param>
        /// <returns>A new runtime configuration containing current component references.</returns>
        internal InteractionUnlockSettings Resolve(Transform root)
        {
            // Removed sources remain missing instead of redirecting to similarly named components.
            InteractionUnlockSettings result = ObjectWorkspace.Copy(Settings);
            ObjectInteraction[] candidates = root.GetComponentsInChildren<ObjectInteraction>(true);
            result.Target = Find(candidates, TargetId);
            result.Replacement = Find(candidates, ReplacementId);
            for (int index = 0; index < result.Conditions.Length; index++)
                result.Conditions[index].Source = index < SourceIds.Length ? Find(candidates, SourceIds[index]) : null;
            return result;
        }

        /// <summary>Finds one actual component using its saved prefab identity.</summary>
        /// <param name="candidates">Existing components in the prefab.</param>
        /// <param name="identity">Saved component file ID.</param>
        /// <returns>The same component or null after removal.</returns>
        private static ObjectInteraction Find(ObjectInteraction[] candidates, long identity)
        {
            // Unlock rules are configuration, never selectable source interactions.
            foreach (ObjectInteraction candidate in candidates)
                if (candidate is not ObjectInteractionUnlock && identity != 0 && ObjectWorkspaceTarget.FileId(candidate) == identity)
                    return candidate;
            return null;
        }

        #endregion

        #endregion
    }
}
