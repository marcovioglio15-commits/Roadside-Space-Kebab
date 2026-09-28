using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Maps one linked prefab component identity to the instance root Unity remaps when cloning.</summary>
    [Serializable]
    internal sealed class CompletionSourceBinding
    {
        #region Fields

        [Tooltip("Stable asset GUID and component file ID, prepared by the spawn rule's Apply transaction.")]
        public string Identity = string.Empty;
        [Tooltip("Owning source prefab root, remapped independently for every spawned or scene-placed instance.")]
        public Transform Root;

        #endregion
    }
}
