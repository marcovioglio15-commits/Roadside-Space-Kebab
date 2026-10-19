using UnityEngine;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Exposes shared fade timing from a flow asset without coupling Menu Studio to its gameplay package.</summary>
    public abstract class MenuTransitionProfile : ScriptableObject
    {
        #region Properties

        /// <summary>Unscaled seconds spent covering the current scene.</summary>
        public abstract float FadeOutSeconds { get; }
        /// <summary>Minimum unscaled seconds held at black after loading.</summary>
        public abstract float BlackSeconds { get; }
        /// <summary>Unscaled seconds spent revealing the destination.</summary>
        public abstract float FadeInSeconds { get; }

        #endregion
    }
}
