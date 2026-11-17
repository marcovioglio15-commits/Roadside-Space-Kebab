using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Overrides the interval before one scene-board slot appears.</summary>
    [Serializable]
    public sealed class OrderSlotRevealDelay
    {
        #region Fields

        [Header("Slot Reveal")]
        [Tooltip("Use a separate interval before this slot instead of the board's shared delay.")]
        public bool Override;
        [Tooltip("Scaled seconds since the preceding slot appears. The first slot appears immediately.")]
        [Range(0f, 10f)]
        public float Delay;

        #endregion
    }
}
