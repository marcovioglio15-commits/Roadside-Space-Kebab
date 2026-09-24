using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies a reusable contact or dialogue configuration imported as an independent item draft.</summary>
    public abstract class ExtendedInteractionPreset : ScriptableObject
    {
        #region Properties

        /// <summary>Feature accepting this preset type.</summary>
        public abstract ExtendedInteractionKind Kind { get; }

        #endregion
    }
}
