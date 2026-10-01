using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Coordinates shared inventory appearance while retaining independent interaction locks and input.</summary>
    public abstract class ObjectInventoryInteraction : ObjectTargetedInteraction
    {
        #region State

        private InventoryFillRun fill;
        private bool fillChecked;
        /// <summary>Same-object storage component, including a disabled deposit action.</summary>
        protected ObjectContainer Container { get; private set; }
        /// <summary>Same-object supply component, including a disabled withdrawal action.</summary>
        protected ObjectDispenser Dispenser { get; private set; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Finds the optional counterpart once and prepares initial fill geometry.</summary>
        protected override void OnEnable()
        {
            // Storage and supply never discover each other in the per-frame input loop.
            Container = GetComponent<ObjectContainer>();
            Dispenser = GetComponent<ObjectDispenser>();
            if (Container != null)
            {
                Container.Container = Container;
                Container.Dispenser = Dispenser;
            }
            if (Dispenser != null)
            {
                Dispenser.Container = Container;
                Dispenser.Dispenser = Dispenser;
            }
            base.OnEnable();
            RefreshFill();
        }

        /// <summary>Restores retained scene geometry before runtime inventory counters reset.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAppearance()
        {
            // Pooling retains inventory; a new Play session starts from authored meshes and materials.
            foreach (ObjectInventoryInteraction interaction in FindObjectsByType<ObjectInventoryInteraction>(FindObjectsInactive.Include))
            {
                interaction.fill?.Restore();
                interaction.fill = null;
                interaction.fillChecked = false;
            }
        }

        /// <summary>Applies initial stock appearance even with scene and domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RefreshSession()
        {
            // Both inventory counters have already reset before these thresholds are evaluated.
            foreach (ObjectInventoryInteraction interaction in FindObjectsByType<ObjectInventoryInteraction>())
                interaction.RefreshFill();
        }

        #endregion

        #region Appearance

        /// <summary>Refreshes both fill layers after an atomic stock change, with Dispenser taking precedence on shared targets.</summary>
        internal void RefreshFill()
        {
            // The Container coordinates both features when present, including while its deposit action is disabled.
            ObjectInventoryInteraction owner = Container != null ? Container : this;
            if (!owner.fillChecked)
            {
                owner.fillChecked = true;
                if (!InventoryFillRun.TryCreate(Item, Container != null ? Container.Settings : null,
                    Dispenser != null ? Dispenser.Settings : null, out owner.fill, out string warning))
                    Debug.LogWarning(warning, this);
            }
            owner.fill?.Update(Container != null ? Container.StoredCount : -1, Dispenser != null ? Dispenser.RemainingCount : -1);
        }

        #endregion

        #endregion
    }
}
