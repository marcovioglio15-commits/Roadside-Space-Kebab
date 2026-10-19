using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines one first-stage order pool and its candidate selection policy.</summary>
    [Serializable]
    public sealed class OrderPool
    {
        #region Fields

        [Header("Order Pool")]
        [Tooltip("Unique label used to identify this extraction pool in the catalog.")]
        public string Name = "Order Pool";
        [Tooltip("Relative chance of choosing this pool when it contains an eligible candidate.")]
        [Range(0f, 10f)]
        public float Weight = 1f;
        [Tooltip("Choose candidates inside this pool uniformly or by their effective weights.")]
        public OrderSelection Selection;
        [Tooltip("Order, variant or generated combo names available from this pool.")]
        public OrderPoolEntry[] Candidates = Array.Empty<OrderPoolEntry>();

        #endregion
    }

    /// <summary>References one named catalog candidate and optionally scales its weighted chance in this pool.</summary>
    [Serializable]
    public sealed class OrderPoolEntry
    {
        #region Fields

        [Tooltip("Exact order, variant or generated combo name supplied by the catalog.")]
        public string Candidate;
        [Tooltip("Multiplier applied to the candidate weight when this pool uses weighted selection.")]
        [Range(0f, 10f)]
        public float Weight = 1f;

        #endregion
    }

    /// <summary>Stores resolved candidates once per spawn for pool-first order extraction.</summary>
    internal sealed class OrderDrawPool
    {
        #region State

        internal readonly string Name;
        internal readonly float Weight;
        internal readonly OrderSelection Selection;
        internal readonly Dictionary<string, float> Candidates = new Dictionary<string, float>(StringComparer.Ordinal);

        #endregion
        #region Methods

        #region Construction

        /// <summary>Captures one validated pool's chance and internal selection mode.</summary>
        /// <param name="name">Pool label from the catalog.</param>
        /// <param name="weight">Relative chance among pools with eligible candidates.</param>
        /// <param name="selection">Uniform or weighted candidate mode.</param>
        internal OrderDrawPool(string name, float weight, OrderSelection selection)
        {
            Name = name;
            Weight = weight;
            Selection = selection;
        }

        /// <summary>Adds a resolved member using its pool-specific weight multiplier.</summary>
        /// <param name="candidate">Catalog candidate referenced by the pool entry.</param>
        /// <param name="weight">Multiplier authored on the pool entry.</param>
        internal void Add(OrderCandidate candidate, float weight)
        {
            Candidates.TryAdd(candidate.Name, candidate.Weight * weight);
        }

        #endregion
        #endregion
    }

}
