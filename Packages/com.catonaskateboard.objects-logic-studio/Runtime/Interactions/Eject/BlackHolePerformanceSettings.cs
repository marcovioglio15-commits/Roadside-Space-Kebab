using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Bounds acquisition work and route queries independently of the surrounding object population.</summary>
    [Serializable]
    public sealed class BlackHolePerformanceSettings
    {
        #region Fields

        [Header("Population")]
        [Tooltip("Maximum distinct bodies admitted during one activation. Nearest eligible bodies are considered first; excess bodies remain unaffected until another activation.")]
        [Range(1, 2048)]
        public int MaximumParticipants = 128;
        [Tooltip("Maximum new bodies whose physics can be acquired in one physics step.")]
        [Range(1, 128)]
        public int AdmissionsPerStep = 16;
        [Tooltip("Maximum queued candidates whose filters and availability can be checked in one physics step.")]
        [Range(16, 1024)]
        public int CandidatesPerStep = 64;
        [Header("Navigation")]
        [Tooltip("Maximum complete route searches shared by all participants in one physics step.")]
        [Range(1, 8)]
        public int RouteSearchesPerStep = 2;
        [Tooltip("Seconds between full route visibility checks for each body. Immediate contact sweeps still run every physics step.")]
        [Range(0.02f, 0.5f)]
        public float RouteRefreshInterval = 0.1f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Rejects unusable work limits without rewriting the configured values.</summary>
        /// <param name="warning">Receives the invalid capacity or scheduling setting.</param>
        /// <returns>True when every budget permits finite progress.</returns>
        public bool TryValidate(out string warning)
        {
            // Budgets smaller than the population intentionally spread acquisition over multiple steps.
            warning = "Black Hole needs positive population, admission, candidate and route budgets, and a finite positive route refresh interval.";
            if (MaximumParticipants < 1 || AdmissionsPerStep < 1 || CandidatesPerStep < 1
                || RouteSearchesPerStep < 1 || !InteractionValues.Positive(RouteRefreshInterval))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
