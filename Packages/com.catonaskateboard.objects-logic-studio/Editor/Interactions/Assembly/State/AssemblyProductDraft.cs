using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains recipe proposals and product-local feature identities after the native prefab stage closes.</summary>
    [Serializable]
    internal sealed class AssemblyProductDraft
    {
        #region Fields

        [Header("Assembly Proposal")]
        [Tooltip("Detached recipe and magnet layout. Interaction targets are stored separately by stable prefab identity.")]
        public AssemblyProductSettings Settings = new AssemblyProductSettings();
        [Tooltip("Existing product interaction identity corresponding to each ingredient rule.")]
        public long[] TargetIds = Array.Empty<long>();

        #endregion

        #region Methods

        #region Snapshots

        /// <summary>Replaces temporary stage references with stable file IDs before retaining a draft.</summary>
        /// <param name="source">Applied product configuration.</param>
        /// <returns>A detached proposal that survives stage close and editor reload.</returns>
        internal static AssemblyProductDraft Capture(AssemblyProductSettings source)
        {
            // Mesh and prefab assets remain persistent references; local components use file identities.
            AssemblyProductDraft draft = new AssemblyProductDraft
            {
                Settings = ObjectWorkspace.Copy(source),
                TargetIds = new long[source.InteractionRules.Length]
            };
            for (int index = 0; index < source.InteractionRules.Length; index++)
            {
                ObjectInteraction target = source.InteractionRules[index].Target;
                draft.TargetIds[index] = target != null ? ObjectWorkspaceTarget.FileId(target) : 0;
                draft.Settings.InteractionRules[index].Target = null;
            }
            return draft;
        }

        /// <summary>Resolves the same existing product features against the currently open prefab branch.</summary>
        /// <param name="root">Product root receiving the recipe.</param>
        /// <returns>A new runtime configuration with real prefab-local component references.</returns>
        internal AssemblyProductSettings Resolve(Transform root)
        {
            // Renames never redirect a rule; removed components remain missing until explicitly replaced.
            AssemblyProductSettings result = ObjectWorkspace.Copy(Settings);
            ObjectInteraction[] candidates = root.GetComponentsInChildren<ObjectInteraction>(true);
            for (int index = 0; index < result.InteractionRules.Length; index++)
            {
                result.InteractionRules[index].Target = null;
                if (index >= TargetIds.Length || TargetIds[index] == 0)
                    continue;
                foreach (ObjectInteraction candidate in candidates)
                    if (ObjectWorkspaceTarget.FileId(candidate) == TargetIds[index])
                    {
                        result.InteractionRules[index].Target = candidate;
                        break;
                    }
            }
            return result;
        }

        #endregion

        #endregion
    }
}
