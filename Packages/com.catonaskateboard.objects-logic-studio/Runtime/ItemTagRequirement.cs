using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Pairs a project tag with the positive quantity required by an item condition.</summary>
    [Serializable]
    public class ItemTagRequirement
    {
        #region Fields

        [Header("Tag and Quantity")]
        [Tooltip("Project tag identifying the items counted by this condition.")]
        public string Tag = "Untagged";
        [Tooltip("Positive whole number of matching items required by this condition.")]
        public int Count = 1;

        #endregion
    }
}
