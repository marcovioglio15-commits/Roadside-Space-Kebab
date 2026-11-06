using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Refreshes only the caches required by explicit card changes.</summary>
    internal static class InteractionRuntimeRefresh
    {
        #region Methods

        #region Binding

        /// <summary>Retains carry and dialogue progress for ordinary settings edits.</summary>
        /// <param name="feature">Changed interaction whose settings already passed validation.</param>
        internal static void Apply(ObjectInteraction feature)
        {
            if (feature == null || !feature.isActiveAndEnabled)
                return;
            switch (feature)
            {
                case ObjectGrab grab:
                    break;
                case ObjectDialogue dialogue:
                    dialogue.RefreshRuntime();
                    break;
                case ObjectDispenser dispenser:
                    dispenser.RefreshDisplay();
                    dispenser.RefreshRuntimeFill();
                    break;
                case ObjectInventoryInteraction inventory:
                    inventory.RefreshRuntimeFill();
                    break;
                case ObjectHover hover:
                    hover.Refresh();
                    break;
                default:
                    // Features with prepared geometry, contacts or source bindings rebuild only at this edit boundary.
                    feature.enabled = false;
                    feature.enabled = true;
                    break;
            }
            RebindRoot(feature.gameObject);
        }

        /// <summary>Refreshes cached cross-feature links once after adding or removing a card.</summary>
        /// <param name="root">Changed item root.</param>
        internal static void RebindRoot(GameObject root)
        {
            if (root.TryGetComponent(out ObjectItem item))
                item.RefreshRuntimeBindings();
            if (root.TryGetComponent(out ObjectGrab grab) && grab.isActiveAndEnabled)
                grab.RefreshCarryGeometry();
            SingleInteractionRegistry.Invalidate();
        }

        #endregion

        #endregion
    }
}
