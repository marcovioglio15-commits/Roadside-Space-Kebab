using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Defines the same current-object scope for catalog, presets and quick navigation.</summary>
    internal static class ObjectWorkspaceContext
    {
        #region Methods

        #region Scope

        /// <summary>Resolves the explicit browsing object without using unrelated scene selections.</summary>
        /// <param name="state">Workspace retaining the prefab picker.</param>
        /// <returns>The selected object, or null when the picker is empty.</returns>
        internal static GameObject Target(ObjectWorkspace state)
        {
            return state.Prefab != null ? state.BrowseTarget.Resolve() ?? state.Target.Resolve() : null;
        }

        /// <summary>Lists the selected branch's cards, falling back to its closest interaction owner.</summary>
        /// <param name="state">Workspace retaining child-object visibility.</param>
        /// <returns>The current object's components.</returns>
        internal static ObjectInteraction[] Interactions(ObjectWorkspace state)
        {
            return Interactions(state, Target(state), out _);
        }

        /// <summary>Shows the nearest interaction owner when a mesh-only branch has no cards of its own.</summary>
        /// <param name="state">Workspace retaining the current hierarchy filter.</param>
        /// <param name="target">Explicitly selected prefab branch.</param>
        /// <param name="owner">Branch whose cards appear in the catalog.</param>
        /// <returns>Interactions in scope, including disabled cards.</returns>
        internal static ObjectInteraction[] Interactions(ObjectWorkspace state, GameObject target, out GameObject owner)
        {
            owner = target;
            if (target == null)
                return Array.Empty<ObjectInteraction>();

            // Keep object editing on the selected branch. Only the catalog falls back to its nearest parent with cards.
            ObjectInteraction[] features = state.Navigation.IncludeChildren
                ? target.GetComponentsInChildren<ObjectInteraction>(true) : target.GetComponents<ObjectInteraction>();
            for (Transform branch = target.transform.parent; features.Length == 0 && branch != null; branch = branch.parent)
            {
                features = branch.GetComponents<ObjectInteraction>();
                if (features.Length > 0)
                    owner = branch.gameObject;
            }
            return features;
        }

        #endregion

        #endregion
    }
}
