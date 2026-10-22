using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares a single occurrence between all spawn-dependent interactions on an item.</summary>
    internal sealed class ObjectSpawnLifetime
    {
        #region State
        private GameObject source;
        private int generation = -1;
        private bool held;
        /// <summary>Reserved occurrence, or zero before the item's next activation.</summary>
        internal int Index { get; private set; }
        /// <summary>Whether inventory or spawn animation currently retains this same logical lifetime.</summary>
        internal bool IsRetained => held;
        #endregion
        #region Methods
        #region Lifetime
        /// <summary>Reserves once, even when several dialogues and orders initialize in different callback orders.</summary>
        /// <param name="owner">Actual item root for direct scene lifetimes.</param>
        /// <param name="prefab">Source known by the instantiation boundary.</param>
        /// <param name="occurrence">Already reserved hierarchy occurrence, or zero for pooling.</param>
        internal void Prepare(GameObject owner, GameObject prefab, int occurrence)
        {
            if (generation != ObjectSpawnHistory.Generation)
            {
                Index = 0;
                held = false;
                generation = ObjectSpawnHistory.Generation;
            }
            if (Index > 0)
                return;
            if (prefab != null)
                source = prefab;
            Index = occurrence > 0 ? occurrence : ObjectSpawnHistory.Next(source != null ? source : owner);
        }

        /// <summary>Distinguishes temporary inventory or animation deactivation from a new pooled lifetime.</summary>
        /// <param name="value">Whether a suspension transaction retains this lifetime.</param>
        internal void Hold(bool value)
        {
            held = value;
        }

        /// <summary>Releases only an actual inactive root, preserving feature-level enable changes.</summary>
        /// <param name="owner">Item leaving the active hierarchy.</param>
        internal void Disable(GameObject owner)
        {
            if (!held && !owner.activeInHierarchy)
                Index = 0;
        }
        #endregion
        #endregion
    }
}
