using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects an exact appearance or a persistent catalog change from that appearance onward.</summary>
    public enum OrderSpawnMatch { Exact, From }

    /// <summary>Overrides the default catalog for a one-based prefab spawn occurrence.</summary>
    [Serializable]
    public sealed class OrderSpawnCatalog
    {
        #region Fields

        [Header("Spawn Catalog")]
        [Tooltip("Exact overrides a single spawn. From remains active until a later threshold; exact rules take priority.")]
        public OrderSpawnMatch Match;
        [Tooltip("One-based spawn number for this prefab across the current play session, including Day Flow scene changes.")]
        public int Spawn = 1;
        [Tooltip("Catalog drawn when this rule wins. The default catalog applies when no rule matches.")]
        public OrderCatalog Catalog;

        #endregion
    }
}
