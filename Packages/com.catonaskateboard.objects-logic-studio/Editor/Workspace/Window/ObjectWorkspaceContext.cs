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

        /// <summary>Lists only interactions inside the selected object's requested scope.</summary>
        /// <param name="state">Workspace retaining child-object visibility.</param>
        /// <returns>The current object's components.</returns>
        internal static ObjectInteraction[] Interactions(ObjectWorkspace state)
        {
            GameObject target = Target(state);
            return target == null ? Array.Empty<ObjectInteraction>() : state.Navigation.IncludeChildren
                ? target.GetComponentsInChildren<ObjectInteraction>(true) : target.GetComponents<ObjectInteraction>();
        }

        #endregion

        #endregion
    }
}
