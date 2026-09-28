using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Requires committed completions of one existing interaction on a source prefab instance.</summary>
    [Serializable]
    public sealed class SpawnCondition
    {
        #region Fields

        [Tooltip("Existing interaction selected by name from the linked source prefab.")]
        public ObjectInteraction Source;
        [Tooltip("Number of successful completions required from this same source instance.")]
        public int Count = 1;
        [Tooltip("Stable prefab component identity prepared by the editor when this rule is applied.")]
        [HideInInspector]
        public string SourceId = string.Empty;

        #endregion
    }

    /// <summary>Pairs one spawnable prefab with its relative random selection weight.</summary>
    [Serializable]
    public sealed class SpawnChoice
    {
        #region Fields

        [Tooltip("Prefab root created when this entry is drawn.")]
        public GameObject Prefab;
        [Tooltip("Relative weighted-random probability. Zero excludes this entry from weighted draws.")]
        public float Weight = 1f;

        #endregion
    }

    /// <summary>Selects the rule used to choose among configured spawn prefabs.</summary>
    public enum SpawnSelection { WeightedRandom, UniformRandom, Sequence }
}
