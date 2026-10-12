using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Combines optional object identity, spawn and consumption conditions without side effects.</summary>
    [Serializable]
    public sealed class DialogueCondition
    {
        #region Fields
        [Header("Conditions")]
        [Tooltip("Require every enabled condition. When disabled, any enabled condition is sufficient.")]
        public bool RequireAll = true;
        [Tooltip("Invert the combined result. An empty condition otherwise passes.")]
        public bool Invert;
        [Tooltip("Check identity flags on the object owning this dialogue.")]
        public bool CheckFlags;
        [Tooltip("Identity flags accepted by this condition.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require all selected identity flags instead of any one of them.")]
        public bool AllFlags = true;
        [Tooltip("Restrict this condition to a range of prefab spawn occurrences.")]
        public bool CheckSpawn;
        [Tooltip("Inclusive first spawn accepted by this condition.")]
        public int FirstSpawn = 1;
        [Tooltip("Inclusive last spawn; zero leaves the upper bound open.")]
        public int LastSpawn;
        [Tooltip("Optional order or consumed-flag condition, independent of the interaction trigger.")]
        public DialogueConsumptionFilter Consumption = new DialogueConsumptionFilter();
        #endregion
        #region Methods
        #region Validation
        /// <summary>Checks enabled conditions without changing the authored proposal.</summary>
        /// <param name="orders">Available order sources, or null for unbound authoring.</param>
        /// <param name="bound">Whether the destination object is known.</param>
        /// <param name="warning">Receives the first invalid condition.</param>
        /// <returns>True when all active predicates can be evaluated.</returns>
        internal bool TryValidate(OrderSettings orders, bool bound, out string warning)
        {
            warning = "Use a positive first spawn and a last spawn of zero or at least the first spawn.";
            if (CheckSpawn && (FirstSpawn <= 0 || LastSpawn < 0 || LastSpawn > 0 && LastSpawn < FirstSpawn))
                return false;
            if (CheckFlags && !ObjectFlagRules.TryValidate(Flags, false, out warning))
                return false;
            warning = "Configure the consumption condition.";
            return Consumption != null && Consumption.TryValidate(orders, out warning, !bound);
        }
        #endregion
        #endregion
    }
}
