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
            if (InteractionRuntimeState.IsStaging)
                return;
            // Storage and supply never discover each other in the per-frame input loop.
            // Scoop alternatives own separate appearance layers. Traditional storage can still share a supply.
            if (this is ObjectContainer { Settings: { ScoopFromDispenser: true } } scoopContainer)
                Container = scoopContainer;
            else if (this is ObjectDispenser { Settings: { ScoopOnly: true } } scoopDispenser)
                Dispenser = scoopDispenser;
            else
            {
                Container = this as ObjectContainer;
                Dispenser = this as ObjectDispenser;
                if (Container == null)
                    foreach (ObjectContainer candidate in GetComponents<ObjectContainer>())
                        if (!candidate.Settings.ScoopFromDispenser && (Container == null || candidate.isActiveAndEnabled))
                            Container = candidate;
                if (Dispenser == null)
                    foreach (ObjectDispenser candidate in GetComponents<ObjectDispenser>())
                        if (!candidate.Settings.ScoopOnly && (Dispenser == null || candidate.isActiveAndEnabled))
                            Dispenser = candidate;
            }
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

        /// <summary>Rebinds thresholds after a committed runtime edit while retaining inventory counts.</summary>
        internal void RefreshRuntimeFill()
        {
            ObjectInventoryInteraction owner = Container != null ? Container : this;
            owner.fill?.Restore();
            owner.fill = null;
            owner.fillChecked = false;
            RefreshFill();
        }

        /// <summary>Refreshes both fill layers after an atomic stock change, with Dispenser taking precedence on shared targets.</summary>
        internal void RefreshFill()
        {
            // Disabled alternatives must never write their initial appearance over the chosen recipe.
            if (this is ObjectContainer { Settings: { ScoopFromDispenser: true } } && !isActiveAndEnabled
                || this is ObjectDispenser { Settings: { ScoopOnly: true } })
                return;
            ObjectInventoryInteraction owner = Container != null ? Container : this;
            if (!owner.fillChecked)
            {
                owner.fillChecked = true;
                if (!InventoryFillRun.TryCreate(Item, Container, Dispenser, out owner.fill, out string warning))
                    Debug.LogWarning(warning, this);
            }
            owner.fill?.Update(Container != null ? Container.Settings.RefillDispenser && Dispenser != null
                ? Dispenser.RemainingCount : Container.FillCount : -1, Dispenser != null ? Dispenser.StepCount : -1);
        }

        #endregion

        #endregion
    }
}
