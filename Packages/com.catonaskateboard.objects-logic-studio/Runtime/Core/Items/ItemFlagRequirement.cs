using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Groups alternative identity flags under one quantity, counting each physical item once.</summary>
    [Serializable]
    public class ItemFlagRequirement
    {
        #region Fields

        [Header("Flag and Quantity")]
        [Tooltip("Alternative identity flags accepted by this condition. Any selected flag qualifies an item.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Positive whole number of matching units required. A Grab contributes its configured Units; other items count as one.")]
        public int Count = 1;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks an alternative group before recipe or dialogue evaluation.</summary>
        /// <param name="warning">Receives invalid membership or quantity.</param>
        /// <returns>True when the alternatives are distinct and the quantity is positive.</returns>
        public bool TryValidate(out string warning)
        {
            // An empty group never acts as an unrestricted condition.
            warning = "Use a positive whole-number quantity.";
            return Count > 0 && ObjectFlagRules.TryValidate(Flags, false, out warning);
        }

        #endregion

        #endregion
    }
}
