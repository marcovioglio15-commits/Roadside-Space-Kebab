using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a scene-local hierarchy and card selection without retaining a scene component in a plan asset.</summary>
    [Serializable]
    public sealed class SceneInteractionReference
    {
        #region Fields

        [Tooltip("Unique hierarchy path from the scene root to the object containing the interaction.")]
        public string Path = string.Empty;
        [Tooltip("Exact interaction card on the selected scene object.")]
        public LocalInteractionReference Interaction = new LocalInteractionReference();

        #endregion

        #region Properties

        /// <summary>Whether this selection has enough information to resolve at a day boundary.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Path) && Interaction is { IsValid: true };

        #endregion

        #region Methods
        #region Resolution

        /// <summary>Resolves only the day's scene, including inactive objects, once at the relevant day boundary.</summary>
        /// <param name="scene">Loaded gameplay scene owning this day's interactions.</param>
        /// <returns>The exact card, or null for a missing or ambiguous hierarchy.</returns>
        public ObjectInteraction Resolve(Scene scene)
        {
            if (!IsValid || !scene.IsValid() || !scene.isLoaded)
                return null;
            int separator = Path.IndexOf('/');
            string rootName = separator < 0 ? Path : Path.Substring(0, separator);
            Transform root = null;
            // Duplicate root names are rejected instead of selecting an arbitrary scene object.
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == rootName)
                {
                    if (root != null)
                        return null;
                    root = candidate.transform;
                }
            Transform target = PlayerHierarchy.Resolve(root, separator < 0 ? string.Empty : Path.Substring(separator + 1));
            return target != null ? Interaction.Resolve(target.gameObject) : null;
        }

        #endregion
        #endregion
    }
}
