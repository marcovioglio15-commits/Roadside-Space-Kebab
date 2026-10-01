using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Starts carrying a new prefab or an original item recovered from this object's Container.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Object Dispenser")]
    public sealed class ObjectDispenser : ObjectInventoryInteraction
    {
        #region Serialized Fields

        [Header("Dispenser")]
        [Tooltip("Prefab supply or linked container, stock limit and output pose.")]
        [SerializeField]
        private DispenserSettings settings = new DispenserSettings();

        #endregion

        #region State

        private int dispensed;
        private bool transferring;

        #endregion

        #region Properties

        /// <summary>Supply and placement configuration.</summary>
        public DispenserSettings Settings => settings;
        /// <summary>Successful withdrawals from finite prefab stock during this instance's lifetime.</summary>
        public int DispensedCount => dispensed;
        /// <summary>Actual recoverable instances plus unused prefab stock; minus one represents unbounded prefab supply.</summary>
        public int RemainingCount => !settings.UseContainer && settings.Unlimited ? -1
            : (settings.UseContainer ? 0 : Mathf.Max(0, settings.Stock - dispensed))
                + (UsesStored && Container != null ? Container.StoredCount : 0);
        /// <summary>Whether originals are supplied from a linked Container before creating a prefab.</summary>
        private bool UsesStored => settings.UseContainer || Container != null && Container.Settings.LimitToDispenserSpace;
        /// <summary>Identifies this single interaction card.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Dispenser;
        /// <summary>Range and aiming configuration.</summary>
        public override TransferTargetSettings Target => settings.Target;

        #endregion

        #region Methods

        #region Session

        /// <summary>Clears transient inventory counters when a new Play session retains managed scene fields.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetSession()
        {
            // Ordinary pooling keeps stock; only a new Play session resets these instance counters.
            foreach (ObjectDispenser dispenser in FindObjectsByType<ObjectDispenser>(FindObjectsInactive.Include))
            {
                dispenser.dispensed = 0;
                dispenser.transferring = false;
            }
        }

        #endregion

        #region Validation

        /// <summary>Checks the local supply dependency before input binding.</summary>
        /// <param name="warning">Receives a missing dependency or invalid setting.</param>
        /// <returns>True when this dispenser can participate.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // The editor draft uses the same dependency check before Apply.
            return TryValidate(settings, out warning);
        }

        /// <summary>Validates reusable settings and a same-object Container when linked storage is selected.</summary>
        /// <param name="configuration">Proposed supply settings.</param>
        /// <param name="warning">Receives the first missing dependency.</param>
        /// <returns>True for a complete dispenser setup.</returns>
        public bool TryValidate(DispenserSettings configuration, out string warning)
        {
            // Prefab mode and inventory mode have no hidden fallback into one another.
            warning = "Dispenser settings are missing.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            if (configuration.UseContainer && GetComponent<ObjectContainer>() == null)
                warning = "Add Container to this same object before linking its stored items.";
            else if (configuration.Unlimited && GetComponent<ObjectContainer>() is ObjectContainer container
                && container.Settings.LimitToDispenserSpace)
                warning = "This Container limits storage to Dispenser spaces. Keep finite Stock or remove that link first.";
            else if ((configuration.UseContainer || !configuration.Unlimited) && !InventoryFillStep.CanBind(configuration.FillSteps, Item, out warning))
                return false;
            return warning.Length == 0;
        }

        #endregion

        #region Supply

        /// <summary>Checks supply before creating or activating an item.</summary>
        /// <returns>True when the available interaction has stock.</returns>
        internal bool CanTake()
        {
            // Empty linked storage remains unavailable until a deposit succeeds.
            return !transferring && Available(InteractionChannels.Transfer) && !Item.IsReserved
                && (UsesStored && Container != null && Container.HasStoredItem()
                    || !settings.UseContainer && (settings.Unlimited || dispensed < settings.Stock));
        }

        /// <summary>Checks unoccupied spaces without treating a previously returned object as a second free slot.</summary>
        /// <param name="stored">Actual originals already retained by the Container.</param>
        /// <returns>True when the finite supply has a vacancy for another deposit.</returns>
        internal bool HasStorageSpace(int stored)
        {
            // Stored-only supply begins empty; finite prefab supply frees one space per successful withdrawal.
            return !transferring && !settings.Unlimited && stored < (settings.UseContainer ? settings.Stock : dispensed);
        }

        /// <summary>Commits stock only after an item has acquired the observer's empty carry slot.</summary>
        /// <param name="observer">Player receiving one object.</param>
        /// <param name="grab">Receives the object now being carried.</param>
        /// <returns>True for a successful atomic withdrawal.</returns>
        internal bool TryTake(HoverObserver observer, out ObjectGrab grab)
        {
            // A second button event cannot re-enter this transaction through lifecycle events.
            grab = null;
            if (observer == null || observer.HeldObject != null || !CanTake())
                return false;
            transferring = true;
            try
            {
                Vector3 position = transform.position;
                Quaternion rotation = transform.rotation * Quaternion.Euler(settings.OutputRotation);
                if (UsesStored && Container != null && Container.HasStoredItem())
                {
                    if (!Container.TryTake(observer, position, rotation, out grab, transform, settings.PickupDuration))
                        return false;
                }
                else
                {
                    if (settings.UseContainer)
                        return false;
                    GameObject created = Instantiate(settings.Prefab, position, rotation);
                    SceneManager.MoveGameObjectToScene(created, gameObject.scene);
                    ObjectGrab candidate = created.GetComponent<ObjectGrab>();
                    if (candidate == null || !candidate.Begin(observer, transform, settings.PickupDuration) || !candidate.IsHeld)
                    {
                        created.SetActive(false);
                        Destroy(created);
                        return false;
                    }
                    grab = candidate;
                    dispensed++;
                }
                RefreshFill();
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
                return true;
            }
            finally
            {
                transferring = false;
            }
        }

        #endregion

        #endregion
    }
}
