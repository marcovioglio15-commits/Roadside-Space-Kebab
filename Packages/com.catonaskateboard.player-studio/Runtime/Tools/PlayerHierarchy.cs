using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Resolves unique hierarchy paths once when binding reusable tool presets.</summary>
    public static class PlayerHierarchy
    {
        #region Methods

        #region Resolution

        /// <summary>Resolves a model-local path without hierarchy searches during playback.</summary>
        /// <param name="model">Bound model root.</param>
        /// <param name="path">Relative child path, or empty for the model.</param>
        /// <returns>The existing target, or null for a missing binding.</returns>
        public static Transform Resolve(Transform model, string path)
        {
            // Optional visual modules never cause implicit object creation.
            if (model == null || string.IsNullOrEmpty(path))
                return model;
            foreach (string segment in path.Split('/'))
            {
                Transform match = null;
                for (int index = 0; index < model.childCount; index++)
                    if (model.GetChild(index).name == segment)
                    {
                        if (match != null)
                            return null;
                        match = model.GetChild(index);
                    }
                if (match == null)
                    return null;
                model = match;
            }
            return model;
        }

        #endregion

        #endregion
    }
}
