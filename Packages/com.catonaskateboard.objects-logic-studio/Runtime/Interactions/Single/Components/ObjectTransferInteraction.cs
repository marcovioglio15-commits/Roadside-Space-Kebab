using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares cached selection geometry for Container and Dispenser interactions.</summary>
    [RequireComponent(typeof(ObjectItem))]
    public abstract class ObjectTransferInteraction : ObjectSingleInteraction
    {
        #region Properties

        /// <summary>Targeting used by the observer when the bound action performs.</summary>
        public abstract TransferTargetSettings Target { get; }
        /// <summary>Collider snapshot captured before any inventory children are attached.</summary>
        internal Collider[] Colliders { get; private set; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches authored target geometry before registering its input.</summary>
        protected override void OnEnable()
        {
            // Inventory children stay collision-free and never become selectable storage targets.
            Colliders = GetComponentsInChildren<Collider>(true);
            base.OnEnable();
        }

        #endregion

        #endregion
    }
}
