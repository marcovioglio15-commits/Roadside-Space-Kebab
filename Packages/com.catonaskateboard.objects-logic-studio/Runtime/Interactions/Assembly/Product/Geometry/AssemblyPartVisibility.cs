using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Temporarily hides ingredient geometry while preserving renderer and LOD configuration.</summary>
    internal sealed class AssemblyPartVisibility
    {
        #region State

        private readonly Renderer[] renderers;
        private readonly bool[] suppressed;
        private bool hidden;

        #endregion

        #region Methods

        #region Visibility

        /// <summary>Caches geometry once when a completed product first replaces this ingredient's visuals.</summary>
        /// <param name="root">Inserted ingredient root.</param>
        internal AssemblyPartVisibility(Transform root)
        {
            // Effects keep their own lifetime and never join the ingredient visibility group.
            List<Renderer> geometry = new List<Renderer>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer or SkinnedMeshRenderer
                    && renderer.GetComponentInParent<InteractionVfxInstance>(true) == null)
                    geometry.Add(renderer);
            renderers = geometry.ToArray();
            suppressed = new bool[renderers.Length];
        }

        /// <summary>Changes only rendering suppression at completion, rollback or detachment boundaries.</summary>
        /// <param name="value">Whether the final product mesh currently replaces ingredient geometry.</param>
        internal void SetHidden(bool value)
        {
            // Repeated completion checks must not overwrite the original visibility snapshot.
            if (hidden == value)
                return;
            hidden = value;
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                {
                    if (hidden)
                        suppressed[index] = renderers[index].forceRenderingOff;
                    renderers[index].forceRenderingOff = hidden || suppressed[index];
                }
        }

        #endregion

        #endregion
    }
}
