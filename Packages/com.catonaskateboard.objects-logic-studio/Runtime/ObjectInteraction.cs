using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Provides a common component family for independently enabled interactions on one prefab.</summary>
    public abstract class ObjectInteraction : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Interaction")]
        [Tooltip("Name shown in Objects Logic Studio to distinguish interactions on the same object.")]
        [SerializeField]
        private string interactionName = "Interaction";

        #endregion

        #region Properties

        /// <summary>Identifies this component in the prefab workspace.</summary>
        public string InteractionName => interactionName;

        #endregion
    }
}
