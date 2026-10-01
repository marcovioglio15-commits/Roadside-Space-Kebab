using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares cached selection geometry for input actions targeting an object in the world.</summary>
    [RequireComponent(typeof(ObjectItem))]
    public abstract class ObjectTargetedInteraction : ObjectSingleInteraction
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

        /// <summary>Refreshes target geometry once for scene instances retained between Play sessions.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RefreshSession()
        {
            // OnEnable may be skipped when scene reload is disabled after editing a collider hierarchy.
            foreach (ObjectTargetedInteraction interaction in FindObjectsByType<ObjectTargetedInteraction>())
                interaction.Colliders = interaction.GetComponentsInChildren<Collider>(true);
        }

        #endregion

        #endregion
    }
}
