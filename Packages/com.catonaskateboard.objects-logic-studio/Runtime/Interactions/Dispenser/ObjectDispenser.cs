using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using Guid = System.Guid;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Supplies a prefab or stored original to the carry slot or an authored world outlet.</summary>
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
        private int withdrawals;
        private bool transferring;
        private ObjectContainer scoopOwner;
        private ObjectItem scoopOutput;
        private bool scoopStored;
        private bool destructionPending;
        private ScoopDisplay display;
        internal event System.Action<ObjectDispenser> ScoopEmptied;

        #endregion

        #region Properties

        /// <summary>Supply and placement configuration.</summary>
        public DispenserSettings Settings => settings;
        /// <summary>Whether the configuration supports separate output for a specific tool with a distinct action.</summary>
        public bool HasSeparateToolOutput => settings.SeparateToolOutput && ToolRequirement != null
            && DispenserSettings.CanSeparateToolOutput(settings.Mode, ToolRequirement.Mode, ToolRequirement.Tool);
        /// <summary>Prefab withdrawals, or logical scoops and other original departures in Scoop Only.</summary>
        public int DispensedCount => settings.ScoopOnly ? display?.WithdrawnCount ?? 0 : dispensed;
        /// <summary>Counter supplied to the authored appearance thresholds.</summary>
        internal int StepCount => settings.StepCounter == DispenseStepCounter.Remaining ? RemainingCount
            : settings.StoredSupply || settings.Unlimited ? withdrawals : Mathf.Max(0, settings.Stock - RemainingCount);
        /// <summary>Actual recoverable instances plus unused prefab stock; minus one represents unbounded prefab supply.</summary>
        public int RemainingCount => settings.ScoopOnly ? display?.Count ?? 0 : !settings.StoredSupply && settings.Unlimited ? -1
            : (settings.StoredSupply ? 0 : Mathf.Max(0, settings.Stock - dispensed))
                + (UsesStored && Container != null ? Container.StoredCount : 0);
        /// <summary>Whether originals are supplied from a linked Container before creating a prefab.</summary>
        private bool UsesStored => !settings.ScoopOnly && (settings.UseContainer || Container != null && Container.Settings.LimitToDispenserSpace && !Container.Settings.RefillDispenser);
        /// <summary>Identifies this single interaction card.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Dispenser;
        /// <summary>Scoop Only uses the held container's input rather than its own binding.</summary>
        public override bool UsesInput => !settings.ScoopOnly;
        /// <summary>Range and aiming configuration.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Owned geometry including the currently floating portions.</summary>
        internal Collider[] AimColliders => display?.Targets() ?? Colliders;
        /// <summary>Floating shapes excluded from their source's visibility checks.</summary>
        internal ISet<Collider> FloatingColliders => display?.SupplyColliders;
        /// <summary>Whether an idle, empty Scoop Only card may yield to a new recipe.</summary>
        internal bool CanYieldSupply => settings.ScoopOnly && !transferring && RemainingCount == 0;
        /// <summary>Whether contact may reload this empty card with its initial portion prefab.</summary>
        internal bool CanReloadScoopSupply => isActiveAndEnabled && !IsLocked && display != null
            && settings.ScoopDisplay.RestockForNewContainer && CanYieldSupply;

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
                dispenser.display?.Stop();
                dispenser.display = null;
                dispenser.dispensed = 0;
                dispenser.withdrawals = 0;
                dispenser.transferring = false;
                dispenser.scoopOwner = null;
                dispenser.scoopOutput = null;
                dispenser.scoopStored = false;
                dispenser.destructionPending = false;
            }
        }

        /// <summary>Starts retained scene dispensers whose normal activation did not run at Play entry.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreDisplays()
        {
            foreach (ObjectDispenser dispenser in FindObjectsByType<ObjectDispenser>())
                if (dispenser.isActiveAndEnabled && dispenser.settings.ScoopOnly && dispenser.display == null)
                    dispenser.RefreshDisplay();
        }

        #endregion

        #region Floating Supply

        /// <summary>Starts a pristine set of portions whenever Scoop Only becomes active.</summary>
        protected override void OnEnable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            base.OnEnable();
            RefreshDisplay();
        }

        /// <summary>Rebuilds floating stock when a committed preset changes the dispenser mode or magnets.</summary>
        internal void RefreshDisplay()
        {
            display?.Stop();
            display = null;
            if (!settings.ScoopOnly)
                return;
            if (TryValidate(out string warning))
                display = new ScoopDisplay(this);
            else
                Debug.LogWarning(warning, this);
        }

        /// <summary>Begins a new pack cycle while retaining the display that accepts previously withdrawn originals.</summary>
        internal void ReloadScoopSupply()
        {
            // A deposited pack uses initial stock. Scoop-triggered restock can still use its own prefab.
            display.Restock(initialSupply: true);
        }

        /// <summary>Stops floating supply without touching already withdrawn originals.</summary>
        protected override void OnDisable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            display?.Stop();
            display = null;
            NotifyScoopEmpty();
            base.OnDisable();
        }

        /// <summary>Releases availability rules when the last portion and its active transfer are gone.</summary>
        internal void NotifyScoopEmpty()
        {
            if (CanYieldSupply)
                ScoopEmptied?.Invoke(this);
        }

        /// <summary>Adds temporary collision exclusions for portions along the scoop path.</summary>
        /// <param name="pairs">Collision exclusions owned by the carried container.</param>
        /// <param name="colliders">Container's owned colliders.</param>
        internal void IgnoreScoopSupply(CarryCollisions pairs, Collider[] colliders)
        {
            display?.Ignore(pairs, colliders);
        }

        /// <summary>Updates only the active display's floating transforms.</summary>
        private void LateUpdate()
        {
            display?.Tick();
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
        /// <param name="requirement">Optional proposed tool restriction. Defaults to the applied restriction.</param>
        /// <returns>True for a complete dispenser setup.</returns>
        public bool TryValidate(DispenserSettings configuration, out string warning, InteractionToolRequirement requirement = null)
        {
            // Prefab mode and inventory mode have no hidden fallback into one another.
            warning = "Dispenser settings are missing.";
            if (configuration == null || !configuration.TryValidate(out warning, requirement ?? ToolRequirement))
                return false;
            if (configuration.StoredSupply && GetComponent<ObjectContainer>() == null)
                warning = "Add Container to this same object before linking its stored items.";
            else if (!configuration.ScoopOnly && GetComponent<ObjectContainer>() is ObjectContainer refill && refill.Settings.RefillDispenser
                && (configuration.StoredSupply || configuration.Unlimited))
                warning = "A refill Container requires finite prefab supply, not stored-only or unlimited supply.";
            else if (!configuration.ScoopOnly && configuration.Unlimited && GetComponent<ObjectContainer>() is ObjectContainer container
                && container.Settings.LimitToDispenserSpace)
                warning = "This Container limits storage to Dispenser spaces. Keep finite Stock or remove that link first.";
            else if (configuration.SupportsSteps && !InventoryFillStep.CanBind(configuration.FillSteps, Item, out warning))
                return false;
            return warning.Length == 0;
        }

        #endregion

        #region Supply

        /// <summary>Resolves output from the action that won input arbitration.</summary>
        /// <param name="action">Actual action ID from the buffered press or captured contact.</param>
        /// <returns>The output mode for this use.</returns>
        internal DispenseMode OutputMode(Guid action) => UsesSeparateOutput(action) ? settings.ToolMode : settings.Mode;

        /// <summary>Checks that this request came from the specific tool's distinct action.</summary>
        /// <param name="action">Action ID selected by the input router.</param>
        /// <returns>True when the tool route overrides the targeting output.</returns>
        internal bool UsesSeparateOutput(Guid action)
        {
            // A delayed hit retains the weapon and action that produced it.
            if (!HasSeparateToolOutput || action == Guid.Empty || Action == null || Action.action == null || Action.action.id == action)
                return false;
            return InteractionProjectileInput.Active
                ? InteractionProjectileInput.ActionId == action && InteractionProjectileInput.Tool == ToolRequirement.Tool
                : HoverObserver.ActiveTools is PlayerTools tools && tools.ActiveTool == ToolRequirement.Tool && tools.UseActionId == action;
        }

        /// <summary>Checks an entire refill against missing finite prefab stock.</summary>
        /// <param name="units">Logical units carried by the deposited item.</param>
        /// <returns>True when every deposited unit fits without discarding a remainder.</returns>
        internal bool CanRefill(int units)
        {
            return !transferring && !settings.StoredSupply && !settings.Unlimited && units > 0 && units <= dispensed;
        }

        /// <summary>Restores stock after the Container successfully consumes its reserved refill item.</summary>
        /// <param name="units">Previously validated deposited quantity.</param>
        internal void Refill(int units)
        {
            // Count and appearance change only at a successful deposit boundary.
            dispensed -= units;
            RefreshFill();
        }

        /// <summary>Checks supply before creating or activating an item.</summary>
        /// <returns>True when the available interaction has stock.</returns>
        internal bool CanTake()
        {
            // Empty linked storage remains unavailable until a deposit succeeds.
            return CanWithdraw() && (settings.ScoopOnly ? display is { Count: > 0 } : UsesStored && Container != null && Container.HasStoredItem()
                    || !settings.StoredSupply && (settings.Unlimited || dispensed < settings.Stock));
        }

        /// <summary>Checks the common transfer gate before using stock or restocking.</summary>
        /// <returns>True when this dispenser can reserve the transfer channel.</returns>
        private bool CanWithdraw()
        {
            return !transferring && Available(InteractionChannels.Transfer) && !Item.IsReserved;
        }

        /// <summary>Checks unoccupied spaces without treating a previously returned object as a second free slot.</summary>
        /// <param name="stored">Actual originals already retained by the Container.</param>
        /// <returns>True when the finite supply has a vacancy for another deposit.</returns>
        internal bool HasStorageSpace(int stored)
        {
            // Stored-only supply begins empty; finite prefab supply frees one space per successful withdrawal.
            return !transferring && !settings.Unlimited && stored < (settings.UseContainer ? settings.Stock : dispensed);
        }

        /// <summary>Releases the outputs requested by one captured weapon contact in the same frame.</summary>
        /// <param name="observer">Player whose interaction reached this dispenser.</param>
        /// <param name="mode">World output mode resolved for the captured action.</param>
        /// <param name="tool">Weapon that fired the shot, retained even if the player switched tools.</param>
        /// <param name="velocity">Incoming shot velocity captured at contact.</param>
        /// <returns>True when at least one output succeeded.</returns>
        internal bool TryTakeShot(HoverObserver observer, DispenseMode mode, PlayerTool tool, Vector3 velocity)
        {
            // Each withdrawal retains its stock, outlet and completion rules.
            InteractionToolRequirement requirement = ToolRequirement;
            bool batch = requirement != null && DispenserSettings.UsesWeaponBatch(mode, requirement.Mode, requirement.Tool)
                && requirement.Tool == tool;
            int outputs = batch ? settings.OutputsPerShot : 1;
            bool scatter = batch && settings.ScatterShotOutputs;
            DispenserShotSpread spread = scatter ? new DispenserShotSpread(velocity, settings.ScatterDistanceMultiplier) : default;
            int taken = 0;
            // Keep the batch in this input pulse and stop as soon as supply becomes unavailable.
            while (taken < outputs && this != null && TryTake(observer, mode, out ObjectGrab output, spread, taken, outputs))
            {
                if (scatter && output != null)
                    ShotBurstCollisions.Add(output.Colliders);
                taken++;
            }
            return taken > 0;
        }

        /// <summary>Commits stock only after carrying succeeds or an item is released at the selected world outlet.</summary>
        /// <param name="observer">Player receiving one object.</param>
        /// <param name="mode">Output selected by the actual input route.</param>
        /// <param name="grab">Receives the new or restored object.</param>
        /// <returns>True for a successful atomic withdrawal.</returns>
        internal bool TryTake(HoverObserver observer, DispenseMode mode, out ObjectGrab grab) => TryTake(observer, mode, out grab, default, 0, 1);

        /// <summary>Withdraws one item using the selected outlet and optional shot spread.</summary>
        /// <param name="observer">Player receiving the output.</param>
        /// <param name="mode">Output mode selected by the actual request.</param>
        /// <param name="grab">Receives the new or restored object.</param>
        /// <param name="spread">Optional launch fan for this shot.</param>
        /// <param name="index">Slot of this output inside the fan.</param>
        /// <param name="count">Total slots requested for the shot.</param>
        /// <returns>True after one withdrawal commits.</returns>
        private bool TryTake(HoverObserver observer, DispenseMode mode, out ObjectGrab grab, DispenserShotSpread spread, int index, int count)
        {
            // A second button event cannot re-enter this transaction through lifecycle events.
            grab = null;
            if (mode == DispenseMode.ScoopOnly)
                return false;
            bool carry = mode == DispenseMode.ToHand;
            if (carry && (observer == null || observer.HeldObject != null) || !CanTake())
                return false;
            transferring = true;
            try
            {
                Vector3 position = transform.position;
                Quaternion rotation = transform.rotation * Quaternion.Euler(settings.OutputRotation);
                if (!carry)
                {
                    // Only the successful withdrawal path samples an outlet; held objects never block world output.
                    DispenserOutlet outlet = settings.Outlets[Random.Range(0, settings.Outlets.Length)];
                    position = transform.TransformPoint(outlet.Position);
                    rotation = transform.rotation * Quaternion.Euler(outlet.Rotation);
                }
                if (UsesStored && Container != null && Container.HasStoredItem())
                {
                    if (!Container.TryTake(observer, position, rotation, out grab, transform, settings.PickupDuration, carry))
                        return false;
                }
                else
                {
                    if (settings.UseContainer)
                        return false;
                    GameObject created = ObjectSpawnHistory.Create(settings.Prefab, position, rotation);
                    SceneManager.MoveGameObjectToScene(created, gameObject.scene);
                    ObjectGrab candidate = created.GetComponent<ObjectGrab>();
                    if (candidate == null || carry && (!candidate.Begin(observer, transform, settings.PickupDuration) || !candidate.IsHeld))
                    {
                        created.SetActive(false);
                        Destroy(created);
                        return false;
                    }
                    grab = candidate;
                    dispensed++;
                }
                if (!carry)
                {
                    spread.GetLaunch(transform.position, position, transform.forward, index, count, out Vector3 velocity, out Vector3 tumble);
                    ObjectSpawnPhysics.Protect(grab.gameObject, velocity, tumble);
                }
                CompleteWithdrawal();
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
                return true;
            }
            finally
            {
                transferring = false;
                DestroyIfExhausted();
            }
        }

        #endregion

        #region Scoop

        /// <summary>Checks output identity without creating a prefab or revealing stored supply.</summary>
        /// <param name="receiver">Held container whose accepted flags are checked.</param>
        /// <returns>True for a compatible next output; empty stored supply remains a handled scoop target.</returns>
        internal bool MatchesScoop(ObjectContainer receiver)
        {
            // Live portions can change identity without leaving their magnets.
            if (settings.ScoopOnly)
                return display != null && (display.Find(receiver, false) != null || display.CanRestock(receiver));
            // Exhausted supply must not turn the same press into a Drop.
            ObjectItem stored = UsesStored && Container != null ? Container.PeekStored() : null;
            CatOnASkateboard.StudioIdentity.ObjectIdentity identity = stored != null ? stored.Identity
                : settings.UseContainer ? null : settings.Prefab != null ? settings.Prefab.GetComponent<CatOnASkateboard.StudioIdentity.ObjectIdentity>() : null;
            return identity != null ? identity.Matches(receiver.Settings.AllowedFlags, receiver.Settings.Match) : settings.UseContainer;
        }

        /// <summary>Claims one output until the scoop reaches its fill keyframe.</summary>
        /// <param name="receiver">Container owning the reservation.</param>
        /// <param name="output">Receives the stored original, or null for prefab supply.</param>
        /// <returns>True when supply is reserved without instantiation.</returns>
        internal bool TryReserveScoop(ObjectContainer receiver, out ObjectItem output)
        {
            output = null;
            // Empty supply may restock only for a fresh compatible box that won targeting.
            bool restock = settings.ScoopOnly && display != null && display.CanRestock(receiver);
            if (!(restock ? CanWithdraw() : CanTake())
                || !Item.Acquire(receiver, InteractionChannels.Transfer))
                return false;
            if (restock)
                display.Restock();
            output = settings.ScoopOnly ? display?.Find(receiver, true) : UsesStored && Container != null ? Container.PeekStored() : null;
            if (settings.ScoopOnly && output == null || output != null && !output.Acquire(receiver, settings.ScoopOnly ? InteractionChannels.Grab : InteractionChannels.None))
            {
                Item.Release(receiver);
                return false;
            }
            scoopOutput = output;
            scoopStored = output != null;
            scoopOwner = receiver;
            transferring = true;
            return true;
        }

        /// <summary>Commits a batch under the original scoop reservation.</summary>
        /// <param name="receiver">Current reservation owner.</param>
        /// <param name="output">Stored original captured at reservation, or null.</param>
        /// <returns>Number of logical portions withdrawn.</returns>
        internal int CommitScoop(ObjectContainer receiver, ObjectItem output)
        {
            // Keep the source reserved even if the fill keyframe withdraws several portions.
            if (scoopOwner != receiver || !isActiveAndEnabled || IsLocked || !Item.IsOwnedBy(receiver)
                || output != scoopOutput || scoopStored && (output == null || !output.IsOwnedBy(receiver)))
                return 0;
            int limit = receiver.ScoopSpace;
            if (settings.ScoopOnly)
                return display?.Take(output, receiver, Mathf.Min(limit, settings.ScoopDisplay.ScoopLimit)) ?? 0;

            // Stored outputs keep their stack order. Stop at an incompatible original.
            int taken = 0;
            while (taken < limit && this != null && isActiveAndEnabled && !IsLocked)
            {
                ObjectItem next = taken == 0 ? output : UsesStored ? Container?.PeekStored() : null;
                if (next != null)
                {
                    if (Container == null || !next.Identity.Matches(receiver.Settings.AllowedFlags, receiver.Settings.Match)
                        || taken > 0 && next.IsReserved || !Container.ConsumeStored(next))
                        break;
                }
                else
                {
                    if (settings.StoredSupply || !settings.Unlimited && dispensed >= settings.Stock || !MatchesScoop(receiver))
                        break;
                    dispensed++;
                }
                receiver.RecordScoopPortion(next != null ? next.gameObject : settings.Prefab);
                taken++;
            }
            if (taken > 0)
                CompleteWithdrawal(taken);
            return taken;
        }

        /// <summary>Releases this scoop's source and original reservations.</summary>
        /// <param name="receiver">Owner ending its request.</param>
        internal void ReleaseScoop(ObjectContainer receiver)
        {
            if (scoopOwner != receiver)
                return;
            if (scoopOutput != null)
                scoopOutput.Release(receiver);
            Item.Release(receiver);
            scoopOutput = null;
            scoopStored = false;
            scoopOwner = null;
            transferring = false;
            NotifyScoopEmpty();
            DestroyIfExhausted();
        }

        /// <summary>Updates appearance and queues optional destruction only after a successful withdrawal.</summary>
        /// <param name="amount">Number of portions taken by this action.</param>
        private void CompleteWithdrawal(int amount = 1)
        {
            // Supply and fill steps change before completion listeners inspect the dispenser.
            withdrawals += amount;
            RefreshFill();
            destructionPending = settings.DestroyWhenEmpty && !settings.Unlimited && RemainingCount == 0;
        }

        /// <summary>Removes exhausted finite supply after movement and reservations release it.</summary>
        private void DestroyIfExhausted()
        {
            // A refill during completion callbacks can make this dispenser usable again.
            if (!destructionPending || transferring || RemainingCount != 0)
                return;
            destructionPending = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        #endregion

        #endregion
    }
}
