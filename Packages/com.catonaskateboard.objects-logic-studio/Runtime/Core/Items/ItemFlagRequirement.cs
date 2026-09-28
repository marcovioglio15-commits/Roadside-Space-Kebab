using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Pairs a project flag with the positive quantity required by an item condition.</summary>
    [Serializable]
    public class ItemFlagRequirement
    {
        #region Fields

        [Header("Flag and Quantity")]
        [Tooltip("Project flag identifying the items counted by this condition.")]
        public ObjectFlag Flag;
        [Tooltip("Positive whole number of matching units required. A Grab contributes its configured Units; other items count as one.")]
        public int Count = 1;

        #endregion
    }
}
